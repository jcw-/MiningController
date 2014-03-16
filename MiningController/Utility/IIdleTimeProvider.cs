using System;

namespace MiningController
{
    public interface IIdleTimeProvider
    {
        TimeSpan IdleTime { get; }
    }
}
