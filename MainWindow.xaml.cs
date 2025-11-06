using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SerialSnoop.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly System.Windows.Threading.DispatcherTimer _autoScrollTimer;
    private object? _pendingScrollItem;
    private static readonly TimeSpan _autoScrollDebounce = TimeSpan.FromMilliseconds(100);
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ViewModels.MainViewModel();

        // Initialize a short debounce timer to batch frequent ScrollIntoView calls.
        _autoScrollTimer = new System.Windows.Threading.DispatcherTimer { Interval = _autoScrollDebounce };
        _autoScrollTimer.Tick += (s, e) =>
        {
            _autoScrollTimer.Stop();
            var item = _pendingScrollItem;
            _pendingScrollItem = null;
            if (item is null) return;
            try
            {
                if ((DataContext as ViewModels.MainViewModel)?.AutoScroll == true)
                    LogList.ScrollIntoView(item);
            }
            catch
            {
                // best-effort only: avoid letting UI races crash the process
            }
        };

        // Auto-scroll behavior handled in code-behind on collection changes
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.LogEntries.CollectionChanged += (s, e) =>
            {
                if (!vm.AutoScroll) return;

                // Only schedule scroll on Adds or Reset; store the last item and
                // restart the debounce timer so bursts result in a single scroll.
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
                    || e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                {
                    if (vm.LogEntries.Count == 0) return;

                    _pendingScrollItem = vm.LogEntries[^1];
                    _autoScrollTimer.Stop();
                    _autoScrollTimer.Start();
                }
            };
        }
    }
}