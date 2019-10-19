using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace client
{
    class Program
    {
        static volatile bool _isRunning = true;

        static void Main()
        {
            Console.WriteLine("Press Enter to Stop!\n");

            var testConnectionString = "localhost:7000,localhost:7001";
            var config = ConfigurationOptions.Parse(testConnectionString);
            config.AbortOnConnectFail = false;

            using var connection = ConnectionMultiplexer.Connect(config);

            connection.ConnectionFailed += (s, e) =>
                PrintEvent(nameof(IConnectionMultiplexer.ConnectionFailed));
            connection.ConnectionRestored += (s, e) =>
                PrintEvent(nameof(IConnectionMultiplexer.ConnectionRestored));
            connection.InternalError += (s, e) =>
                PrintEvent(nameof(IConnectionMultiplexer.InternalError));
            connection.ErrorMessage += (s, e) =>
            {
                PrintEvent(nameof(IConnectionMultiplexer.ErrorMessage));
                Console.WriteLine(e.Message);
            };

            connection.ConnectionRestored += (s, e) =>
                PrintEvent(nameof(IConnectionMultiplexer.ConnectionRestored));

            connection.ConfigurationChanged += (s, e) =>
                PrintEvent(nameof(IConnectionMultiplexer.ConfigurationChanged));

            connection.ConfigurationChangedBroadcast += (s, e) =>
                PrintEvent(nameof(IConnectionMultiplexer.ConfigurationChangedBroadcast));

            var testerTask = Task.Factory.StartNew(() =>
               Retry(() => RunRedisTester(connection)), TaskCreationOptions.LongRunning);

            Console.ReadLine();
            _isRunning = false;

            Console.WriteLine("Gracefully stopping...");
            testerTask.Wait();
        }

        static void RunRedisTester(ConnectionMultiplexer redisConnection)
        {
            var db = redisConnection.GetDatabase();

            while (_isRunning)
            {
                var id = Guid.NewGuid().ToString();

                db.StringSet(id, id);

                var endPoint = db.IdentifyEndpoint(id);

                Console.WriteLine($"{db.StringGet(id)} - {endPoint}");

                Thread.Sleep(100);
            }
        }

        static void Retry(Action action, int maxAttemptCount = 100)
        {
            for (var attempted = 0; attempted < maxAttemptCount; attempted++)
            {
                try
                {
                    if (attempted > 0)
                        Thread.Sleep(TimeSpan.FromSeconds(4));

                    action();
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            Console.WriteLine("Exceeded max retry count!");
        }

        static void PrintEvent(string eventName)
        {
            Console.WriteLine("**************************");
            Console.WriteLine($"{eventName} event");
            Console.WriteLine("**************************");
        }
    }
}
