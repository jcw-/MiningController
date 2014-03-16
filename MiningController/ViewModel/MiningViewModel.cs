using MiningController.Mining;
using MiningController.Model;
using MiningController.Properties;
using Octokit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;

namespace MiningController.ViewModel
{
    public enum UserActivity
    {
        Unknown,
        Idle, // no user activity detected recently
        SnoozeRequested, // user has requested a temporary pause in mining activity (e.g. to play a game)
        Active // recent user activity detected
    }

    /// <summary>
    /// Schedule this app for when your user logs in to the computer: http://www.techrepublic.com/blog/windows-and-office/run-uac-restricted-programs-without-the-uac-prompt/
    /// </summary>
    public class MiningViewModel : ViewModelBase
    {
        private const double InitialWindowWidth = 500;
        
        private const string Title = "Mining Controller";

        private const string GitHubOwner = "jcw-";

        private const string GitHubRepo = "MiningController";

        private IController controller;

        private IIdleTimeProvider idleTimeProvider;

        private ISummaryDataManager summaryDataManager;

        private IWindowController windowController;

        private DispatcherTimer idleMonitoringTimer;

        private DispatcherTimer processCheckTimer;        

        // this constructor is called from XAML - use fixed dependencies
        public MiningViewModel()
        {
            // enforce a minimum of ten seconds for the polling period
            var pollingPeriod = TimeSpan.FromSeconds(Math.Max(TimeSpan.FromSeconds(10).TotalSeconds, Settings.Default.MinerWatchdogPollingPeriod.TotalSeconds));
            if (pollingPeriod != Settings.Default.MinerWatchdogPollingPeriod)
            {
                AddMessage(string.Format("Setting MinerWatchdogPollingPeriod to minimum polling period [{0}].", pollingPeriod));
            }

            IController controller = null;
            ISummaryDataManager dataManager = null;
            if (!IsDesignMode)
            {
                var minerComms = new MinerCommunication(Settings.Default.MinerProcessName, Settings.Default.LaunchCommand, Settings.Default.ImportantProcessNames.Cast<string>());
                controller = new Controller(minerComms, pollingPeriod);
                dataManager = new SummaryDataManager(Settings.Default.SaveInterval);
            }

            var idleTimeProvider = new IdleTimeProvider();
            var windowController = new WindowController();

            var conn = new Connection(new ProductHeaderValue(Title.Replace(" ", string.Empty)));
            var api = new ApiConnection(conn);
            var releasesClient = new ReleasesClient(api);

            var versionService = new VersionService(releasesClient, GitHubOwner, GitHubRepo);

            this.InitializeDesignTime(controller, idleTimeProvider, dataManager, windowController, versionService);            
        }

        public MiningViewModel(IController controller, IIdleTimeProvider idleTimeProvider, ISummaryDataManager summaryDataManager, IWindowController windowController, IVersionService versionService)
        {
            this.InitializeDesignTime(controller, idleTimeProvider, summaryDataManager, windowController, versionService);
        }
        
        private void InitializeDesignTime(IController controller, IIdleTimeProvider idleTimeProvider, ISummaryDataManager summaryDataManager, IWindowController windowController, IVersionService versionService)
        {
            this.ApplicationTitle = Title;
            this.idleTimeProvider = idleTimeProvider;
            this.summaryDataManager = summaryDataManager;
            this.summaryDataManager.Message += (s, e) => { AddMessage(e.Message); };
            this.windowController = windowController;

            this.Messages = new ObservableCollection<string>();
            this.DataPointsHashRate = new ObservableCollection<DataPoint>();
            this.GraphTimeSpans = new ObservableCollection<GraphTimeSpan>(LoadGraphTimeSpans());            

            var durations = new List<TimeSpan>();
            foreach (string durationText in Settings.Default.SnoozeDurations)
            {
                TimeSpan duration;
                if (TimeSpan.TryParse(durationText, out duration))
                {
                    durations.Add(duration);
                    if (duration == Settings.Default.DefaultSnoozeDuration)
                    {
                        this.SnoozeDuration = duration;
                    }
                }
            }

            this.SnoozeDurations = durations;

            if (!IsDesignMode)
            {   
                this.InitializeRunTime(controller, versionService);
            }

            this.Activity = UserActivity.Active; // initial setting
        }

