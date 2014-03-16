using System;

namespace MiningController.Mining
{
    public interface IMinerCommunication
    {
        event EventHandler<EventArgs> Connected;

        event EventHandler<MessageEventArgs> Message;

        bool MinerProcessDetected { get; }

        bool ImportantProcessDetected { get; }

        bool LaunchMinerProcess(bool visible);

        void KillMinerProcess();

        string ExecuteCommand(MinerCommand command);
    }
}
