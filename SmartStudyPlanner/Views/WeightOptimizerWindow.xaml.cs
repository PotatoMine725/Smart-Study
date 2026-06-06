using SmartStudyPlanner.ViewModels;
using System.Windows;

namespace SmartStudyPlanner.Views
{
    public partial class WeightOptimizerWindow : Window
    {
        public WeightOptimizerWindow()
        {
            InitializeComponent();
            var vm = new WeightOptimizerViewModel();
            DataContext = vm;
            Loaded += async (_, _) => await vm.LoadSuggestionCommand.ExecuteAsync(null);
        }
    }
}