        private IEnumerable<GraphTimeSpan> LoadGraphTimeSpans()
        {
            return new GraphTimeSpan[]
            { 
                new GraphTimeSpan() { Label = "1h", Span = TimeSpan.FromHours(1) },
                new GraphTimeSpan() { Label = "12h", Span = TimeSpan.FromHours(12) },
                new GraphTimeSpan() { Label = "1d", Span = TimeSpan.FromDays(1) },
                new GraphTimeSpan() { Label = "7d", Span = TimeSpan.FromDays(7) }
            };
        }

        private void InitializeRunTime(IController controller, IVersionService versionService)
        {
            try
            {
                this.SelectedGraphTimeSpan = this.GraphTimeSpans.FirstOrDefault();

                this.idleMonitoringTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                this.idleMonitoringTimer.Tick += (sender, args) =>
                {
                    this.IdleTime = this.idleTimeProvider.IdleTime;
                };

                this.controller = controller;
                this.controller.Connected += (s, e) => { this.IsConnected = true; };
                this.controller.Disconnected += (s, e) => { this.IsConnected = false; };
                this.controller.Message += (s, e) => { AddMessage(e.Message); };
                this.controller.Summary += (s, e) => { UpdateSummaryData(e.Summary); };

                this.IsImportantProcessDetected = this.controller.ImportantProcessDetected;
                this.processCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                this.processCheckTimer.Tick += (s, e) => this.IsImportantProcessDetected = this.controller.ImportantProcessDetected;
                this.processCheckTimer.Start();
                
                this.ConfigureDesiredMinerStatus(MinerProcessStatus.Running);
#if !DEBUG
                versionService.IsUpdateAvailableAsync(available => this.IsUpdateAvailable = available);
#endif
            }
            catch (Exception e)
            {
                AddMessage(e.Message);
#if DEBUG
                throw;
#endif
            }
        }

        private RelayCommand snoozeCommand;

        public ICommand SnoozeCommand
        {
            get
            {
                if (this.snoozeCommand == null)
                {
                    this.snoozeCommand = new RelayCommand(item => Snooze(this.SnoozeDuration));
                }

                return this.snoozeCommand;
            }
        }

        private RelayCommand clearCommand;

        public ICommand ClearCommand
        {
            get
            {
                if (this.clearCommand == null)
                {
                    this.clearCommand = new RelayCommand(item => this.Messages.Clear());
                }

                return this.clearCommand;
            }
        }

        private RelayCommand copyCommand;

        public ICommand CopyCommand
        {
            get
            {
                if (this.copyCommand == null)
                {
                    this.copyCommand = new RelayCommand(item => 
                        {
                            Action copy = () => { System.Windows.Clipboard.SetData(System.Windows.DataFormats.Text, string.Join("\r\n", this.Messages)); };
                            try
                            {
                                copy();
                            }
                            catch (System.Runtime.InteropServices.COMException)
                            {
                                // try once more...
                                copy();
                            }
                        }
                    );
                }

                return this.copyCommand;
            }
        }

        private RelayCommand resumeCommand;

        public ICommand ResumeCommand
        {
            get
            {
                if (this.resumeCommand == null)
                {
                    this.resumeCommand = new RelayCommand(item => this.SnoozeDurationRemaining = TimeSpan.Zero);
                }

                return this.resumeCommand;
            }
        }

        private RelayCommand toggleGraphCommand;

        public ICommand ToggleGraphCommand
        {
            get
            {
                if (this.toggleGraphCommand == null)
                {
                    this.toggleGraphCommand = new RelayCommand(item => this.ShowGraph = !this.ShowGraph);
                }

                return this.toggleGraphCommand;
            }
        }

        public bool ShowGraph
        {
            get
            {
                return Settings.Default.ShowGraph;
            }

            set
            {
                if (Settings.Default.ShowGraph != value)
                {
                    Settings.Default.ShowGraph = value;
                    Settings.Default.Save();
                    this.OnPropertyChanged("ShowGraph");                    
                }
            }
        }

        private RelayCommand toggleMinerCommand;

        public ICommand ToggleMinerCommand
        {
            get
            {
                if (this.toggleMinerCommand == null)
                {
                    this.toggleMinerCommand = new RelayCommand(item => 
                        {
                            this.ShowMiner = !this.ShowMiner;
                            this.EnsureWindowVisibility(this.ShowMiner);
                        }
                    );
                }

                return this.toggleMinerCommand;
            }
        }

