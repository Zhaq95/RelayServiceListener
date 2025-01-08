using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace WindowsServiceSap
{
    public partial class WindowsServiceSap : ServiceBase
    {
        private RelayService relayService;
        private CancellationTokenSource cancellationTokenSource;
        private readonly string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private Logger logger;

        public WindowsServiceSap()
        {
            InitializeComponent();
            relayService = new RelayService();
            cancellationTokenSource = new CancellationTokenSource();
            logger = new Logger();
        }

        protected override void OnStart(string[] args)
        {
            logger.WriteLogAsync("Service started at: " + DateTime.Now);
            StartRelayServiceLogic();
        }

        protected override void OnStop()
        {
            logger.WriteLogAsync("Service stopped at: " + DateTime.Now);
            StopRelayServiceLogic();
        }

        public void StartRelayServiceLogic()
        {
            Task.Run(() => StartRelayServiceAsync(cancellationTokenSource.Token));
        }

        public void StopRelayServiceLogic()
        {
            cancellationTokenSource.Cancel();
            relayService?.Dispose();
        }

        private async Task StartRelayServiceAsync(CancellationToken cancellationToken)
        {
            try
            {
                await relayService.ConnectRelay(cancellationToken);
            }
            catch (Exception ex)
            {

                await logger.WriteLogAsync($"Error starting RelayService: {ex.Message}");
            }
        }

        //private async Task  WriteLogAsync(string message)
        //{
        //    //string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
        //    if (!Directory.Exists(logPath))
        //    {
        //        Directory.CreateDirectory(logPath);
        //    }

        //    string logFile = Path.Combine(logPath, "ServiceLog.txt");
        //    try
        //    {
        //        using (StreamWriter writer = new StreamWriter(logFile, true))
        //        {
        //            await writer.WriteLineAsync(message);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        // Handle logging exceptions here (e.g., with a secondary log mechanism)
        //        Debug.WriteLine($"Error writing to log: {ex.Message}");
        //    }
        //}
    }

}
