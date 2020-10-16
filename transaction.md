# Schedule
A sequence of Read(T, X), Write(T, X), Commit(T), Abort(T).
The order of operations must respect the transaction.

## Recovery
A schedule is recoverable if a transaction does not commit until all transactions that produced the data item it reads is committed. Such a schedule never requires rolling back committed transaction.

> The reason why a transaction needs to be rollback is because it made wrong assumption based on reading wrong input. If a transaction commits only after all its inputs are known to be correct, there is no way such a transaction is wrong.

A schedule is called cascadeless if it only read committed data. Such a schedule never requires rolling back a transaction becaues it read wrong data from aborting transaction. Therefore aborting one transaction will never cause another to abort.

A schedule is called strict if it never reads or writes a data item until the transaction producing the data item is committed. Such a schedule allows undoing a write simply by replacing it by the value before it was written (because that is guaranteed to be already committed and newer committed version cannot exist)

## Serializability

Two operations conflicts if:
1. They belongs to different transactions
2. They operate on the same data item, and
3. One of them is write.

If all pairs of conflicting operations in a schedule always follow the same order, the schedule is conflict serializable.

# Timestamp Ordering
A transaction is given an unique time stamp. The idea is that the schedule produced by this concurrency control protocol is always conflict serializable to the timestamp order.

A data item is marked with a read timestamp and a write timestamp, representing the last transaction that read/write the item.

# Basic Time Stamp Ordering
## Rules:
1. If Read(T, X) but WTS(X) > TS(T), abort

> Younger transaction already overwritten what I would like to read, too bad.

2. If Write(T, X) but RTS(X) < TS(T), abort

> Yonger transaction have already read the value, if I write, that transaction would have read a wrong one, so bail out.

3. If Write(T, X) but WTS(X) < TS(T), abort

> Yonger transaction have already written the value, if I write, the final state would be wrong, so bail out.

Obviously these rules guarantee conflict serializability to time stamp order. It may leads to cascading abort as there is no measure to prevent reading uncommitted writes.

## Recoverability concerns
To produce a recoverable schedule, commit only when dependent transactions are committed.
To produce a strict schedule, read/write data only when producing transactions are committed.

> To abort, we need to restore the pre-write values, in reverse timestamp order.

# Multiversion Concurrency Control (MVCC)
Rule 1 of Basic time stamp is unfortunate. We could have saved it by maintaining old versions.

In MVCC, every write creates a new version with read/write time stamp set to the time stamp of the transaction writing it.

## Rules
1. If Write(T, X), but the pair Write(T1, X), Read(T2, X) exist where T1 is the last transaction that non-aborted write X before T and T2 is younger than T, abort.

> Same as rule 2 in Basic Time Stamp Ordering.

2. If Read(T, X), find the last non-aborted write X before T and return the value.

> Rule 1 and rule 3 never applies because we have multiple versions.

## Recoverability concerns

Same trick for postponing commit to guarantee conflict serializability.
Same need for cascading abort - same trick to prevent.

> To abort, eliminate bad created versions. Therefore all versions are either committed or pending.
