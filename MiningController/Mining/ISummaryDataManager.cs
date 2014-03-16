using System;
using System.Collections.Generic;

namespace MiningController.Mining
{
    public interface ISummaryDataManager
    {
        event EventHandler<MessageEventArgs> Message;

        void Add(SummaryData summaryData);
                
        void LoadDataAsync(DateTime startUtc, TimeSpan timeSpan, double requestedDensity, Action<IEnumerable<SummaryData>> callback);
    }
}
