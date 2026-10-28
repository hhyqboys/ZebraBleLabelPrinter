using System.Windows;
using System.Windows.Input;
using ZebraBleLabelPrinter.ViewModels;

namespace ZebraBleLabelPrinter;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    // 连按 5 次 Ctrl（相邻间隔 ≤500ms）呼出/关闭“复制选中 MAC”按钮
    private static readonly TimeSpan CtrlWindow = TimeSpan.FromMilliseconds(500);
    private const int CtrlCountToToggle = 5;
    private DateTime _lastCtrlPress = DateTime.MinValue;
    private int _ctrlStreak;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.IsRepeat) return;
        if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl) return;

        DateTime now = DateTime.Now;
        _ctrlStreak = _ctrlStreak > 0 && now - _lastCtrlPress <= CtrlWindow
            ? _ctrlStreak + 1
            : 1;
        _lastCtrlPress = now;

        if (_ctrlStreak >= CtrlCountToToggle)
        {
            _ctrlStreak = 0;
            _vm.ToggleCopyButton();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.Shutdown();
        base.OnClosed(e);
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {

    }
}
