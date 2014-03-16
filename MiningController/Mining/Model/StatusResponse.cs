using Newtonsoft.Json;

namespace MiningController.Mining
{
    public enum StatusKind
    {
        Unknown,
        Warning,
        Informational,
        Success,
        Error,
        Fatal
    }

    public class StatusResponse
    {   
        private string _status;

        [JsonProperty("STATUS")]
        public string _Status
        {
            get
            {
                return this._status;
            }

            set
            {
                this._status = value;
                switch (value)
                {
                    case "W": this.Status = StatusKind.Warning; break;
                    case "I": this.Status = StatusKind.Informational; break;
                    case "S": this.Status = StatusKind.Success; break;
                    case "E": this.Status = StatusKind.Error; break;
                    case "F": this.Status = StatusKind.Fatal; break;
                    default: this.Status = StatusKind.Unknown; break;
                }
            }
        }

        public long When { get; set; }

        public int Code { get; set; }

        public string Msg { get; set; }

        public string Description { get; set; }

        public StatusKind Status { get; private set; }
    }
}
