using System.ComponentModel;
using System.ServiceProcess;

namespace WindowsServiceSap
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();

            // Define the ServiceProcessInstaller
            ServiceProcessInstaller processInstaller = new ServiceProcessInstaller();
            processInstaller.Account = ServiceAccount.LocalSystem; // Runs as LocalSystem

            // Define the ServiceInstaller
            ServiceInstaller serviceInstaller = new ServiceInstaller();
            serviceInstaller.StartType = ServiceStartMode.Automatic; // Auto-start
            serviceInstaller.ServiceName = "OCIR"; // Service name

            // Add the installers to the Installers collection
            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
