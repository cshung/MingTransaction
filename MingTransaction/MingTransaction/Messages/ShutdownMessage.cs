namespace MingTransaction
{
    using System.Threading.Tasks;

    internal class ShutdownMessage : IMessage
    {
        public TaskCompletionSource<bool> TaskCompletionSource { get; }

        public ShutdownMessage(TaskCompletionSource<bool> tcs)
        {
            this.TaskCompletionSource = tcs;
        }
    }
}