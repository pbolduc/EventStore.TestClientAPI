using System;
using System.IO;
using System.Threading;
using Metrics;

namespace EventStore.TestClientAPI
{
    class Program
    {
        static void Main(string[] args)
        {
            int exitCode = 0;
            TimeSpan metricsInterval = TimeSpan.FromSeconds(10);
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

            if (exitCode == 0)
            {
                PrintMetrics();

                // wait for the metrics to be reported one last time
                Console.Write("waiting {0} for final metrics to be written...", metricsInterval);
                Thread.Sleep(metricsInterval);
            }
            Environment.Exit(exitCode);
        }

        private static void InitializeMetrics(TimeSpan interval, string filenamePrefix)
        {
            string directory = string.Format("{0}-metrics-{1}", filenamePrefix, DateTime.Now.ToString("yyyy-MM-dd_HH-mm"));
            string path = Path.GetTempPath();
            string filename = Path.Combine(path, directory);

            Console.WriteLine("Performance metrics will be written every {1} to {0}", filename, interval);

            Metric.Config
                .WithSystemCounters()
                .WithAppCounters()
                .WithReporting(report => report.WithCSVReports(filename, interval));
        }

        private static void PrintMetrics()
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
}
