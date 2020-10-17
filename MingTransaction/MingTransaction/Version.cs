namespace MingTransaction
{
    internal class Version
    {
        public long ReadTimeStamp
        {
            get;
            set;
        }

        public Transaction WriteTransaction
        {
            get;
            set;
        }

        public string Content
        {
            get;
            set;
        }
    }
}