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

            string[] streams = Enumerable.Range(0, streamsCnt).Select(x => Guid.NewGuid().ToString()).ToArray();

            for (var i = 0; i < clientsCnt; i++)
            {
                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
 
                var client = context.Client.CreateEventStoreConnection();
                clients.Add(client);
                threads.Add(WriteFloodWorker.CreateThread(streams, count, countdown, Create, client, size));
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
                UTF8NoBom.GetBytes("{ \"METADATA\" : \"" + new string('$', 100) + "\"}"));
        }

        private class WriteFloodWorker
        {
            private long _sent;
            private long _sentComplete;

            private string stream;
            private long count;
            private int size;
            private CountdownEvent countdown;
            private Func<int, EventData> Create; 
            private IEventStoreConnection _connection;

            private WriteFloodWorker(
                string[] streams, 
                long count, 
                CountdownEvent countdown, 
                Func<int, EventData> create, 
                IEventStoreConnection connection,
                int eventDataSize)
            {
                Random random = new Random(Guid.NewGuid().GetHashCode());
                this.stream = streams[random.Next(streams.Length)];
                this.count = count;
                this.countdown = countdown;
                Create = create;
                _connection = connection;
                size = eventDataSize;
            }

            public static Thread CreateThread(
                string[] streams, 
                long count, 
                CountdownEvent countdown, 
                Func<int, EventData> create, 
                IEventStoreConnection connection,
                int eventDataSize)
            {
                WriteFloodWorker worker = new WriteFloodWorker(streams, count, countdown, create, connection, eventDataSize);
                ThreadStart threadStart = new ThreadStart(worker.Run);
                Thread thread = new Thread(threadStart);
                thread.IsBackground = true;

                return thread;
            }

            private void Run()
            {
                for (var j = 0; j < count; j++)
                {
                    var events = new EventData[] { Create(size) };
                    // AppendToStreamAsync will block if the internal send queue is full
                    var task = _connection.AppendToStreamAsync(stream, ExpectedVersion.Any, events).ContinueWith(AppendToStreamComplete);
                    Interlocked.Increment(ref _sent);
                }

                // wait for all submitted tasks to complete
                while (Interlocked.Read(ref _sentComplete) < _sent)
                {
                    Thread.Sleep(1);
                }

                countdown.Signal();                
            }

            private void AppendToStreamComplete(Task task)
            {
                Statistics.OnWriteEvent();
                Interlocked.Increment(ref _sentComplete);               
            }
        }
    }
}