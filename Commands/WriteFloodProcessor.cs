using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;

namespace EventStore.TestClientAPI.Commands
{
    public class WriteFloodProcessor : ICmdProcessor
    {
        private static readonly UTF8Encoding UTF8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            var clientsCnt = 1;
            long requestsCnt = 5000;
            var streamsCnt = 1000;
            var size = 256;

            if (args.Length > 0)
            {
                if (args.Length < 2 || args.Length > 4)
                    return false;

                try
                {
                    clientsCnt = int.Parse(args[0]);
                    requestsCnt = long.Parse(args[1]);
                    if (args.Length >= 3)
                        streamsCnt = int.Parse(args[2]);
                    if (args.Length >= 4)
                        size = int.Parse(args[3]);
                }
                catch
                {
                    return false;
                }
            }

            WriteFlood(context, clientsCnt, requestsCnt, streamsCnt, size);
            return true;
        }

        public string Usage { get { return "WRFL [<clients> <requests> [<streams-cnt> [<size>]]]"; } }
        public string Keyword { get { return "WRFL"; } }


        private void WriteFlood(CommandProcessorContext context, int clientsCnt, long requestsCnt, int streamsCnt, int size)
        {
            var clients = new List<IEventStoreConnection>();
            var threads = new List<Thread>();
            CountdownEvent countdown = new CountdownEvent(clientsCnt);

            var streams = Enumerable.Range(0, streamsCnt).Select(x => Guid.NewGuid().ToString()).ToArray();

            for (var i = 0; i < clientsCnt; i++)
            {
                long sent = 0;
                long received = 0;

                ManualResetEvent appendComplete = new ManualResetEvent(false);

                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
                Random random = new Random();

                var client = context.Client.CreateEventStoreConnection();
                clients.Add(client);

                threads.Add(new Thread(() =>
                {
                    var stream = streams[random.Next(streamsCnt)];
                    List<Task> tasks = new List<Task>(client.Settings.MaxConcurrentItems);

                    for (var j = 0; j < count; j++)
                    {
                        var events = new EventData[] {Create(size)};
                        var task = client.AppendToStreamAsync(stream, ExpectedVersion.Any, events);
                        tasks.Add(task);

                        var localSent = Interlocked.Increment(ref sent);
                        while (localSent - Interlocked.Read(ref received) > client.Settings.MaxConcurrentItems)
                        {
                            var taskArray = tasks.ToArray();
                            var index = Task.WaitAny(taskArray);
                            Statistics.OnWriteEvent();
                            Interlocked.Increment(ref received);
                            tasks.RemoveAt(index);
                        }
                    }

                    while (tasks.Count > 0)
                    {
                        var taskArray = tasks.ToArray();
                        var index = Task.WaitAny(taskArray);
                        Statistics.OnWriteEvent();
                        tasks.RemoveAt(index);
                    }

                    countdown.Signal();
                }) { IsBackground = true });
            }

            threads.ForEach(thread => thread.Start());

            countdown.Wait();

            clients.ForEach(client => client.Close());

            context.Success();
        }

        private EventData Create(int size)
        {
            return new EventData(Guid.NewGuid(), "TakeSomeSpaceEvent", true,
                UTF8NoBom.GetBytes("{ \"DATA\" : \"" + new string('*', size) + "\"}"),
                UTF8NoBom.GetBytes("{ \"METADATA\" : \"" + new string('$', 100) + "\", \"CommitId\": \"" + Guid.NewGuid().ToString("n") + "\"}"));
        }
    }
}