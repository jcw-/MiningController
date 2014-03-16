using Newtonsoft.Json;

namespace MiningController.Mining
{
    public class MiningDevice
    {
        [JsonProperty("GPU")]
        public int Gpu { get; set; }

        [JsonProperty("Enabled")]
        public string _Enabled { get; set; }

        public bool IsEnabled
        {
            get
            {
                return this._Enabled == "Y";
            }
        }

        public string Status { get; set; }

        public double Temperature { get; set; }

        [JsonProperty("Fan Speed")]
        public int FanSpeed { get; set; }

        [JsonProperty("Fan Percent")]
        public double FanPercent { get; set; }

        [JsonProperty("GPU Clock")]
        public int GpuClock { get; set; }

        [JsonProperty("Memory Clock")]
        public int MemoryClock { get; set; }

        [JsonProperty("GPU Voltage")]
        public double GpuVoltage { get; set; }

        [JsonProperty("GPU Activity")]
        public double GpuActivity { get; set; }

        public int Powertune { get; set; }

        [JsonProperty("MHS av")]
        public double MhsAverage { get; set; }

        [JsonProperty("Mhs 5s")]
        public double MhsFiveSeconds { get; set; }

        public int Accepted { get; set; }

        public int Rejected { get; set; }

        [JsonProperty("Hardware Errors")]
        public int HardwareErrors { get; set; }

        public double Utility { get; set; }

        public int Intensity { get; set; }

        [JsonProperty("Last Share Pool")]
        public long LastSharePool { get; set; }

        [JsonProperty("Last Share Time")]
        public long LastShareTime { get; set; }

        [JsonProperty("Total MH")]
        public double TotalMH { get; set; }

        [JsonProperty("Diff1 Work")]
        public int Diff1Work { get; set; }

        [JsonProperty("Difficulty Accepted")]
        public double DifficultyAccepted { get; set; }

        [JsonProperty("Difficulty Rejected")]
        public double DifficultyRejected { get; set; }

        [JsonProperty("Last Share Difficulty")]
        public double LastShareDifficulty { get; set; }

        [JsonProperty("Last Valid Work")]
        public int LastValidWork { get; set; }

        [JsonProperty("Device Hardware")]
        public double DeviceHardware { get; set; }

        [JsonProperty("Device Rejected")]
        public double DeviceRejected { get; set; }

        [JsonProperty("Device Elapsed")]
        public long DeviceElapsed { get; set; }
    }
}
