using SmartStudyPlanner.Models;
using SmartStudyPlanner.ViewModels;
using System.Windows;

namespace SmartStudyPlanner.Views
{
    public partial class WorkloadBalancerWindow : Window
    {
        public WorkloadBalancerWindow(HocKy hocKy)
        {
            InitializeComponent();
            this.DataContext = new WorkloadBalancerViewModel(hocKy);
        }
    }
}