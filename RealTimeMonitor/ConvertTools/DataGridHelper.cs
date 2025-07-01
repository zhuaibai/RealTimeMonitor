using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Collections.Specialized;
using System.Windows.Media;

namespace RealTimeMonitor.ConvertTools
{
    public static class DataGridHelper
    {
        public static readonly DependencyProperty SelectedItemsBindingProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItemsBinding",
                typeof(IList),
                typeof(DataGridHelper),
                new PropertyMetadata(null,OnSelectedItemsBindingChanged));

        public static void SetSelectedItemsBinding(DependencyObject element, IList value)
        {
            element.SetValue(SelectedItemsBindingProperty, value);
        }

        public static IList GetSelectedItemsBinding(DependencyObject element)
        {
            return (IList)element.GetValue(SelectedItemsBindingProperty);
        }

        private static void OnSelectedItemsBindingChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid)
            {
                //// 清除旧的事件处理
                //dataGrid.SelectionChanged -= DataGrid_SelectionChanged;

                //if (e.NewValue != null)
                //{
                //    // 设置新的事件处理
                //    dataGrid.SelectionChanged += DataGrid_SelectionChanged;

                //    // 如果ViewModel集合已有数据，同步到DataGrid
                //    if (e.NewValue is IList selectedItems && selectedItems.Count > 0)
                //    {
                //        foreach (var item in selectedItems)
                //        {
                //            dataGrid.SelectedItems.Add(item);
                //        }
                //    }
                //}
                // 移除旧的事件处理
                dataGrid.SelectionChanged -= DataGrid_SelectionChanged;

                // 处理旧集合的变更通知
                if (e.OldValue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= ViewModelCollectionChanged;
                }

                if (e.NewValue != null)
                {
                    // 添加新的事件处理
                    dataGrid.SelectionChanged += DataGrid_SelectionChanged;

                    // 处理新集合的变更通知
                    if (e.NewValue is INotifyCollectionChanged newCollection)
                    {
                        newCollection.CollectionChanged += ViewModelCollectionChanged;
                    }

                    // 安全同步选中项（避免操作UI元素）
                    SyncDataGridSelection(dataGrid);
                }
            }
        }

        private static void ViewModelCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 找到关联的DataGrid
            foreach (var dataGrid in Application.Current.Windows
                .OfType<Window>()
                .SelectMany(w => w.FindChildren<DataGrid>()))
            {
                var binding = GetSelectedItemsBinding(dataGrid);
                if (ReferenceEquals(binding, sender))
                {
                    SyncDataGridSelection(dataGrid);
                    break;
                }
            }
        }

        private static void SyncDataGridSelection(DataGrid dataGrid)
        {
            // 确保在UI线程执行
            dataGrid.Dispatcher.Invoke(() =>
            {
                var selectedItems = GetSelectedItemsBinding(dataGrid);
                if (selectedItems == null) return;

                // 暂停事件处理避免递归
                dataGrid.SelectionChanged -= DataGrid_SelectionChanged;

                // 清除现有选择
                dataGrid.SelectedItems.Clear();

                // 添加新选择（仅添加数据项，不添加UI元素）
                foreach (var item in selectedItems)
                {
                    // 重要：确保添加的是数据对象而不是UI元素
                    dataGrid.SelectedItems.Add(item);
                }

                // 恢复事件处理
                dataGrid.SelectionChanged += DataGrid_SelectionChanged;
            });
        }

        private static void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                //var selectedItems = GetSelectedItemsBinding(dataGrid);
                //if (selectedItems == null) return;

                //// 更新ViewModel集合
                //foreach (var item in e.RemovedItems)
                //{
                //    selectedItems.Remove(item);
                //}

                //foreach (var item in e.AddedItems)
                //{
                //    if (!selectedItems.Contains(item))
                //    {
                //        selectedItems.Add(item);
                //    }
                //}
                var selectedItems = GetSelectedItemsBinding(dataGrid);
                if (selectedItems == null) return;

                // 更新ViewModel集合
                foreach (var item in e.RemovedItems)
                {
                    // 确保操作的是数据对象
                    if (item != null && !(item is DependencyObject))
                    {
                        selectedItems.Remove(item);
                    }
                }

                foreach (var item in e.AddedItems)
                {
                    // 确保操作的是数据对象
                    if (item != null && !(item is DependencyObject) && !selectedItems.Contains(item))
                    {
                        selectedItems.Add(item);
                    }
                }
            }
        }


    }
    public static class VisualTreeExtensions
    {
        public static IEnumerable<T> FindChildren<T>(this DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null) yield break;

            var queue = new Queue<DependencyObject>();
            queue.Enqueue(parent);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var count = VisualTreeHelper.GetChildrenCount(current);

                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(current, i);
                    if (child is T typedChild)
                    {
                        yield return typedChild;
                    }
                    queue.Enqueue(child);
                }
            }
        }
    }
}
