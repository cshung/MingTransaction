namespace MingTransaction
{
    using System.Threading.Tasks;

    internal class GetMessage : IMessage
    {
        public GetMessage(TaskCompletionSource<GetResult> tcs, Transaction transaction, string key)
        {
            this.TaskCompletionSource = tcs;
            this.Transaction = transaction;
            this.Key = key;
        }

        public TaskCompletionSource<GetResult> TaskCompletionSource { get; }

        public Transaction Transaction { get; }

        public string Key { get; }
    }
}