using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.TestClientAPI.Commands;

namespace EventStore.TestClientAPI
{
    public class Client
    {
        public readonly ClientOptions Options;
        private readonly CommandsProcessor _commands = new CommandsProcessor();


        public Client(ClientOptions options)
        {
            Options = options;
            RegisterProcessors();
        }

        public int Run()
        {
            return Execute(Options.Command);
        }

        private int Execute(string[] args)
        {
            int exitCode;
            CommandProcessorContext context = new CommandProcessorContext(this,new ManualResetEventSlim());

            if (_commands.TryProcess(context, args, out exitCode))
            {
                return exitCode;
            }

            return 1;
        }

        private void RegisterProcessors()
        {
            _commands.Register(new WriteFloodProcessor());
        }

        public IEventStoreConnection CreateEventStoreConnection()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, Options.TcpPort);
            var connectionStringBuilder = ConnectionSettings
                .Create()
                //.UseConsoleLogger()
                //.EnableVerboseLogging()
                //.LimitRetriesForOperationTo(1)
                .SetDefaultUserCredentials(new UserCredentials(Options.Username, Options.Password));

            IEventStoreConnection connection = EventStoreConnection.Create(connectionStringBuilder, endPoint);

            using (Statistics.OnStartConnect())
            {
                if (!connection.ConnectAsync().Wait(Options.ConnectTimeout))
                {
                    throw new TimeoutException(string.Format("Took longer than {0} connect to Event Store.", Options.ConnectTimeout));
                }
            }

            return connection;
        }

    }
}