        public bool ShowMiner
        {
            get
            {
                return Settings.Default.ShowMiner;
            }

            set
            {
                if (Settings.Default.ShowMiner != value)
                {
                    Settings.Default.ShowMiner = value;
                    Settings.Default.Save();
                    this.OnPropertyChanged("ShowMiner");
                }
            }
        }

        private string applicationTitle;

        public string ApplicationTitle
        {
            get
            {
                return this.applicationTitle;
            }

            set
            {
                this.SetProperty(ref this.applicationTitle, value);
            }
        }

        private bool isUpdateAvailable;

        public bool IsUpdateAvailable
        {
            get
            {
                return this.isUpdateAvailable;
            }

            set
            {
                this.SetProperty(ref this.isUpdateAvailable, value);
            }
        }

        public double GraphWidth { get; set; }

        public ObservableCollection<DataPoint> DataPointsHashRate { get; private set; }

        public ObservableCollection<GraphTimeSpan> GraphTimeSpans { get; private set; }
        
        private GraphTimeSpan selectedGraphTimeSpan;

        public GraphTimeSpan SelectedGraphTimeSpan
        {
            get
            {
                return this.selectedGraphTimeSpan;
            }

            set
            {
                var needsUpdate = this.selectedGraphTimeSpan != value;
                this.SetProperty(ref this.selectedGraphTimeSpan, value);
                
                if (needsUpdate && this.summaryDataManager != null)
                {
                    var startUtc = DateTime.UtcNow - value.Span;                    

                    var callback = new Action<IEnumerable<SummaryData>>((summaryData) =>
                        {
                            // update series
                            var hashRateData = summaryData.Select(s => new DataPoint() { TimeLocal = s.TimestampUtc.ToLocalTime(), Value = s.TotalKiloHashesAverage5Sec });
                            PerformFlatLineFix(hashRateData);

                            this.DataPointsHashRate = new ObservableCollection<DataPoint>(hashRateData);
                            this.OnPropertyChanged("DataPointsHashRate");
                        });

                    this.DataPointsHashRate.Clear();
                    this.summaryDataManager.LoadDataAsync(startUtc, value.Span, Math.Max(this.GraphWidth, InitialWindowWidth), callback);
                }
            }
        }

        private static void PerformFlatLineFix(IEnumerable<DataPoint> hashRateData, DataPoint referencePoint = null)
        {
            // workaround for charting component unable to handle displaying a straight line (if all viewable data points are equal to the same value it throws an overflow exception)
            if (referencePoint == null)
            {
                referencePoint = hashRateData.FirstOrDefault();
            }
            
            if (hashRateData.Count() > 0 && hashRateData.All(p => p.Value == referencePoint.Value))
            {
                referencePoint.Value += 1.0 / 10000;
            }
        }

        private UserActivity activity;

        public UserActivity Activity
        {
            get
            {
                return this.activity;
            }

            set
            {
                if (this.activity != value)
                {
                    if (!IsDesignMode && this.idleMonitoringTimer != null)
                    {
                        if (value == UserActivity.Active || value == UserActivity.Idle)
                        {
                            this.idleMonitoringTimer.Start();
                        }
                        else
                        {
                            this.idleMonitoringTimer.Stop();
                        }
                    }

                    this.SetProperty(ref this.activity, value);
                    this.OnPropertyChanged("IsUserActive");
                }
            }
        }

        private TimeSpan snoozeDuration;

        public TimeSpan SnoozeDuration
        {
            get
            {
                return this.snoozeDuration;
            }

            set
            {
                this.SetProperty(ref this.snoozeDuration, value);
            }
        }

        public IEnumerable<TimeSpan> SnoozeDurations { get; private set; }

        private TimeSpan snoozeDurationRemaining;

        public TimeSpan SnoozeDurationRemaining 
        { 
            get
            {
                return this.snoozeDurationRemaining;
            }

            private set
            {
                this.SetProperty(ref this.snoozeDurationRemaining, value);
            }
        }

        private bool isImportantProcessDetected;

