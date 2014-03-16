using System;

namespace MiningController.Mining
{
    public class TooManyMinerLaunches : Exception
    {
        public TooManyMinerLaunches() : base() { }

        public TooManyMinerLaunches(string message) : base(message) { }
    }
}
