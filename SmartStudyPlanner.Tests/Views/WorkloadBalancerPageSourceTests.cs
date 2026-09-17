using System;
using System.IO;
using System.Text.RegularExpressions;
using SmartStudyPlanner.Tests.Services.Soe;   // RepoLocator (internal, cùng assembly)
using Xunit;

namespace SmartStudyPlanner.Tests.Views
{
    /// <summary>
    /// Lỗi gốc nằm trong BINDING và trong CHUỖI hiển thị — không unit test ViewModel nào chứng
    /// minh được XAML trỏ đúng property hay bản copy mô tả đúng thuật toán. Dùng lại tiền lệ
    /// source-assertion có sẵn trong repo: ObjectiveEvaluatorTests
    /// .SourceFiles_ContainNoHanChotOrDeadlineToken.
    /// </summary>
    public sealed class WorkloadBalancerPageSourceTests
    {
        private static string ReadRepoFile(params string[] parts)
        {
            string path = Path.Combine(RepoLocator.FindRepoRoot(), Path.Combine(parts));
            Assert.True(File.Exists(path), $"Không tìm thấy file: {path}");
            return File.ReadAllText(path);
        }

        private static string Xaml() =>
            ReadRepoFile("SmartStudyPlanner", "Views", "WorkloadBalancerPage.xaml");

        [Fact]
        public void Xaml_MoiBindingDoLuong_DeuTroVaoRenderedCapacityHours()
        {
            string xaml = Xaml();

            // (a) Chặn âm: mọi tham chiếu qua ancestor đều phải là RenderedCapacityHours; hai
            //     binding cố ý giữ giá trị sống (slider :68, số 38pt :56) là
            //     {Binding CapacityHours} trần, KHÔNG có tiền tố "DataContext.". Nên khẳng định
            //     này chính xác, và đỏ nếu có converter binding nào bị trỏ ngược về giá trị sống.
            Assert.DoesNotContain("DataContext.CapacityHours", xaml, StringComparison.Ordinal);

            // (b) Chặn dương. Chỉ có (a) thì XOÁ sạch năm binding cũng xanh — đúng loại lỗ hổng
            //     mà probe M5 của automated gate đã phơi ra. Đếm cụ thể mới chứng minh được
            //     tiêu chí nghiệm thu 3, và tiện thể phủ luôn dòng caption đường nét đứt mà
            //     design §5.2 ghi là "không guard được" (dạng binding của nó không phân biệt
            //     được với hai binding phải giữ sống).
            Assert.Equal(5, Regex.Matches(xaml, @"Path=""DataContext\.RenderedCapacityHours""").Count);
            Assert.Equal(2, Regex.Matches(xaml, @"Path=""RenderedCapacityHours""").Count);   // caption + badge
        }

        [Fact]
        public void Xaml_KhongConMoTaLuatXepLichCu()
        {
            string xaml = Xaml();

            foreach (var token in new[] { "đều khắp", "ít tải nhất" })
            {
                Assert.False(
                    xaml.Contains(token, StringComparison.Ordinal),
                    $"WorkloadBalancerPage.xaml còn chuỗi '{token}' — mô tả luật least-load mà T3.3 đã thay bằng ngày-sớm-nhất-còn-chỗ.");
            }
        }

        [Fact]
        public void IWorkloadService_DocComment_KhongConNoiLeastLoadHayChanHorizon7Ngay()
        {
            string src = ReadRepoFile("SmartStudyPlanner", "Services", "IWorkloadService.cs");

            foreach (var token in new[] { "Least-Load", "7 ngày" })
            {
                Assert.False(
                    src.Contains(token, StringComparison.Ordinal),
                    $"IWorkloadService.cs còn chuỗi '{token}' — hợp đồng scheduling mô tả sai sau T3.3 (không còn least-load, cũng không chốt 7 ngày).");
            }
        }
    }
}
