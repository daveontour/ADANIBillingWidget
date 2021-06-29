using System;
using Topshelf;

namespace AMSWidgetBase {

    internal class Program {

        private static void Main(string[] args) {
            var exitCode = HostFactory.Run(x => {
                string test = null;

                x.AddCommandLineDefinition("test", f => { test = f; });

                x.Service<AMSWidgetBase>(s => {
                    s.ConstructUsing(core => new AMSWidgetBase(test));
                    s.WhenStarted(core => core.Start());
                    s.WhenStopped(core => core.Stop());
                });

                x.RunAsLocalSystem();
                x.StartAutomatically();
                x.EnableServiceRecovery(rc => {
                    rc.RestartService(1); // restart the service after 1 minute
                });

                x.SetServiceName($"{Parameters.APPSERVICENAME}  {Parameters.VERSION}");
                x.SetDisplayName(Parameters.APPDISPLAYNAME);
                x.SetDescription(Parameters.APPDESCRIPTION);
            });

            int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
            Environment.ExitCode = exitCodeValue;
        }
    }
}