using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsServiceSap
{
    public class Logger
    {
        private readonly string logPath;

        public Logger(string logDirectory = null)
        {
            // Set the log path to the provided directory or the default application directory
            logPath = logDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

            // Ensure the directory exists
            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }
        }

        public async Task WriteLogAsync(string message)
        {
            string logFile = Path.Combine(logPath, "ServiceLog.txt");

            try
            {
                using (StreamWriter writer = new StreamWriter(logFile, true))
                {
                    await writer.WriteLineAsync($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
                }
            }
            catch (Exception ex)
            {
                // Handle logging exceptions (e.g., fallback to Debug output)
                Debug.WriteLine($"Error writing to log: {ex.Message}");
            }
        }
    }
}
