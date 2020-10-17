namespace MingTransaction
{
    using System.Threading.Tasks;

    internal class CollectMessage : IMessage
    {
        public TaskCompletionSource<bool> TaskCompletionSource { get; }

        public CollectMessage(TaskCompletionSource<bool> tcs)
        {
            this.TaskCompletionSource = tcs;
        }
    }
}