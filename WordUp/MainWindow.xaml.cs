using System.Windows;
using WordUp.ViewModels;

namespace WordUp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
