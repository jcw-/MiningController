using Newtonsoft.Json;
using System.Collections.Generic;

namespace MiningController.Mining
{
    public class DevicesResponse : MinerCommandResponse
    {
        [JsonProperty("DEVS")]
        public List<MiningDevice> Devices { get; set; }
    }
}
