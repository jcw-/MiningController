using ProtoBuf;
using System;

namespace MiningController.Model
{
    [ProtoContract]
    public class DataPoint
    {
        [ProtoMember(1)]
        public DateTime TimeLocal { get; set; }

        [ProtoMember(2)]
        public double Value { get; set; }
    }
}