using Microsoft.Shell;
using System;
using System.Collections.Generic;
using System.Windows;

namespace MiningController
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ISingleInstanceApp
    {
        private const string UniqueAppToken = "530c4d164eae35abe30181da";

        [STAThread]
        public static void Main()
        {
            if (SingleInstance<App>.InitializeAsFirstInstance(UniqueAppToken))
            {
                var application = new App();

                application.InitializeComponent();
                application.Run();

                // allow single instance code to perform cleanup operations
                SingleInstance<App>.Cleanup();
            }
        }

        public bool SignalExternalCommandLineArgs(IList<string> args)
        {
            // there aren't any command line arguments needed for this app, so just ensure the main window is visible and return true
            MainWindow.Show();
            MainWindow.Activate();

            return true;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

#if !DEBUG
            if (MiningController.Properties.Settings.Default.AutomaticErrorReporting)
            {
                BugFreak.BugFreak.Hook("93a34c94-0705-4b5f-a09f-9741c1a1b1cb", UniqueAppToken, this);
                BugFreak.GlobalConfig.ErrorDataProviders.Add(new MiningControllerErrorDataProvider());
            }
#endif
        }
    }
}
