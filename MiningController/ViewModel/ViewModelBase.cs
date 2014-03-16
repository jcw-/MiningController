using MiningController.Model;
using System.ComponentModel;

namespace MiningController.ViewModel
{
    public abstract class ViewModelBase : NotifyBase
    {
        private static readonly bool isInDesignMode = DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject());

        public static bool IsDesignMode
        {
            get
            {
                return isInDesignMode;
            }
        }
    }
}
