namespace MingTransaction.Tests
{
    using System.Threading.Tasks;
    using Xunit;

    public class MingTransactionTests
    {
        [Fact]
        public void SimpleTest()
        {
            TransactionManager transactionManager = new TransactionManager();
            Task.Factory.StartNew(() => Work(transactionManager));
            transactionManager.Run();
        }

        private void Work(TransactionManager transactionManager)
        {
            WorkAsync(transactionManager).Wait();
        }

        private async Task WorkAsync(TransactionManager transactionManager)
        {
            Transaction t1 = new Transaction(transactionManager);
            Transaction t2 = new Transaction(transactionManager);
            Transaction t3 = new Transaction(transactionManager);
            await t1.InitializeAsync();
            await t2.InitializeAsync();
            await t3.InitializeAsync();
            Assert.True(await t1.PutAsync("Hello", "World"));
            Assert.True(await t2.PutAsync("Hello", "Cruel"));
            await t2.AbortAsync();
            GetResult getResult = await t3.GetAsync("Hello");
            Assert.True(getResult.Succeed);
            Assert.Equal("World", getResult.Content);
            Assert.True(await t1.CommitAsync());
            Assert.True(await t3.CommitAsync());
            await transactionManager.ShutdownAsync();
        }
    }
}
