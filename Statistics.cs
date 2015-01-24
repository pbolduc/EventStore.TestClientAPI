using System;
using Metrics;

namespace EventStore.TestClientAPI
{
    public static class Statistics
    {
        private const string MetricPrefix = "EventStore.";
        private static readonly Meter EventsWritten = Metric.Meter(MetricPrefix + "Events.Write", Unit.Events);
        private static readonly Timer ConnectTime = Metric.Timer(MetricPrefix + "Connect.Time", Unit.Events);

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