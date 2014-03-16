using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MiningController.ViewModel
{
    /// <summary>
    /// This class contains a few useful extenders for the ListBox
    /// </summary>
    public class ScrollToEndBehavior : DependencyObject
    {
        public static readonly DependencyProperty AutoScrollToEndProperty = DependencyProperty.RegisterAttached("AutoScrollToEnd", typeof(bool), typeof(ScrollToEndBehavior), new UIPropertyMetadata(default(bool), OnAutoScrollToEndChanged));

        /// <summary>
        /// Returns the value of the AutoScrollToEndProperty
        /// </summary>
        /// <param name="obj">The dependency-object whichs value should be returned</param>
        /// <returns>The value of the given property</returns>
        public static bool GetAutoScrollToEnd(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoScrollToEndProperty);
        }

        /// <summary>
        /// Sets the value of the AutoScrollToEndProperty
        /// </summary>
        /// <param name="obj">The dependency-object whichs value should be set</param>
        /// <param name="value">The value which should be assigned to the AutoScrollToEndProperty</param>
        public static void SetAutoScrollToEnd(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoScrollToEndProperty, value);
        }

        /// <summary>
        /// This method will be called when the AutoScrollToEnd
        /// property was changed
        /// </summary>
        /// <param name="s">The sender (the ListBox)</param>
        /// <param name="e">Some additional information</param>
        public static void OnAutoScrollToEndChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
        {
            var itemsControl = s as ItemsControl;
            var items = itemsControl.Items;
            var data = items.SourceCollection as INotifyCollectionChanged;

            var scrollToEndHandler = new System.Collections.Specialized.NotifyCollectionChangedEventHandler(
                (s1, e1) =>
                {
                    if (itemsControl.Items.Count > 0)
                    {
                        object lastItem = itemsControl.Items[itemsControl.Items.Count - 1];
                        items.MoveCurrentTo(lastItem);

                        if (s is ListBox)
                        {
                            ((ListBox)itemsControl).ScrollIntoView(lastItem);
                            return;
                        }

                        if (s is DataGrid)
                        {
                            ((DataGrid)itemsControl).ScrollIntoView(lastItem);
                            return;
                        }

                        var container = itemsControl.ItemContainerGenerator.ContainerFromItem(lastItem) as FrameworkElement;
                        if (container != null)
                        {
                            container.BringIntoView();
                        }
                    }
                });

            if ((bool)e.NewValue)
                data.CollectionChanged += scrollToEndHandler;
            else
                data.CollectionChanged -= scrollToEndHandler;
        }
    }
}
