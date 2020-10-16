namespace kvs
{
    using System.Threading.Tasks;

    internal class CommitMessage : IMessage
    {
        public CommitMessage(TaskCompletionSource<bool> tcs, Transaction transaction)
        {
            this.TaskCompletionSource = tcs;
            this.Transaction = transaction;
        }

        public TaskCompletionSource<bool> TaskCompletionSource { get; }

        public Transaction Transaction { get; }
    }
}