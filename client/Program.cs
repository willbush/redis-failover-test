using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace client
{
    class Program
    {
        static volatile bool _isRunning = true;

        static readonly ConfigurationOptions _config = new ConfigurationOptions
        {
            EndPoints =
            {
                { "localhost", 7000  },
                { "localhost", 7001  },
            },
            ReconnectRetryPolicy = new LinearRetry(2000)
        };

        static void Main()
        {
            Console.WriteLine("Press Enter to Stop!\n");

            var testerTask = Task.Factory.StartNew(() =>
                Retry(() => RunRedisTester()), TaskCreationOptions.LongRunning);

            Console.ReadLine();
            _isRunning = false;
            testerTask.Wait();
        }

        static void RunRedisTester()
        {
            using (var redis = ConnectionMultiplexer.Connect(_config))
            {
                var db = redis.GetDatabase();
                while (_isRunning)
                {
                    var id = Guid.NewGuid().ToString();
                    db.StringSet(id, id);
                    var endPoint = db.IdentifyEndpoint(id, CommandFlags.PreferMaster);

                    Console.WriteLine($"{db.StringGet(id)} - {endPoint}");

                    Thread.Sleep(100);
                }
            }
        }

        static void Retry(Action action, int maxAttemptCount = 10)
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
        }
    }
}
