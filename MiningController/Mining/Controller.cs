using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace MiningController.Mining
{
    public enum MinerProcessStatus
    {
        Unknown,
        Running,
        Stopped
    }

    public class Controller : IController
    {   
        private DispatcherTimer watchdogTimer;
        private TimeSpan watchDogPollingPeriod;
        private TimeSpan watchDogPollingPeriodDisconnected;
        private IMinerCommunication minerComms;
        private bool raiseBlankSummaryDataNotifications;
        private int stopAttempts;
        private static readonly TimeSpan watchDogPollingPeriodStopping = TimeSpan.FromSeconds(3);
        private const int MaxPoliteStopAttempts = 3;

        public event EventHandler<EventArgs> Connected = delegate { };

        public event EventHandler<EventArgs> Disconnected = delegate { };

        public event EventHandler<MessageEventArgs> Message = delegate { };

        public event EventHandler<SummaryEventArgs> Summary = delegate { };
        
        public Controller(IMinerCommunication minnerCommunication, TimeSpan watchDogPollingPeriod)
        {   
            this.minerComms = minnerCommunication;

            // bubble up events from IMinerCommunication
            this.minerComms.Message += (s, e) => { this.Message(s, e); }; 
            this.minerComms.Connected += (s, e) => { this.OnConnected(); };

            this.watchDogPollingPeriod = watchDogPollingPeriod;

            // need to balance this speed between the mining process being able to start in this time and also having the app detect the initial connected status in a reasonable amount of time
            // so far, it seems that at giving it 5 seconds (since the minimum polling period is 10) is working well
            // if it's too fast, it will spawn an extra mining process
            this.watchDogPollingPeriodDisconnected = TimeSpan.FromTicks(watchDogPollingPeriod.Ticks / 2);

            this.watchdogTimer = new DispatcherTimer();
            this.watchdogTimer.Tick += watchdogTimer_Tick;
            this.watchdogTimer.Interval = watchDogPollingPeriodDisconnected;
            this.watchdogTimer.Start();
        }

        private void watchdogTimer_Tick(object sender, EventArgs e)
        {
            // watchdog timer runs all the time in order to provide summary details, but it actually only pings the miner when the desired status is running
            if (raiseBlankSummaryDataNotifications)
            {
                this.PerformSummaryNotification(null);
                this.EnsureMinerStatus();
                return;
            }

            // every timer tick, call a simple command on the miner - request the summary (also serves to track average hashrate, and maybe later, hardware errors)
            var json = this.minerComms.ExecuteCommand(new MinerCommand() { Command = "summary" });
            var response = JsonConvert.DeserializeObject<MinerCommandResponse>(json);
            StatusResponse status = null;

            if (response != null)
            {
                status = response.Statuses.FirstOrDefault();
            }

            if (status == null)
            {
                this.OnDisconnected();
                
                // this is too verbose unless troubleshooting
                //this.Message(this, new MessageEventArgs() { Message = "Watchdog check was unable to determine miner status." });
            }

            if (status != null && status.Status != StatusKind.Success)
            {
                this.OnDisconnected();
                this.Message(this, new MessageEventArgs() { Message = string.Format("Watchdog check received an unexpected status [{0}] [{1}]", Enum.GetName(typeof(StatusKind), status.Status), status.Msg) });
            }

            this.PerformSummaryNotification(json);

            if (sender != null && e != null)
            {
                // if we detect a mismatch between desired and actual, re-run the logic
                var running = this.minerComms.MinerProcessDetected;
                if ((this.DesiredMinerStatus == MinerProcessStatus.Running && !running) || (this.DesiredMinerStatus == MinerProcessStatus.Stopped && running))
                {
                    this.EnsureMinerStatus();
                }
            }
        }

        private void PerformSummaryNotification(string json)
        {
            SummaryData data;

            if (string.IsNullOrEmpty(json))
            {
                data = new SummaryData()
                {
                    TimestampUtc = DateTime.UtcNow,
                    TotalHardwareErrors = 0,
                    TotalKiloHashesAverage5Sec = 1 /10000,
                    TotalStale = 0
                };
            }
            else
            {
                var d = JsonConvert.DeserializeObject<dynamic>(json);
                JArray summary = d.SUMMARY;

                data = new SummaryData()
                {
                    TimestampUtc = DateTime.UtcNow,
                    TotalHardwareErrors = summary.Select(s => (int)s["Hardware Errors"]).Sum(),
                    TotalKiloHashesAverage5Sec = Math.Max(summary.Select(s => (double)s["MHS 5s"]).Sum() * 1000, 1 / 10000),
                    TotalStale = summary.Select(s => (int)s["Stale"]).Sum()
                };
            }

            this.Summary(this, new SummaryEventArgs() { Summary = data });
        }

        public bool ImportantProcessDetected
        {
            get
            {
                return this.minerComms.ImportantProcessDetected;
            }
        }

        public bool IsMinerDesiredVisible { get; set; }

        private MinerProcessStatus desiredMinerStatus;

        public MinerProcessStatus DesiredMinerStatus
        {
            get
            {
                return this.desiredMinerStatus;
            }
            
            set
            {
                this.stopAttempts = 0;
                this.desiredMinerStatus = value;
                this.EnsureMinerStatus();
            }
        }

        private void EnsureMinerStatus()
        {
            switch (this.DesiredMinerStatus)
            {
                case MinerProcessStatus.Unknown:
                    StopMiner();
                    break;
                case MinerProcessStatus.Running:
                    StartMiner();
                    break;
                case MinerProcessStatus.Stopped:
                    StopMiner();
                    break;
                default:
                    StopMiner();
                    break;
            } 
        }

        private void StartMiner()
        {
            var launched = false;

            // if process isn't running, launch it
            if (!this.minerComms.MinerProcessDetected)
            {
                try
                {
                    launched = this.minerComms.LaunchMinerProcess(this.IsMinerDesiredVisible);
                }
                catch(TooManyMinerLaunches ex)
                {
                    this.Message(this, new MessageEventArgs() { Message = ex.Message });
                    StopMiner();
                    this.watchdogTimer.Stop();
                }
                catch(ArgumentException ex)
                {
                    this.Message(this, new MessageEventArgs() { Message = ex.Message });
                    this.Message(this, new MessageEventArgs() { Message = "Configure LaunchCommand by clicking File -> Settings." });
                    this.Message(this, new MessageEventArgs() { Message = "See About -> 'Getting Started' for instructions on all required settings." });
                    this.Message(this, new MessageEventArgs() { Message = "Restart this application after the updated configuration file has been saved." });
                    StopMiner();
                    this.watchdogTimer.Stop();
                }
            }
            else
            {
                this.Message(this, new MessageEventArgs() { Message = "The miner appears to be running." });
            }

            this.raiseBlankSummaryDataNotifications = false;

            if (!launched)
            {
                // unless we just launched the miner (in which case we want to wait for it to initialize), we can begin monitoring right away
                this.watchdogTimer_Tick(null, null);                
            }
        }

        private void StopMiner()
        {
            // stop watchdog
            this.raiseBlankSummaryDataNotifications = true;
            this.OnDisconnected();

            if (this.minerComms.MinerProcessDetected)
            {
                if (this.stopAttempts >= MaxPoliteStopAttempts)
                {
                    this.Message(this, new MessageEventArgs() { Message = "Killing miner process..." });
                    this.minerComms.KillMinerProcess();
                }
                else
                {
                    // if process isn't running, request it to stop
                    this.stopAttempts++;
                    this.minerComms.ExecuteCommand(new MinerCommand() { Command = "quit" });
                    this.Message(this, new MessageEventArgs() { Message = string.Format("Miner has been requested to shutdown [{0}/{1}].", this.stopAttempts, MaxPoliteStopAttempts) });                    
                }

                this.watchdogTimer.Interval = watchDogPollingPeriodStopping; // monitor relatively quickly until the miner is stopped
            }
            else
            {
                this.stopAttempts = 0;
            }
        }

        public string Version
        {
            get
            {
                return JsonConvert.DeserializeObject<MinerCommandResponse>(this.minerComms.ExecuteCommand(new MinerCommand() { Command = "version" })).Statuses.First().Description;
            }
        }

        public int Intensity
        {
            get
            {
                return this.Devices.First().Intensity;
            }

            set
            {
                MinerCommandResponse response = JsonConvert.DeserializeObject<MinerCommandResponse>(this.minerComms.ExecuteCommand(new MinerCommand() { Command = "gpuintensity", Parameter = "0," + value.ToString() }));
                if (response != null && response.Statuses.First().Status != StatusKind.Informational)
                {
                    this.Message(this, new MessageEventArgs() { Message = string.Format("Call to gpuintensity returned a status of [{0}] [{1}]", Enum.GetName(typeof(StatusKind), response.Statuses.First().Status), response.Statuses.First().Msg) });
                }
            }
        }

        private void OnDisconnected()
        {
            this.watchdogTimer.Interval = watchDogPollingPeriodDisconnected;
            this.Disconnected(this, EventArgs.Empty);
        }

        private void OnConnected()
        {
            this.watchdogTimer.Interval = watchDogPollingPeriod;
            this.Connected(this, EventArgs.Empty);
        }

        private IEnumerable<MiningDevice> Devices
        {
            get
            {
                DevicesResponse response = JsonConvert.DeserializeObject<DevicesResponse>(this.minerComms.ExecuteCommand(new MinerCommand() { Command = "devs" }));
                if (response != null && response.Statuses.First().Status == StatusKind.Success)
                {
                    return response.Devices;
                }
                else
                {
                    return new List<MiningDevice>();
                }
            }
        }
    }
}
