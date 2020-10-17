namespace MingTransaction
{
    internal enum TransactionState
    {
        Uninitialized,
        Pending,
        Committed,
        Aborted
    }
}