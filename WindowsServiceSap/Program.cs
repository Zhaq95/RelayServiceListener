using System;
using System.ServiceProcess;
using System.Diagnostics;

namespace WindowsServiceSap
{
    internal static class Program
    {
        private static readonly string logSource = "WindowsServiceSap";
        static void Main()
        {
            if (Environment.UserInteractive)
            {
                // Interactive mode: Run as a console application
                Console.WriteLine("Starting service in DEBUG/Interactive mode...");
                var service = new WindowsServiceSap();
                try
                {
                    service.StartRelayServiceLogic(); // Start the service logic interactively
                    Console.WriteLine("Press Enter to stop...");
                    Console.ReadLine(); // Keep the console running
                }
                catch (Exception ex)
                {
                    WriteLog($"Error running service in interactive mode: {ex.Message}");
                }
                finally
                {
                    service.StopRelayServiceLogic(); // Stop the service logic on exit
                }
            }
            else
            {
                // Non-interactive mode: Run as a Windows Service
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new WindowsServiceSap()
                };
                ServiceBase.Run(ServicesToRun);
            }

        }
        private static void WriteLog(string message)
        {
            Debug.WriteLine($"{logSource}:{message}");
            // You can implement additional logging functionality here (e.g., writing to a log file)
        }
    }
}