        public bool IsImportantProcessDetected
        {
            get
            {
                return this.isImportantProcessDetected;
            }

            private set
            {
                var isImportantProcessAbsent = this.isImportantProcessDetected && !value;
                var isImportantProcessIntroduced = !this.isImportantProcessDetected && value;
                this.SetProperty(ref this.isImportantProcessDetected, value);
                if (isImportantProcessAbsent)
                {
                    // now that any important processes (VIP's - Very Important Processes?) are gone, ensure that the desired state is correct
                    this.AddMessage("Important process has completed. Normal mining behavior will now resume.");
                    this.ConfigureDesiredMinerStatus((this.Activity == UserActivity.SnoozeRequested) ? MinerProcessStatus.Stopped : MinerProcessStatus.Running);                    
                }

                if (isImportantProcessIntroduced)
                {
                    this.controller.DesiredMinerStatus = MinerProcessStatus.Stopped;
                    this.AddMessage("Important process detected - mining is now paused.");
                }
            }
        }

        public bool IsSnoozeEnabled
        {
            get
            {
                return this.SnoozeDurationRemaining > TimeSpan.Zero;
            }
        }

        private void Snooze(TimeSpan snoozeDuration)
        {
            this.Activity = UserActivity.SnoozeRequested;
            this.ConfigureDesiredMinerStatus(MinerProcessStatus.Stopped);
            this.SnoozeDurationRemaining = snoozeDuration;
            this.OnPropertyChanged("IsSnoozeEnabled");
                        
            var snoozeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            snoozeTimer.Tick += (sender, args) =>
            {
                this.SnoozeDurationRemaining -= TimeSpan.FromSeconds(1);
                if (this.SnoozeDurationRemaining <= TimeSpan.Zero)
                {
                    snoozeTimer.Stop();
                    this.Activity = UserActivity.Active;
                    this.ConfigureDesiredMinerStatus(MinerProcessStatus.Running);
                    this.OnPropertyChanged("IsSnoozeEnabled");
                }
            };

            snoozeTimer.Start();
        }

        private void ConfigureDesiredMinerStatus(MinerProcessStatus minerProcessStatus)
        {
            if (!this.IsImportantProcessDetected)
            {
                this.controller.IsMinerDesiredVisible = this.ShowMiner;
                this.controller.DesiredMinerStatus = minerProcessStatus;
            }
        }

        private void AddMessage(string message)
        {
            this.Messages.Add(string.Format("[{0}] {1}", DateTime.Now, message));
        }

        public static bool AreAllPointsEqual(IEnumerable<DataPoint> data)
        {
            var first = data.FirstOrDefault();
            if (first == null)
            { return true; }

            return data.All(p => p.Value == first.Value);
        }

        private void UpdateSummaryData(SummaryData summaryData)
        {
            TrimToGraphRange(this.DataPointsHashRate, this.SelectedGraphTimeSpan.Span);

            // add the data point to our series if it will actually result in an additional pixel, this prevents the graph from getting overloaded with too many
            // data points even if the UI stays open for long periods of time (for example, a new point every 10 seconds at 7d time range - that would result 
            // in a very high density that would impact performance while not actually showing any additional detail on the screen)
            var dataPoint = new DataPoint() { TimeLocal = summaryData.TimestampUtc.ToLocalTime(), Value = summaryData.TotalKiloHashesAverage5Sec };
            var minimumGap = TimeSpan.FromMilliseconds(this.SelectedGraphTimeSpan.Span.TotalMilliseconds / Math.Max(this.GraphWidth, InitialWindowWidth));
            if (this.DataPointsHashRate.Count == 0 || (dataPoint.TimeLocal - this.DataPointsHashRate.Last().TimeLocal) >= minimumGap)
            {
                PerformFlatLineFix(this.DataPointsHashRate, dataPoint);                

                this.DataPointsHashRate.Add(dataPoint);
            }

            // inform the data manager about the new point (regardless of display density logic)
            if (this.summaryDataManager != null)
            {
                this.summaryDataManager.Add(summaryData);
            }

            this.ApplicationTitle = string.Format("Mining Controller ({0:0.#} KH/s)", summaryData.TotalKiloHashesAverage5Sec);
        }

