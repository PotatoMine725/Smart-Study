using System.Windows;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.ViewModels;

namespace SmartStudyPlanner.Views
{
    public partial class FocusWindow : Window
    {
        public FocusWindow(TaskDashboardItem task)
        {
            InitializeComponent();
            var vm = new FocusViewModel(task);

            // Khi bấm Hoàn thành, tự động đóng cửa sổ
            vm.OnKetThuc = () => this.Close();

            DataContext = vm;
        }
    }
}