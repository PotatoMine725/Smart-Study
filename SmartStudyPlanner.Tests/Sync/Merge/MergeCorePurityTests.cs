using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Merge
{
    /// <summary>
    /// E-1 / E-2 / E-4 plus the D1 purity guard. The pure core is only worth having if it is
    /// provably pure and provably deterministic, so both are asserted mechanically rather than
    /// documented.
    /// </summary>
    public class MergeCorePurityTests
    {
        private const string MergeNamespace = "SmartStudyPlanner.Sync.Merge";

        private static string MergeSourceDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SmartStudyPlanner.slnx")))
                dir = dir.Parent;

            Assert.NotNull(dir);
            var path = Path.Combine(dir!.FullName, "SmartStudyPlanner", "Sync", "Merge");
            Assert.True(Directory.Exists(path), $"Merge source directory not found at '{path}'.");
            return path;
        }

        private static IEnumerable<(string File, string Text)> MergeSources() =>
            Directory.EnumerateFiles(MergeSourceDir(), "*.cs", SearchOption.AllDirectories)
                     .Select(f => (Path.GetFileName(f), File.ReadAllText(f)));

        // The core's own doc comments name the forbidden dependencies in order to forbid them, so
        // the guard has to look at CODE. No string literal in the core contains "//" or "/*".
        private static IEnumerable<(string File, string Code)> MergeCode() =>
            MergeSources().Select(s => (s.File,
                Regex.Replace(Regex.Replace(s.Text, @"/\*.*?\*/", " ", RegexOptions.Singleline),
                              @"//[^\r\n]*", " ")));

        // ---- D1 purity, enforced on the source text ------------------------------------------

        [Theory]
        [InlineData("DbContext")]
        [InlineData("AppDbContext")]
        [InlineData("Microsoft.EntityFrameworkCore")]
        [InlineData("SaveChanges")]
        [InlineData("SyncStamper")]
        [InlineData("SyncBaseSnapshotStore")]
        [InlineData("SyncChangeEnumerator")]
        [InlineData("SmartStudyPlanner.Models")]
        [InlineData("System.Net")]
        [InlineData("System.Windows")]
        public void MergeCoreSource_ContainsNoForbiddenDependency(string forbidden)
        {
            var offenders = MergeCode()
                .Where(s => s.Code.Contains(forbidden, StringComparison.Ordinal))
                .Select(s => s.File)
                .ToArray();

            Assert.Equal(Array.Empty<string>(), offenders);
        }

        [Fact] // CRITICAL RULE: no machine-local timezone conversion anywhere in Sync/Merge
        public void MergeCoreSource_NeverConvertsTimeZones_AndNeverReadsAClock()
        {
            var banned = new[] { "ToLocalTime", "ToUniversalTime", "DateTime.Now", "DateTime.UtcNow",
                                 "DateTimeOffset.Now", "DateTimeOffset.UtcNow", "TimeZoneInfo" };

            var offenders = MergeCode()
                .SelectMany(s => banned.Where(b => s.Code.Contains(b, StringComparison.Ordinal))
                                       .Select(b => $"{s.File}: {b}"))
                .ToArray();

            Assert.Equal(Array.Empty<string>(), offenders);
        }

        [Fact] // the guard above is only meaningful if it actually scans files
        public void PurityGuard_ActuallyReadsTheMergeSources()
        {
            var files = MergeSources().ToList();
            Assert.True(files.Count >= 8, $"expected the whole Merge core, saw {files.Count} file(s)");
            Assert.Contains(files, f => f.File == "ThreeWayMerge.cs");
            Assert.Contains(files, f => f.Text.Contains("ConflictKind", StringComparison.Ordinal));
        }

        [Fact] // no type in the merge namespace may reference an EF or WPF assembly
        public void MergeCoreTypes_ReferenceNoEfOrUiTypesInTheirSignatures()
        {
            var types = typeof(ThreeWayMerge).Assembly.GetTypes()
                .Where(t => t.Namespace == MergeNamespace)
                .ToList();
            Assert.NotEmpty(types);

            var offenders = new List<string>();
            foreach (var t in types)
            {
                var referenced = t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance
                                              | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
                    .Concat(t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.PropertyType))
                    .Distinct();

                foreach (var rt in referenced)
                {
                    var asm = rt.Assembly.GetName().Name ?? "";
                    var ns = rt.Namespace ?? "";
                    if (asm.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                        || ns.StartsWith("SmartStudyPlanner.Models", StringComparison.Ordinal)
                        || ns.StartsWith("SmartStudyPlanner.Data", StringComparison.Ordinal)
                        || ns.StartsWith("System.Windows", StringComparison.Ordinal))
                    {
                        offenders.Add($"{t.Name} -> {rt.FullName}");
                    }
                }
            }

            Assert.Equal(Array.Empty<string>(), offenders.ToArray());
        }

        [Fact] // the two entry points are static (no instance state to leak between calls)
        public void EntryPoints_AreStaticAndTakeNoClockOrDeviceId()
        {
            Assert.True(typeof(ThreeWayMerge).IsAbstract && typeof(ThreeWayMerge).IsSealed);
            Assert.True(typeof(ConstraintMerge).IsAbstract && typeof(ConstraintMerge).IsSealed);

            var merge = typeof(ThreeWayMerge).GetMethod(nameof(ThreeWayMerge.Merge))!;
            Assert.True(merge.IsStatic);
            Assert.Equal(new[] { typeof(EntityRef), typeof(EntitySnapshot), typeof(EntitySnapshot), typeof(EntitySnapshot) },
                         merge.GetParameters().Select(p => p.ParameterType));

            var detect = typeof(ConstraintMerge).GetMethod(nameof(ConstraintMerge.Detect))!;
            Assert.True(detect.IsStatic);
            Assert.Equal(typeof(ConstraintScopeInput), Assert.Single(detect.GetParameters()).ParameterType);
        }

        [Fact] // no mutable static state anywhere in the core (rules out memoisation/caches)
        public void MergeCoreTypes_HoldNoMutableStaticState()
        {
            var offenders = typeof(ThreeWayMerge).Assembly.GetTypes()
                .Where(t => t.Namespace == MergeNamespace)
                // the compiler's own lambda/closure caches are not authored state
                .Where(t => !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
                .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                  .Where(f => !f.IsInitOnly && !f.IsLiteral)
                                  .Select(f => $"{t.Name}.{f.Name}"))
                .ToArray();

            Assert.Equal(Array.Empty<string>(), offenders);
        }

        // ---- E-1 / E-2: referential transparency and input-order independence ----------------

        [Fact] // E-1
        public void Merge_IsReferentiallyTransparent_AcrossRepeatedCalls()
        {
            var b = MergeTestData.Snap(SyncEntityTypes.StudyTask, MergeTestData.Live(100, "d0"));
            var l = b.With("TenTask", new StringValue("L")).WithProv(MergeTestData.Live(200, "d1"));
            var r = b.With("TenTask", new StringValue("R")).WithProv(MergeTestData.Live(300, "d2"));
            var reference = MergeTestData.Ref(SyncEntityTypes.StudyTask, MergeTestData.G3);

            var first = ThreeWayMerge.Merge(reference, b, l, r);
            for (var i = 0; i < 10; i++)
            {
                var again = ThreeWayMerge.Merge(reference, b, l, r);
                Assert.Equal(CanonicalJson.Write(first.Result!), CanonicalJson.Write(again.Result!));
                Assert.Equal(first.ResultProvenance, again.ResultProvenance);
                Assert.Equal(first.Outcomes.Select(o => (o.Field, o.Decision)),
                             again.Outcomes.Select(o => (o.Field, o.Decision)));
                Assert.Equal(first.Candidates.Select(ConflictKeys.ConflictKey),
                             again.Candidates.Select(ConflictKeys.ConflictKey));
            }
        }

        [Theory] // E-2 — shuffling the input dictionaries changes nothing
        [MemberData(nameof(MergeTestData.AllEntityTypes), MemberType = typeof(MergeTestData))]
        public void Merge_IsIndependentOfInputFieldOrder(string entityType)
        {
            var field = MergeTestData.FirstMergeField(entityType);
            var b = MergeTestData.Snap(entityType, MergeTestData.Live(100, "d0"));
            var l = b.With(field, MergeTestData.Bump(b.Fields[field])).WithProv(MergeTestData.Live(200, "d1"));
            var r = b.WithProv(MergeTestData.Live(300, "d2"));
            var reference = MergeTestData.Ref(entityType, MergeTestData.G3);

            var ordered = ThreeWayMerge.Merge(reference, b, l, r);
            var shuffled = ThreeWayMerge.Merge(reference, b.Shuffled(1), l.Shuffled(2), r.Shuffled(3));

            Assert.Equal(CanonicalJson.Write(ordered.Result!), CanonicalJson.Write(shuffled.Result!));
            Assert.Equal(ordered.Result, shuffled.Result);
        }

        [Fact] // E-2b — canonical output order is the registry's, never the dictionary's
        public void CanonicalOutput_IgnoresDictionaryEnumerationOrder()
        {
            var snap = MergeTestData.Snap(SyncEntityTypes.StudyTask, MergeTestData.Live(1, "d"));
            for (var seed = 1; seed <= 5; seed++)
                Assert.Equal(CanonicalJson.Write(snap), CanonicalJson.Write(snap.Shuffled(seed)));
        }

        [Fact] // inputs are never mutated by the core
        public void Merge_DoesNotMutateItsInputs()
        {
            var b = MergeTestData.Snap(SyncEntityTypes.StudyTask, MergeTestData.Live(100, "d0"));
            var l = b.With("TenTask", new StringValue("L")).WithProv(MergeTestData.Live(200, "d1"));
            var r = b.With("TenTask", new StringValue("R")).WithProv(MergeTestData.Live(300, "d2"));

            var bBefore = CanonicalJson.Write(b);
            var lBefore = CanonicalJson.Write(l);
            var rBefore = CanonicalJson.Write(r);

            ThreeWayMerge.Merge(MergeTestData.Ref(SyncEntityTypes.StudyTask, MergeTestData.G3), b, l, r);

            Assert.Equal(bBefore, CanonicalJson.Write(b));
            Assert.Equal(lBefore, CanonicalJson.Write(l));
            Assert.Equal(rBefore, CanonicalJson.Write(r));
        }

        [Fact] // regression fence for the merge-surface freeze: no new file may sneak in silently
        public void MergeCore_ConsistsOfTheExpectedFiles()
        {
            var files = MergeSources().Select(s => s.File).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[]
            {
                "CanonicalJson.cs", "ConflictKeys.cs", "ConstraintMerge.cs", "Lww.cs",
                "MergeDtos.cs", "MergeExceptions.cs", "MergeOutcomes.cs",
                "MergeSurfaceRegistry.cs", "ThreeWayMerge.cs"
            }, files);
        }
    }
}
