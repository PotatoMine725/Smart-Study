using System;
using System.Linq;
using System.Reflection;
using SmartStudyPlanner.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Core
{
    /// <summary>
    /// Mobile spec INV-1 / INV-2: <c>SmartStudyPlanner.Core</c> references no WPF/WinForms assembly and no ML.NET.
    /// Asserted on the compiled metadata, so a stray <c>using</c> in a moved file cannot slip past a grep.
    /// </summary>
    public class CoreAssemblyPurityTests
    {
        private static readonly string[] ForbiddenExact =
            { "PresentationFramework", "PresentationCore", "WindowsBase", "System.Windows.Forms" };

        private static string[] Offenders(Assembly assembly) =>
            assembly.GetReferencedAssemblies()
                .Select(a => a.Name ?? "")
                .Where(n => ForbiddenExact.Contains(n, StringComparer.Ordinal)
                         || n.StartsWith("Microsoft.ML", StringComparison.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

        [Fact]
        public void CoreAssembly_ReferencesNoWpfWinFormsOrMlNet()
        {
            var core = typeof(ThreeWayMerge).Assembly;
            Assert.Equal("SmartStudyPlanner.Core", core.GetName().Name);

            // Self-check: the same probe does flag the WPF app, which really references WPF and ML.NET.
            var wpfOffenders = Offenders(typeof(App).Assembly);
            Assert.Contains("PresentationFramework", wpfOffenders);
            Assert.Contains(wpfOffenders, n => n.StartsWith("Microsoft.ML", StringComparison.Ordinal));

            Assert.Equal(Array.Empty<string>(), Offenders(core));
        }
    }
}
