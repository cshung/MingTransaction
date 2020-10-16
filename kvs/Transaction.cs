namespace kvs
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public class Transaction
    {
        private TransactionManager m_transactionManager;
        private int m_dependentTransactionCount;
        private List<Transaction> m_dependentTransactions;
        private TaskCompletionSource<bool> m_commitTaskCompletionSource;

        // These should be accessed only by TransactionManager
        internal long m_id;
        internal TransactionState m_state = TransactionState.Uninitialized;

        public Transaction(TransactionManager transactionManager)
        {
            this.m_transactionManager = transactionManager;
            this.m_dependentTransactions = new List<Transaction>();
        }

        public Task InitializeAsync()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            this.m_transactionManager.Enqueue(new InitializeTransactionMessage(tcs, this));
            return tcs.Task;
        }

        public Task<GetResult> GetAsync(string key)
        {
            TaskCompletionSource<GetResult> tcs = new TaskCompletionSource<GetResult>();
            this.m_transactionManager.Enqueue(new GetMessage(tcs, this, key));
            return tcs.Task;
        }

        public Task<bool> PutAsync(string key, string value)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            this.m_transactionManager.Enqueue(new PutMessage(tcs, this, key, value));
            return tcs.Task;
        }

        public Task<bool> CommitAsync()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            this.m_transactionManager.Enqueue(new CommitMessage(tcs, this));
            return tcs.Task;
        }

        public Task<bool> AbortAsync()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            this.m_transactionManager.Enqueue(new AbortMessage(tcs, this));
            return tcs.Task;
        }

        internal void DoInitialize(TaskCompletionSource<bool> taskCompletionSource)
        {
            this.m_id = this.m_transactionManager.GetNextTransactionId();
            this.m_state = TransactionState.Pending;
            taskCompletionSource.SetResult(true);
        }

        private Version GetLastWrittenVersion(string key)
        {
            List<Version> versions = this.m_transactionManager.GetVersions(key);
            for (int i = versions.Count - 1; i >= 0; i--)
            {
                Version version = versions[i];
                Transaction writingTransaction = version.WriteTransaction;
                if (writingTransaction == null)
                {
                    version.WriteTransaction = writingTransaction = new Transaction(this.m_transactionManager) { m_id = 0, m_state = TransactionState.Committed };
                }
                if (writingTransaction.m_state != TransactionState.Aborted && writingTransaction.m_id <= this.m_id)
                {
                    return version;
                }
            }
            Debug.Assert(false);
            return null;
        }

        internal void DoGet(TaskCompletionSource<GetResult> taskCompletionSource, string key)
        {
            GetResult result = new GetResult();
            if (this.m_state != TransactionState.Pending)
            {
                result.Succeed = false;
                taskCompletionSource.SetResult(result);
            }
            else
            {
                result.Succeed = true;
                Version writingVersion = GetLastWrittenVersion(key);
                Transaction writingTransaction = writingVersion.WriteTransaction;
                Debug.Assert(writingTransaction.m_state != TransactionState.Uninitialized);
                if (writingTransaction.m_state != TransactionState.Aborted)
                {
                    if (writingTransaction != this && writingTransaction.m_state == TransactionState.Pending)
                    {
                        this.m_dependentTransactionCount++;
                        writingTransaction.m_dependentTransactions.Add(this);
                    }
                    writingVersion.ReadTimeStamp = Math.Max(this.m_id, writingVersion.ReadTimeStamp);
                    result.Content = writingVersion.Content;
                    taskCompletionSource.SetResult(result);
                }
            }
        }

        internal void DoPut(TaskCompletionSource<bool> taskCompletionSource, string key, string value)
        {
            if (this.m_state != TransactionState.Pending)
            {
                taskCompletionSource.SetResult(false);
            }
            else
            {
                Version writingVersion = GetLastWrittenVersion(key);
                Transaction writingTransaction = writingVersion.WriteTransaction;
                if (writingTransaction.m_id <= this.m_id)
                {
                    Debug.Assert(writingTransaction.m_state != TransactionState.Uninitialized);
                    if (writingTransaction.m_state != TransactionState.Aborted)
                    {
                        if (writingVersion.ReadTimeStamp > this.m_id)
                        {
                            this.Abort();
                            taskCompletionSource.SetResult(false);
                        }
                        else
                        {
                            Version newVersion = new Version();
                            newVersion.Content = value;
                            newVersion.ReadTimeStamp = this.m_id;
                            newVersion.WriteTransaction = this;
                            this.m_transactionManager.GetVersions(key).Add(newVersion);
                            taskCompletionSource.SetResult(true);
                        }
                    }
                }
            }
        }

        internal void DoCommit(TaskCompletionSource<bool> taskCompletionSource)
        {
            this.m_commitTaskCompletionSource = taskCompletionSource;
            this.TryCommit();
        }

        internal void DoAbort(TaskCompletionSource<bool> taskCompletionSource)
        {
            this.Abort();
            taskCompletionSource.SetResult(true);
        }

        private void TryCommit()
        {
            if (this.m_commitTaskCompletionSource != null)
            {
                if (this.m_dependentTransactionCount == 0)
                {
                    this.m_state = TransactionState.Committed;
                    this.m_transactionManager.OnTransactionCompleted(this.m_id);
                    foreach (Transaction dependentTransaction in this.m_dependentTransactions)
                    {
                        Debug.Assert(dependentTransaction.m_state == TransactionState.Pending);
                        dependentTransaction.ReduceDependencyCount();
                    }
                    this.m_commitTaskCompletionSource.SetResult(true);
                    this.m_commitTaskCompletionSource = null;
                }
            }
        }

        private void ReduceDependencyCount()
        {
            this.m_dependentTransactionCount--;
            this.TryCommit();
        }

        private void Abort()
        {
            this.m_state = TransactionState.Aborted;
            this.m_transactionManager.OnTransactionCompleted(this.m_id);
            foreach (Transaction dependentTransaction in this.m_dependentTransactions)
            {
                Debug.Assert(dependentTransaction.m_state == TransactionState.Pending);
                dependentTransaction.Abort();
            }
            if (this.m_commitTaskCompletionSource != null)
            {
                this.m_commitTaskCompletionSource.SetResult(false);
                this.m_commitTaskCompletionSource = null;
            }
        }
    }
}