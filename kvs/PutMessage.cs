namespace kvs
{
    using System.Threading.Tasks;

    internal class PutMessage : IMessage
    {
        public PutMessage(TaskCompletionSource<bool> tcs, Transaction transaction, string key, string value)
        {
            this.TaskCompletionSource = tcs;
            this.Transaction = transaction;
            this.Key = key;
            this.Value = value;
        }

        public TaskCompletionSource<bool> TaskCompletionSource { get; }

        public Transaction Transaction { get; }

        public string Key { get; }

        public string Value { get; }
    }
}