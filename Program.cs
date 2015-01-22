using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using Metrics;
using System.Net;
using EventStore.TestClientAPI.Commands;

namespace EventStore.TestClientAPI
{
    class Program
    {
        static void Main(string[] args)
        {
            int exitCode = 0;
            TimeSpan metricsInterval = TimeSpan.FromSeconds(1);
            InitializeMetrics(metricsInterval, "WriteFlood");
            
            try
            {
                var client = new Client(new ClientOptions(args));
                exitCode = client.Run();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Oops: {0}", exception);
            }

            WriteMetrics();

            // wait for the metrics to be reported one last time
            Thread.Sleep(metricsInterval);
            Environment.Exit(exitCode);
        }

        private static void InitializeMetrics(TimeSpan interval, string filenamePrefix)
        {
            string filename = string.Format("{0}-metrics", filenamePrefix);
            Metric.Config
                .WithSystemCounters()
                .WithAppCounters()
                .WithReporting(report => report.WithCSVReports(Path.Combine(@"C:\temp\reports", filename), interval));
        }

        private static void WriteMetrics()
        {
            var data = Metric.Context(null).DataProvider.CurrentMetricsData;
            foreach (var meter in data.Meters)
            {
                Console.WriteLine("{0} : {1:0.00} {2}/{3}", meter.Name, meter.Value.MeanRate, meter.Unit, meter.RateUnit);
            }

            foreach (var timer in data.Timers)
            {
                Console.WriteLine("{0} : {1:0.00000} {2}", timer.Name, timer.Value.Rate.MeanRate, timer.DurationUnit);
            }
        }
    }

    public class CommandsProcessor
    {
        private readonly IDictionary<string, ICmdProcessor> _processors = new Dictionary<string, ICmdProcessor>();
        private ICmdProcessor _regCommandsProcessor;

        public void Register(ICmdProcessor processor, bool usageProcessor = false)
        {
            var cmd = processor.Keyword.ToUpper();

            if (_processors.ContainsKey(cmd))
                throw new InvalidOperationException(
                    string.Format("The processor for command '{0}' is already registered.", cmd));

            _processors[cmd] = processor;

            if (usageProcessor)
                _regCommandsProcessor = processor;
        }

        public bool TryProcess(CommandProcessorContext context, string[] args, out int exitCode)
        {
            var commandName = args[0].ToUpper();
            var commandArgs = args.Skip(1).ToArray();

            ICmdProcessor commandProcessor;
            if (!_processors.TryGetValue(commandName, out commandProcessor))
            {
                //_log.Info("Unknown command: {0}.", commandName);
                Console.Error.WriteLine("Unknown command: {0}.", commandName);
                if (_regCommandsProcessor != null)
                    _regCommandsProcessor.Execute(context, new string[0]);
                exitCode = 1;
                return false;
            }

            int exitC = 0;
            var executedEvent = new AutoResetEvent(false);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var syntaxOk = commandProcessor.Execute(context, commandArgs);
                    if (syntaxOk)
                    {
                        exitC = context.ExitCode;
                    }
                    else
                    {
                        exitC = 1;
                        //_log.Info("Usage of {0}:{1}{2}", commandName, Environment.NewLine, commandProcessor.Usage);
                        Console.Error.WriteLine("Usage of {0}:{1}{2}", commandName, Environment.NewLine, commandProcessor.Usage);
                    }
                    executedEvent.Set();
                }
                catch (Exception exc)
                {
                    //_log.ErrorException(exc, "Error while executing command {0}.", commandName);
                    Console.Error.WriteLine("Error while executing command {0}. {1}", commandName, exc);
                    exitC = -1;
                    executedEvent.Set();
                }
            });

            executedEvent.WaitOne(1000);
            context.WaitForCompletion();

            exitCode = exitC == 0 ? context.ExitCode : exitC;
            return true;
        }
    }

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
                //Log.Info("Command exited with code {0}.", exitCode);
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

    /// <summary>
    /// Data contract for the command-line options accepted by test client.
    /// This contract is handled by CommandLine project for .NET
    /// </summary>
    public sealed class ClientOptions //: IOptions
    {
        //[ArgDescription(Opts.ShowHelpDescr)]
        public bool Help { get; set; }
        //[ArgDescription(Opts.ShowVersionDescr)]
        public bool Version { get; set; }
        //[ArgDescription(Opts.LogsDescr)]
        public string Log { get; set; }
        //[ArgDescription(Opts.ConfigsDescr)]
        public string Config { get; set; }
        //[ArgDescription(Opts.DefinesDescr)]
        public string[] Defines { get; set; }
        //[ArgDescription(Opts.WhatIfDescr, Opts.AppGroup)]
        public bool WhatIf { get; set; }

        //[ArgDescription(Opts.IpDescr)]
        public IPAddress Ip { get; set; }
        //[ArgDescription(Opts.TcpPortDescr)]
        public int TcpPort { get; set; }
        //[ArgDescription(Opts.HttpPortDescr)]
        public int HttpPort { get; set; }
        public int Timeout { get; set; }
        public int ReadWindow { get; set; }
        public int WriteWindow { get; set; }
        public int PingWindow { get; set; }
        //[ArgDescription(Opts.ForceDescr)]
        public bool Force { get; set; }
        public string[] Command { get; set; }

        public string Username { get; set; }
        public string Password { get; set; }

        public TimeSpan ConnectTimeout { get; set; }

        public ClientOptions()
        {
            Config = "";
            Command = new string[] { };
            //Help = Opts.ShowHelpDefault;
            //Version = Opts.ShowVersionDefault;
            //Log = Opts.LogsDefault;
            //Defines = Opts.DefinesDefault;
            //WhatIf = Opts.WhatIfDefault;
            Ip = IPAddress.Loopback;
            TcpPort = 1113;
            HttpPort = 2113;
            Timeout = -1;
            ReadWindow = 2000;
            WriteWindow = 2000;
            PingWindow = 2000;
            Force = false;

            Username = "admin";
            Password = "changeit";
            ConnectTimeout = TimeSpan.FromSeconds(2);
        }

        public ClientOptions(params string[] args) : this()
        {
            Command = args;
        }
    }


    public static class Statistics
    {
        private const string MetricPrefix = "EventStore.";
        private static readonly Meter EventsRead = Metric.Meter(MetricPrefix + "Events.Read", Unit.Events);
        private static readonly Meter EventsWritten = Metric.Meter(MetricPrefix + "Events.Write", Unit.Events);
        private static readonly Metrics.Timer ConnectTime = Metric.Timer(MetricPrefix + "Connect.Time", Unit.Events);

        public static void OnWriteEvent()
        {
            EventsWritten.Mark(1);
        }
        public static void OnWriteEvent(int count)
        {
            EventsWritten.Mark(count);
        }

        public static IDisposable OnStartConnect()
        {
            return ConnectTime.NewContext();
        }
    }
}
