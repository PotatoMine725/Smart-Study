using SmartStudyPlanner.Models;
using SmartStudyPlanner.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SmartStudyPlanner
{
    public partial class AnalyticsPage : Page
    {
        private readonly AnalyticsViewModel _vm;

        public HocKy HocKy => _vm.HocKy;

        public AnalyticsPage(HocKy hocKy)
        {
            InitializeComponent();
            _vm = new AnalyticsViewModel(hocKy);
            this.DataContext = _vm;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await _vm.LoadAsync();
        }
    }
}
