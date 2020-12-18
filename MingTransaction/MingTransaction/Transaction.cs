namespace MingTransaction
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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
                Version writingVersion = this.m_transactionManager.GetLastWrittenVersion(key, this.m_id);
                Transaction writingTransaction = writingVersion.WriteTransaction;
                Debug.Assert(writingTransaction.m_state != TransactionState.Uninitialized);
                Debug.Assert(writingTransaction.m_state != TransactionState.Aborted);
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

        internal void DoPut(TaskCompletionSource<bool> taskCompletionSource, string key, string value)
        {
            if (this.m_state != TransactionState.Pending)
            {
                taskCompletionSource.SetResult(false);
            }
            else
            {
                Version writingVersion = this.m_transactionManager.GetLastWrittenVersion(key, this.m_id);
                Transaction writingTransaction = writingVersion.WriteTransaction;
                Debug.Assert(writingTransaction.m_id <= this.m_id);
                Debug.Assert(writingTransaction.m_state != TransactionState.Uninitialized);
                Debug.Assert(writingTransaction.m_state != TransactionState.Aborted);
                if (writingVersion.ReadTimeStamp > this.m_id)
                {
                    this.Abort(false);
                    taskCompletionSource.SetResult(false);
                }
                else
                {
                    Version newVersion = new Version();
                    newVersion.Content = value;
                    newVersion.ReadTimeStamp = this.m_id;
                    newVersion.WriteTransaction = this;
                    this.m_transactionManager.AddVersion(key, newVersion);
                    taskCompletionSource.SetResult(true);
                }
            }
        }

        internal void DoCommit(TaskCompletionSource<bool> taskCompletionSource)
        {
            if (this.m_state != TransactionState.Pending)
            {
                taskCompletionSource.SetResult(false);
            }
            else
            {
                this.m_commitTaskCompletionSource = taskCompletionSource;
                this.TryCommit(false);
            }
        }

        internal void DoAbort(TaskCompletionSource<bool> taskCompletionSource)
        {
            this.Abort(false);
            taskCompletionSource.SetResult(true);
        }

        private void TryCommit(bool calledRecursively)
        {
            if (this.m_commitTaskCompletionSource != null)
            {
                if (this.m_dependentTransactionCount == 0)
                {
                    this.m_state = TransactionState.Committed;
                    this.m_transactionManager.OnTransactionCompleted(this.m_id);
                    foreach (Transaction dependentTransaction in this.m_dependentTransactions)
                    {
                        if (dependentTransaction.m_state == TransactionState.Pending)
                        {
                            dependentTransaction.ReduceDependencyCount();
                        }
                        else
                        {
                            Debug.Assert(dependentTransaction.m_state == TransactionState.Aborted);
                        }
                    }
                    if (!calledRecursively)
                    {
                        this.m_transactionManager.Collect();
                    }
                    this.m_commitTaskCompletionSource.SetResult(true);
                    this.m_commitTaskCompletionSource = null;
                }
            }
        }

        private void ReduceDependencyCount()
        {
            this.m_dependentTransactionCount--;
            this.TryCommit(true);
        }

        private void Abort(bool calledRecursively)
        {
            this.m_state = TransactionState.Aborted;
            this.m_transactionManager.OnTransactionCompleted(this.m_id);
            foreach (Transaction dependentTransaction in this.m_dependentTransactions)
            {
                Debug.Assert(dependentTransaction.m_state == TransactionState.Pending);
                dependentTransaction.Abort(true);
            }
            if (!calledRecursively)
            {
                this.m_transactionManager.Collect();
            }
            if (this.m_commitTaskCompletionSource != null)
            {
                this.m_commitTaskCompletionSource.SetResult(false);
                this.m_commitTaskCompletionSource = null;
            }
        }
    }
}