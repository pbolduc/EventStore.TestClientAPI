using System;
using System.Threading;
using EventStore.ClientAPI;

namespace EventStore.TestClientAPI
{
    public class CommandProcessorContext
    {
        public int ExitCode;
        public Exception Error;
        public string Reason;

        //public readonly ILogger Log;
        public readonly Client Client;

        private readonly ManualResetEventSlim _doneEvent;
        private int _completed;

        public CommandProcessorContext(Client client, ManualResetEventSlim doneEvent)
        {
            Client = client;
            _doneEvent = doneEvent;
        }

        public void Completed(int exitCode = (int)0, Exception error = null, string reason = null)
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
            {
                ExitCode = exitCode;

                Error = error;
                Reason = reason;

                _doneEvent.Set();
            }
        }

        public void Fail(Exception exc = null, string reason = null)
        {
            Completed((int)1, exc, reason);
        }

        public void Success()
        {
            Completed();
        }

        public void IsAsync()
        {
            _doneEvent.Reset();
        }

        public void WaitForCompletion()
        {
            if (Client.Options.Timeout < 0)
                _doneEvent.Wait();
            else
            {
                if (!_doneEvent.Wait(Client.Options.Timeout * 1000))
                    throw new TimeoutException("Command didn't finished within timeout.");
            }
        }

    }
}