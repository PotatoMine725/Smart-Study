using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 4 — N-10 (plan §17): a <c>MutationRejectedException</c> is never
    /// caught in production code. The OD-7 mechanism the owner chose (R2) restores the graph BEFORE
    /// the executor throws, and the surfacing site (<c>App.DispatcherUnhandledException</c>) recognises
    /// the rejection with an <c>is</c>/type test on an exception that already reached the global
    /// handler -- neither is a catch, so the allowlist of catch sites is EMPTY. Whole production
    /// project, not just <c>Sync/Fence</c> (which <c>FenceSourceFenceTests</c> already scans).
    /// </summary>
    public class MutationRejectedCatchSourceScanTests
    {
        // `catch (MutationRejectedException ...)`, `catch (Sync.Fence.MutationRejectedException)`,
        // and an exception filter that singles it out: `catch (Exception e) when (e is MutationRejectedException)`.
        private static readonly Regex CatchesRejection = new(
            @"catch\s*\([^)]*MutationRejectedException[^)]*\)" +
            @"|catch\s*\([^)]*\)\s*when\s*\([^)]*MutationRejectedException",
            RegexOptions.Compiled);

        private static readonly IReadOnlyCollection<string> AllowedCatchSites = Array.Empty<string>();

        /// <summary>The production project directories: Core, then the WPF app.</summary>
        private static string[] ProductionRoots()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SmartStudyPlanner.slnx")))
                dir = dir.Parent;

            Assert.NotNull(dir);
            var roots = new[] { Path.Combine(dir!.FullName, "SmartStudyPlanner.Core"), Path.Combine(dir.FullName, "SmartStudyPlanner") };
            foreach (var root in roots) Assert.True(Directory.Exists(root), root);
            return roots;
        }

        private static IReadOnlyList<string> ProductionSourceFiles(string root)
        {
            var sep = Path.DirectorySeparatorChar;
            return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{sep}obj{sep}", StringComparison.Ordinal)
                         && !f.Contains($"{sep}bin{sep}", StringComparison.Ordinal))
                .ToList();
        }

        [Fact]
        public void N10_NoProductionCodeCatchesMutationRejectedException()
        {
            var roots = ProductionRoots();
            var files = roots.SelectMany(ProductionSourceFiles).ToList();
            Assert.True(files.Count > 50, $"scanner found only {files.Count} files -- wrong root?");
            Assert.True(ProductionSourceFiles(roots[0]).Count > 50, $"expected the full Core tree under {roots[0]}");
            Assert.True(ProductionSourceFiles(roots[1]).Count > 10, $"expected the full WPF tree under {roots[1]}");

            var offenders = files
                .Where(f => !AllowedCatchSites.Contains(Path.GetFileName(f)))
                .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
                .Where(x => CatchesRejection.IsMatch(x.line))
                .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}: {x.line.Trim()}")
                .ToList();

            Assert.Empty(offenders);
        }

        [Theory]
        [InlineData("catch (MutationRejectedException ex)")]
        [InlineData("catch (Sync.Fence.MutationRejectedException)")]
        [InlineData("} catch (Exception e) when (e is MutationRejectedException) {")]
        public void TheScanner_ActuallyMatchesAPlantedCatch(string planted) =>
            Assert.Matches(CatchesRejection, planted);

        [Theory]
        [InlineData("if (LocalSaveRejection.TryFind(args.Exception, out var rejected))")]
        [InlineData("catch (Exception restoreFailure)")]
        [InlineData("catch (Exception)")]
        public void TheScanner_DoesNotFlagTheSurfacingAndRollbackShapes(string line) =>
            Assert.DoesNotMatch(CatchesRejection, line);
    }
}
