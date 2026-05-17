using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests
{
    public class UxViewModelTests
    {
        [Fact]
        public void TaskReferenceLinkItemVm_DomainHost_ExtractsHost()
        {
            var vm = new TaskReferenceLinkItemVm
            {
                Url = "https://learn.microsoft.com/dotnet"
            };

            Assert.Equal("learn.microsoft.com", vm.DomainHost);
        }

        [Fact]
        public void TaskReferenceLinkItemVm_DomainHost_InvalidUrl_ReturnsFallback()
        {
            var vm = new TaskReferenceLinkItemVm
            {
                Url = "not-a-valid-url"
            };

            Assert.Equal("Liên kết không hợp lệ", vm.DomainHost);
        }
    }
}

