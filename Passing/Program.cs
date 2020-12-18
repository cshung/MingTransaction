namespace Passing
{
    using MingTransaction;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    internal static class Program
    {
        static TransactionManager transactionManager = new TransactionManager();
        static Random random = new Random(0);
        // Bug - two thread buggy version is leading to a situation where we only abort.
        const int NUM_PLAYERS = 2;

        static void Main(string[] args)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Task.Factory.StartNew(StartGame);
            transactionManager.Run();
            Console.WriteLine(stopwatch.Elapsed);
        }

        static void StartGame()
        {
            StartGameAsync().Wait();
        }

        static async Task StartGameAsync()
        {
            Transaction placeBalls = new Transaction(transactionManager);
            await placeBalls.InitializeAsync();
            for (int i = 0; i < 10; i++)
            {
                await placeBalls.PutAsync($"{i}", (i % 2 == 0) ? CreateBall() : "");
            }
            await placeBalls.CommitAsync();
            Console.WriteLine("Game on");
            Task[] tasks = new Task[NUM_PLAYERS];
            for (int i = 0; i < NUM_PLAYERS; i++)
            {
                tasks[i] = PlayAsync();
            }
            await Task.WhenAll(tasks);

            Transaction checkBalls = new Transaction(transactionManager);
            await checkBalls.InitializeAsync();
            int ballCount = 0;
            for (int i = 0; i < 10; i++)
            {
                GetResult getResult = await checkBalls.GetAsync($"{i}");
                Debug.Assert(getResult.Succeed);
                ballCount += getResult.Content == "" ? 0 : 1;
            }
            await checkBalls.CommitAsync();
            Debug.Assert(ballCount == 5);
            Console.WriteLine("Game over");
            await transactionManager.ShutdownAsync();
        }

        static async Task PlayAsync()
        {
            for (int i = 0; i < 500000; i++)
            {
                Transaction t = new Transaction(transactionManager);
                await t.InitializeAsync();
                int player1 = 0;
                int player2 = 0;
                while (player1 == player2)
                {
                    player1 = random.Next(10);
                    player2 = random.Next(10);
                }
                GetResult player1Result = (await t.GetAsync($"{player1}"));
                GetResult player2Result = (await t.GetAsync($"{player2}"));
                string player1Content = player1Result.Content;
                string player2Content = player2Result.Content;
                if (player1Content != "" && player2Content == "")
                {
                    await t.PutAsync($"{player1}", "");
                    await t.PutAsync($"{player2}", CreateBall());
                    // Console.Write('*');
                    await t.CommitAsync();
                }
                else if (player1Content == "" && player2Content != "")
                {
                    await t.PutAsync($"{player1}", CreateBall());
                    await t.PutAsync($"{player2}", "");
                    // Console.Write('*');
                    await t.CommitAsync();
                }
                else
                {
                    // Console.Write('.');
                    await t.AbortAsync();
                }
            }
        }

        private static string CreateBall()
        {
            return "ball";
            // return new string('b', 40000);
        }
    }
}
