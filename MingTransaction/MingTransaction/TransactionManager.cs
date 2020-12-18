namespace MingTransaction
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class TransactionManager
    {
        private const bool delayCollection = true;
        private Queue<IMessage> m_messages = new Queue<IMessage>();
        private long m_id = 0;
        private Dictionary<string, List<Version>> m_versions = new Dictionary<string, List<Version>>();
        private SortedSet<long> m_pendingTransactions = new SortedSet<long>();

        public TransactionManager()
        {
            if (delayCollection)
            {
                GC.RegisterForFullGCNotification(10, 10);
                new Thread(WaitForFullGC).Start();
            }
        }

        private void WaitForFullGC()
        {
            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                // Check for a notification of an approaching collection.
                GCNotificationStatus s = GC.WaitForFullGCApproach();
                if (s == GCNotificationStatus.Succeeded)
                {
                    this.CollectAsync().Wait();
                    GC.Collect();
                }
            }
        }

        public Task CollectAsync()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            this.Enqueue(new CollectMessage(tcs));
            return tcs.Task;
        }

        public Task ShutdownAsync()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            this.Enqueue(new ShutdownMessage(tcs));
            return tcs.Task;
        }

        public void Run()
        {
            while (true)
            {
                IMessage message;
                lock (m_messages)
                {
                    while (m_messages.Count == 0)
                    {
                        Monitor.Wait(m_messages);
                    }
                    message = m_messages.Dequeue();
                }
                if (message is ShutdownMessage)
                {
                    ((ShutdownMessage)message).TaskCompletionSource.SetResult(true);
                    break;
                }
                Process(message);
            }
        }

        internal void OnTransactionCompleted(long id)
        {
            this.m_pendingTransactions.Remove(id);
        }

        internal void Enqueue(IMessage message)
        {
            lock (m_messages)
            {
                m_messages.Enqueue(message);
                Monitor.Pulse(m_messages);
            }
        }

        private void Process(IMessage message)
        {
            switch (message)
            {
                case InitializeTransactionMessage initializeTransactionMessage:
                    initializeTransactionMessage.Transaction.DoInitialize(initializeTransactionMessage.TaskCompletionSource);
                    break;
                case GetMessage getMessage:
                    getMessage.Transaction.DoGet(getMessage.TaskCompletionSource, getMessage.Key);
                    break;
                case PutMessage putMessage:
                    putMessage.Transaction.DoPut(putMessage.TaskCompletionSource, putMessage.Key, putMessage.Value);
                    break;
                case CommitMessage commitMessage:
                    commitMessage.Transaction.DoCommit(commitMessage.TaskCompletionSource);
                    break;
                case AbortMessage abortMessage:
                    abortMessage.Transaction.DoAbort(abortMessage.TaskCompletionSource);
                    break;
                case CollectMessage collectMessage:
                    this.Collect(collectMessage.TaskCompletionSource);
                    break;
                default:
                    break;
            }
        }

        internal void Collect(TaskCompletionSource<bool> taskCompletionSource)
        {
            this.DoCollect();
            taskCompletionSource.SetResult(true);
        }

        internal void Collect()
        {
            if (!delayCollection)
            {
                this.DoCollect();
            }
        }

        internal void DoCollect()
        {
            if (this.m_pendingTransactions.Count == 0)
            {
                Version keep = null;
                foreach (List<Version> versions in this.m_versions.Values)
                {
                    for (int i = versions.Count - 1; i >= 0; i--)
                    {
                        Version version = versions[i];
                        Transaction writingTransaction = version.WriteTransaction;
                        if (writingTransaction == null || writingTransaction.m_state == TransactionState.Committed)
                        {
                            keep = version;
                            break;
                        }
                    }
                    keep.ReadTimeStamp = 0;
                    keep.WriteTransaction = null;
                    versions.Clear();
                    versions.Add(keep);
                }
            }
            else
            {
                long oldestTransaction = this.m_pendingTransactions.Min;
                foreach (List<Version> versions in this.m_versions.Values)
                {
                    List<Version> copy = new List<Version>(versions);
                    List<int> toDelete = new List<int>();
                    bool done = false;
                    for (int i = versions.Count - 1; i >= 0; i--)
                    {
                        if (done)
                        {
                            toDelete.Add(i);
                        }
                        else
                        {
                            Version version = versions[i];
                            Transaction writingTransaction = version.WriteTransaction;
                            if (writingTransaction == null)
                            {
                                version.WriteTransaction = writingTransaction = new Transaction(this) { m_id = 0, m_state = TransactionState.Committed };
                            }
                            if (writingTransaction.m_state == TransactionState.Aborted)
                            {
                                toDelete.Add(i);
                            }
                            else if (writingTransaction.m_state == TransactionState.Committed && writingTransaction.m_id <= oldestTransaction)
                            {
                                // This is the last one we will need, any older version is gone
                                done = true;
                            }

                        }
                    }
                    foreach (int v in toDelete)
                    {
                        versions.RemoveAt(v);
                    }
                    Debug.Assert(versions.Count > 0);
                    bool temp = false;
                    foreach (Version v in versions)
                    {
                        if (v.WriteTransaction.m_state == TransactionState.Committed)
                        {
                            temp = true;
                        }
                    }
                    Debug.Assert(temp);
                }
            }
        }

        internal long GetNextTransactionId()
        {
            long result = ++this.m_id;
            this.m_pendingTransactions.Add(result);
            return result;
        }

        internal List<Version> GetVersions(string key)
        {
            List<Version> versions;
            if (!m_versions.TryGetValue(key, out versions))
            {
                versions = new List<Version>();
                versions.Add(new Version());
                m_versions.Add(key, versions);
            }
            return versions;
        }
    }
}