        private void TrimToGraphRange(IList<DataPoint> data, TimeSpan range)
        {
            var start = DateTime.Now - range;

            // 1. make a copy of what the datapoints look like trimmed
            var copy = new List<DataPoint>(data);
            TrimData(copy, start);
                        
            // 2. perform a flat line fix on it to adjust the correct individual datapoint within the collection
            PerformFlatLineFix(copy);
                        
            // 3. trim the points from the actual list, which triggers view model updates to the graph component - we can now be assured that there will not be a flat line in the result, even with points removed
            var beforeCount = data.Count;
            TrimData(data, start);

            // although the flat-line fix that is currently in place should prevent this, i'm leaving this here in case there is another edge case that crops up
            if (data.Count > 0 && AreAllPointsEqual(data))
            {
                throw new Exception(string.Format("Unexpected flat line detected in graph series - this results in an overflow exception in the third-party library. BeforeCount [{0}], AfterCount[{1}]", beforeCount, data.Count));
            }
        }

        private static void TrimData(IList<DataPoint> data, DateTime start)
        {
            while (data.Count > 0 && data[0].TimeLocal <= start)
            {
                data.RemoveAt(0);
            }
        }

        public ObservableCollection<string> Messages { get; private set; }
        
        private TimeSpan idleTime;

        public TimeSpan IdleTime
        {
            get
            {
                return this.idleTime;
            }

            set
            {   
                this.SetProperty(ref this.idleTime, value);

                var prevActivity = this.Activity;
                if (value > Settings.Default.MaxIdleTime)
                {
                    this.Activity = UserActivity.Idle;
                    EnsureDesiredState();
                }

                if (value < Settings.Default.MaxIdleTime && this.Activity == UserActivity.Idle)
                {
                    this.Activity = UserActivity.Active;
                    EnsureDesiredState();
                }

                if (prevActivity != this.Activity)
                {
                    this.EnsureWindowVisibility(this.ShowMiner);
                }
            }
        }

        public bool IsUserActive
        {
            get
            {
                return this.Activity == UserActivity.Active;
            }
        }
        
        private bool isBusy;

        public bool IsBusy
        {
            get
            {
                return this.isBusy;
            }

            set
            {
                this.SetProperty(ref this.isBusy, value);
            }
        }

        private bool isConnected;

        public bool IsConnected
        {
            get
            {
                return this.isConnected;
            }

            set
            {
                // anytime the connection status changes, ensure it matches the desired state
                if (this.isConnected != value)
                {
                    this.SetProperty(ref this.isConnected, value);

                    AddMessage(value ? "Connected" : "Disconnected");
                    this.EnsureDesiredState();
                    this.EnsureWindowVisibility(this.ShowMiner);
                }
            }
        }

        private void EnsureDesiredState()
        {
            switch (this.activity)
            {
                case UserActivity.Unknown:
                    break;
                case UserActivity.Idle:
                    if (this.IsConnected)
                    {
                        var intensity = this.controller.Intensity;
                        if (intensity != Settings.Default.FullIntensity)
                        {
                            this.controller.Intensity = Settings.Default.FullIntensity;
                            AddMessage(string.Format("User is [{0}], changing intensity from [{2}] to [{1}] (full).", Enum.GetName(typeof(UserActivity), this.activity), Settings.Default.FullIntensity, intensity));
                        }
                    }
                    break;
                case UserActivity.SnoozeRequested:
                    AddMessage(string.Format("User is [{0}], shutting down miner.", Enum.GetName(typeof(UserActivity), this.activity)));
                    break;
                case UserActivity.Active:
                    if (this.IsConnected)
                    {
                        var intensity = this.controller.Intensity;
                        if (intensity != Settings.Default.ThrottledIntensity)
                        {
                            this.controller.Intensity = Settings.Default.ThrottledIntensity;
                            AddMessage(string.Format("User is [{0}], changing intensity from [{2}] to [{1}] (throttled).", Enum.GetName(typeof(UserActivity), this.activity), Settings.Default.ThrottledIntensity, intensity));
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        private void EnsureWindowVisibility(bool visible)
        {
            try
            {
                if (this.IsConnected)
                {   
                    this.windowController.SetWindowVisibilityByProcessName(Settings.Default.MinerProcessName, visible);
                }
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException)
                {
                    AddMessage(ex.Message);
                }
                else
                {
#if DEBUG
                    AddMessage(ex.Message);
                    throw;
#else
                    if (ex is System.ComponentModel.Win32Exception && Settings.Default.AutomaticErrorReporting)
                    {
                        BugFreak.ReportingService.Instance.BeginReport(ex);
                    }
#endif
                }
            }
        }
    }
}
