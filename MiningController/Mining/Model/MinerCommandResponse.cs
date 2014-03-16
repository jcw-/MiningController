using Newtonsoft.Json;
using System.Collections.Generic;

namespace MiningController.Mining
{
    public class MinerCommandResponse
    {
        [JsonProperty("STATUS")]
        public List<StatusResponse> Statuses { get; set; }

        public int id { get; set; }
    }
}
