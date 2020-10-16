namespace kvs
{
    using System;
    using System.Threading.Tasks;

    class Program
    {
        public static void Main(string[] args)
        {
            TransactionManager transactionManager = new TransactionManager();
            Task.Factory.StartNew(() => Work(transactionManager));
            transactionManager.Run();
        }

        public static void Work(TransactionManager transactionManager)
        {
            WorkAsync(transactionManager).Wait();
        }

        private static async Task WorkAsync(TransactionManager transactionManager)
        {
            Transaction t1 = new Transaction(transactionManager);
            Transaction t2 = new Transaction(transactionManager);
            Transaction t3 = new Transaction(transactionManager);
            await t1.InitializeAsync();
            await t2.InitializeAsync();
            await t3.InitializeAsync();
            await t1.PutAsync("Hello", "World");
            await t2.PutAsync("Hello", "Cruel");
            await t2.AbortAsync();
            Console.WriteLine((await t3.GetAsync("Hello")).Content);
            await t1.CommitAsync();
            await t3.CommitAsync();
            await transactionManager.ShutdownAsync();
        }
    }
}
