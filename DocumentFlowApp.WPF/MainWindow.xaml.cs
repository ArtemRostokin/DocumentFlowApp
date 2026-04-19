using System.Windows;
using DocumentFlowApp.WPF.ViewModels;
using DocumentFlowApp.WPF.Views;

namespace DocumentFlowApp.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Конструктор, используемый при запуске с DI
        public MainWindow(KanbanBoardViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            InitializeKanbanView();
        }

        // Параметрless конструктор для дизайнера и быстрого запуска без DI
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new KanbanBoardViewModel();
            InitializeKanbanView();
        }

        private void InitializeKanbanView()
        {
            RootContent.Children.Clear();
            RootContent.Children.Add(new KanbanBoardView());
        }
    }
}
