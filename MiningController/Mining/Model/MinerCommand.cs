using Newtonsoft.Json;

namespace MiningController.Mining
{
    public class MinerCommand
    {
        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("parameter")]
        public string Parameter { get; set; }
    }
}
