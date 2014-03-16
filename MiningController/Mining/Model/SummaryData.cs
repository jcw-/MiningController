using ProtoBuf;
using System;

namespace MiningController.Mining
{
    [ProtoContract]
    public class SummaryData
    {
        [ProtoMember(1)]
        public DateTime TimestampUtc { get; set; }

        [ProtoMember(2)]
        public double TotalKiloHashesAverage5Sec { get; set; }

        [ProtoMember(3)]
        public int TotalHardwareErrors { get; set; }

        [ProtoMember(4)]
        public int TotalStale { get; set; }
    }
}
