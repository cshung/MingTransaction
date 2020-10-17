namespace MingTransaction
{
    using System.Threading.Tasks;

    internal class InitializeTransactionMessage : IMessage
    {
        public InitializeTransactionMessage(TaskCompletionSource<bool> tcs, Transaction transaction)
        {
            this.TaskCompletionSource = tcs;
            this.Transaction = transaction;
        }

        public TaskCompletionSource<bool> TaskCompletionSource { get; }

        public Transaction Transaction { get; }
    }
}