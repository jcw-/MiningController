using MiningController.Properties;
using MiningController.ViewModel;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace MiningController
{
    /// <summary>
    /// Justification: I'm being lazy and borrowing this class as a view model for a handful of things that easier to accomplish
    /// with a window reference handy.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnInitialized(EventArgs ea)
        {
            base.OnInitialized(ea);

            this.TrayGraph.Fill = new VisualBrush(this.MyChart) { Stretch = Stretch.Uniform };

            var cm = this.BuildTrayIconContextMenu();
            this.TrayIcon.ContextMenu = cm;
        }

        private ContextMenu BuildTrayIconContextMenu()
        {
            var cm = new ContextMenu();            

            MenuItem mi;

            mi = new MenuItem() { Header = "Show Mining Controller", FontWeight = FontWeights.Bold };
            mi.Click += (s, e) => { this.ShowSelf(); };
            cm.Items.Add(mi);

            cm.Items.Add(new Separator());

            mi = new MenuItem() { Header = "Snooze" };
            mi.SetBinding(MenuItem.CommandProperty, new Binding("SnoozeCommand") { Source = this.DataContext });
            cm.Items.Add(mi);

            mi = new MenuItem() { Header = "Miner Visibility", IsCheckable = true };
            mi.SetBinding(MenuItem.CommandProperty, new Binding("ToggleMinerCommand") { Source = this.DataContext });
            mi.SetBinding(MenuItem.IsCheckedProperty, new Binding("ShowMiner") { Source = this.DataContext, Mode = BindingMode.OneWay });
            cm.Items.Add(mi);

            cm.Items.Add(new Separator());

            mi = new MenuItem() { Header = "Exit" };
            mi.Click += (s, e) => { this.Close(); };
            cm.Items.Add(mi);

            return cm;
        }

        private void ShowSelf()
        {
            this.Show();
            this.Activate();
        }

        private void HideSelf()
        {
            this.Hide();
            if (Settings.Default.FirstTimeHide)
            {
                Settings.Default.FirstTimeHide = false;
                Settings.Default.Save();

                //var icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri(@"pack://application:,,,/MiningController;component/images/cryptocoin.ico")).Stream);
                this.TrayIcon.ShowBalloonTip("Mining Controller", "The application is still running, double-click on the icon to bring it back into view.", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
            }
        }

        private RelayCommand showSelfCommand;

        public ICommand ShowSelfCommand
        {
            get
            {
                if (this.showSelfCommand == null)
                {
                    this.showSelfCommand = new RelayCommand(item => this.ShowSelf());
                }

                return this.showSelfCommand;
            }
        }

        private RelayCommand hideSelfCommand;

        public ICommand HideSelfCommand
        {
            get
            {
                if (this.hideSelfCommand == null)
                {
                    this.hideSelfCommand = new RelayCommand(item => this.HideSelf());
                }

                return this.hideSelfCommand;
            }
        }

        private RelayCommand exitCommand;

        public ICommand ExitCommand
        {
            get
            {
                if (this.exitCommand == null)
                {
                    this.exitCommand = new RelayCommand(item => this.Close());
                }

                return this.exitCommand;
            }
        }

        private RelayCommand aboutCommand;

        public ICommand AboutCommand
        {
            get
            {
                if (this.aboutCommand == null)
                {
                    this.aboutCommand = new RelayCommand(item => this.ShowAbout());
                }

                return this.aboutCommand;
            }
        }

        private void ShowAbout()
        {
            var dlg = new About()
            {
                Owner = this
            };

            dlg.ShowDialog();
        }

        private RelayCommand settingsCommand;

        public ICommand SettingsCommand
        {
            get
            {
                if (this.settingsCommand == null)
                {
                    this.settingsCommand = new RelayCommand(item => this.LaunchSettings());
                }

                return this.settingsCommand;
            }
        }

        private void LaunchSettings()
        {
            // the .config extension is not likely registered with a text editor, so explicitly using notepad - however, this
            // same file can be manually opened in another editor of the users choice of course
            var appConfig = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            if (System.IO.File.Exists(appConfig))
            {
                try
                {
                    System.Diagnostics.Process.Start(@"notepad.exe", appConfig);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, string.Format("An error occurred while attempting to open a file [{0}] in notepad: {1}", appConfig, ex.Message), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show(this, string.Format("Unable to locate the configuration file at the expected location [{0}].", appConfig), "File Not Found", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
