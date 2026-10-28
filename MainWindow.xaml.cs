using System.Windows;
using ZebraBleLabelPrinter.ViewModels;

namespace ZebraBleLabelPrinter;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
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
