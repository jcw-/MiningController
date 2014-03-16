using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;

namespace MiningController.Mining
{
    public class SummaryDataManager : ISummaryDataManager
    {
        private const string FolderName = "GraphData";
        private const string FileName = "datastore.proto";
        private readonly TimeSpan MaxPersistAge = TimeSpan.FromDays(7);
        private Lazy<List<SummaryData>> allKnownSummaryData;
        private DispatcherTimer persistenceTimer;
        private readonly object padlock = new object();
        private string filePath;
        private string folderPath;
        private bool unsavedData;

        public event EventHandler<MessageEventArgs> Message = delegate { };

        public SummaryDataManager(TimeSpan saveInterval)
        {
            // enforce one minute as the fastest save interval
            if (saveInterval < TimeSpan.FromMinutes(1) && saveInterval != TimeSpan.MinValue)
            {
                saveInterval = TimeSpan.FromMinutes(1);
            }

            this.folderPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), FolderName);
            this.filePath = Path.Combine(this.folderPath, FileName);

            this.allKnownSummaryData = new Lazy<List<SummaryData>>(() => LoadFromDisk());

            this.persistenceTimer = new DispatcherTimer();
            this.persistenceTimer.Tick += SaveToDisk;
            this.persistenceTimer.Interval = saveInterval;
        }

        private List<SummaryData> LoadFromDisk()
        {
            var data = new List<SummaryData>();

            if (File.Exists(this.filePath))
            {
                using (var file = File.OpenRead(this.filePath))
                {
                    data = Serializer.Deserialize<List<SummaryData>>(file);
                }
            }

            this.unsavedData = false;

            this.AddOutageData(data, DateTime.UtcNow);

            // TimeSpan.MinValue has the special behavior of disabling save to disk
            if (this.persistenceTimer.Interval != TimeSpan.MinValue)
            {
                this.persistenceTimer.Start();
            }

            return data;
        }

        /// <summary>
        /// Zero-out time span between last stored data point and now so that the graph doesn't interpolate across the outage. Since the application
        /// wasn't running to monitor the miner, we have to assume a zero hash rate.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="outageEndUtc"></param>
        private void AddOutageData(List<SummaryData> data, DateTime outageEndUtc)
        {
            if (data.Count > 0)
            {
                var outageStartUtc = data.Max(p => p.TimestampUtc) + TimeSpan.FromMilliseconds(1);
                data.Add(new SummaryData() { TimestampUtc = outageStartUtc, TotalKiloHashesAverage5Sec = 1 / 10000 });
                data.Add(new SummaryData() { TimestampUtc = outageEndUtc, TotalKiloHashesAverage5Sec = 1 / 10000 });
            }
        }

        private void SaveToDisk(object sender, EventArgs e)
        {
            try
            {
                // looks like roughly 1.5MB/7day for file size
                if (!this.unsavedData || this.allKnownSummaryData.Value.Count == 0)
                {
                    return;
                }

                if (!Directory.Exists(this.folderPath))
                {
                    Directory.CreateDirectory(this.folderPath);
                }

                // save to disk
                var startUtc = DateTime.UtcNow - MaxPersistAge;
                var data = this.allKnownSummaryData.Value.Where(d => d.TimestampUtc > startUtc).ToList();
                using (var file = File.Create(this.filePath))
                {
                    Serializer.Serialize<List<SummaryData>>(file, data);
                }

                this.unsavedData = false;
            }
            catch (Exception ex)
            {
                this.Message(this, new MessageEventArgs() { Message = ex.ToString() });
            }
        }

        public void Add(SummaryData summaryData)
        {
            this.allKnownSummaryData.Value.Add(summaryData);
            this.unsavedData = true;
        }

        public void LoadDataAsync(DateTime startUtc, TimeSpan timeSpan, double requestedDensity, Action<IEnumerable<SummaryData>> callback)
        {
            var worker = new System.ComponentModel.BackgroundWorker();
            worker.DoWork += (s, e) =>
            {
                lock (this.padlock)
                {
                    SummaryData initialPoint;
                    var data = this.allKnownSummaryData.Value.Where(p => p.TimestampUtc >= startUtc).ToList();
                    if (data.Count == 0)
                    {
                        initialPoint = new SummaryData()
                        {
                            TimestampUtc = startUtc.AddSeconds(-1) + timeSpan,
                            TotalKiloHashesAverage5Sec = 1.0 / 10000
                        };

                        data.Add(initialPoint);
                    }
                    else
                    {
                        this.EnsureFixedDensity(data, startUtc, timeSpan, requestedDensity);
                    }

                    this.PrefixPadData(data, startUtc, timeSpan.TotalMilliseconds / Math.Max(requestedDensity, 100));

                    callback(data.OrderBy(p => p.TimestampUtc).ToList());
                }
            };

            worker.RunWorkerAsync();
        }

        private void EnsureFixedDensity(List<SummaryData> data, DateTime startUtc, TimeSpan timeSpan, double requestedDensity)
        {
            // TODO: implement graph performance boost - reduce density by removing points or increase it by adding interpolated points
        }

        private void PrefixPadData(List<SummaryData> data, DateTime startUtc, double millisPerPixel)
        {
            // prefix padding
            var initialTime = data.Min(p => p.TimestampUtc);
            if (initialTime > startUtc)
            {
                var time = startUtc;
                var count = ((initialTime - startUtc).TotalMilliseconds / millisPerPixel);
                for (var i = 0; i < count; i++)
                {
                    data.Add(new SummaryData() { TimestampUtc = time, TotalKiloHashesAverage5Sec = 1.0 / 10000 });
                    time += TimeSpan.FromMilliseconds(millisPerPixel);
                }
            }
        }
    }
}
