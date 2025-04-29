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
                Console.WriteLine("Starting service in DEBUG/Interactive mode...");

                // Attempt to launch the debugger if not already attached
                if (!Debugger.IsAttached)
                {
                    Debugger.Launch(); // Prompts to attach debugger
                    Console.WriteLine("Debugger launched. Please attach the debugger.");
                }

                var service = new WindowsServiceSap();
                try
                {
                    Console.WriteLine("Starting service...");
                    service.StartRelayServiceLogic();
                    Console.WriteLine("Service logic started successfully.");
                    Console.WriteLine("Press Enter to stop...");
                    Console.ReadLine();
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
                // Run as a Windows Service (non-interactive mode)
                ServiceBase[] ServicesToRun = { new WindowsServiceSap() };
                ServiceBase.Run(ServicesToRun);
            }
        }

        private static void WriteLog(string message)
        {
            // Log messages for debugging purposes
            Debug.WriteLine($"{logSource}: {message}");
            // You can also write to a log file or event log here if needed
        }
    }
}
