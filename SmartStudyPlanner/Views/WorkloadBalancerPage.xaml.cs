using SmartStudyPlanner.Models;
using SmartStudyPlanner.ViewModels;
using System.Windows.Controls;

namespace SmartStudyPlanner.Views
{
    public partial class WorkloadBalancerPage : Page
    {
        public HocKy HocKy { get; }

        public WorkloadBalancerPage(HocKy hocKy)
        {
            InitializeComponent();
            HocKy = hocKy;
            this.DataContext = new WorkloadBalancerViewModel(hocKy);
        }
    }
}
