using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiningController.Mining
{
    public class SummaryEventArgs : EventArgs
    {
        public SummaryData Summary { get; set; }
    }
}
