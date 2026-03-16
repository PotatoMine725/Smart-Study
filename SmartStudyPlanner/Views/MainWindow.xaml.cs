using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using System;
using System.Windows;

namespace SmartStudyPlanner
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            HocKy hocKyCu = DataManager.DocHocKy();

            if (hocKyCu != null)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"Tìm thấy dữ liệu của '{hocKyCu.Ten}'. Bạn có muốn tiếp tục quản lý học kỳ này không?",
                    "Phát hiện dữ liệu cũ", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // ĐỔI KÊNH SANG DASHBOARD
                    MainFrame.Navigate(new DashboardPage(hocKyCu));
                }
                else
                {
                    // ĐỔI KÊNH SANG SETUP
                    MainFrame.Navigate(new SetupPage());
                }
            }
            else
            {
                MainFrame.Navigate(new SetupPage());
            }
        }
    }
}