using System;

namespace MiningController.Mining
{
    public interface IController
    {
        event EventHandler<EventArgs> Connected;

        event EventHandler<EventArgs> Disconnected;

        event EventHandler<MessageEventArgs> Message;

        event EventHandler<SummaryEventArgs> Summary;

        MinerProcessStatus DesiredMinerStatus { get; set; }

        bool IsMinerDesiredVisible { get; set; }

        string Version { get; }

        int Intensity { get; set; }

        bool ImportantProcessDetected { get; }
    }
}
