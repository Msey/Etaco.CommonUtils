using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace ETACO.CommonUtils.WinService
{
    [RunInstaller(true)]
    public class WinServiceInstaller : Installer
    {
        public WinServiceInstaller()
        {
            BeforeInstall += (s, e) => Context.Parameters["assemblyPath"] = "\"{0}\" \"-cfg:file={1}\"".FormatStr(Context.Parameters["assemblyPath"], AppContext.Config.ConfigFileName);
            var spi = new ServiceProcessInstaller();
            var si = new ServiceInstaller();

            var config = AppContext.Config;

            spi.Account = config.GetParameter("serviceinstaller", "account", ServiceAccount.LocalSystem);
            spi.Password = config["serviceinstaller", "password", null];
            spi.Username = config["serviceinstaller", "username", null];

            si.ServiceName = config["serviceinstaller", "name", "EtacoService"];
            si.Description = config["serviceinstaller", "description", si.ServiceName];
            si.DisplayName = si.ServiceName;
            si.StartType = config.GetParameter("serviceinstaller", "startmode", ServiceStartMode.Automatic);

            Installers.AddRange(new System.Configuration.Install.Installer[] { spi, si });
        }

        public override void Uninstall(IDictionary savedState)
        {
            var serviceName = AppContext.Config["serviceinstaller", "name", "EtacoService"];
            var sc = new ServiceController(serviceName);
            if (sc.CanStop)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped);
            }
            sc.Close();
            base.Uninstall(savedState);
        }
    }
}