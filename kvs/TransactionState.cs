namespace kvs
{
    internal enum TransactionState
    {
        Uninitialized,
        Pending,
        Committed,
        Aborted
    }
}