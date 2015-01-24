using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EventStore.TestClientAPI
{
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
}