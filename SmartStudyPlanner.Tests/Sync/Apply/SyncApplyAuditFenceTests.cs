using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Apply
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-5) — the audit fence for the ONE approved Epic-1 hard-delete exception
    /// (DoR §9.2 mechanism M5, owner ACK A1). The exception is safe only while it stays exactly one
    /// site: a second physical delete anywhere in production would reintroduce hard deletes on synced
    /// data under cover of an approval that was granted for a single row class.
    /// <para>
    /// Only the PRODUCTION project directory is scanned, resolved by exact name — not by prefix, which
    /// would also match <c>SmartStudyPlanner.Tests</c>, whose own raw-SQL trigger tests legitimately
    /// contain the same literal and would make this fence either self-failing or (if loosened to
    /// compensate) vacuous.
    /// </para>
    /// </summary>
    public class SyncApplyAuditFenceTests
    {
        private const string AllowedFile = "ConflictStaging.cs";
        private const string AllowedMethod = "HardDeleteWithdrawnTaskNoteAsync";

        // "DELETE FROM" in any casing/spacing, plus EF's bulk-delete APIs.
        private static readonly Regex PhysicalDelete =
            new(@"delete\s+from|ExecuteDelete(Async)?\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [Fact]
        public void ProductionCode_ContainsExactlyOnePhysicalDeleteSite()
        {
            var hits = ScanProduction().ToList();

            Assert.Single(hits);
            Assert.Equal(AllowedFile, Path.GetFileName(hits[0].File));
            Assert.Contains("TaskNotes", hits[0].Line);
        }

        /// <summary>
        /// The one allowed site must live inside the named guarded method. Moving the statement out of it
        /// — into a general-purpose helper, say — would keep the count at one while dropping every
        /// precondition the approval depends on (evidence persisted first, TaskNote only, inside the
        /// session's transaction, target not tracked).
        /// </summary>
        [Fact]
        public void TheOnlyPhysicalDelete_SitsInsideTheGuardedMethod()
        {
            var hit = Assert.Single(ScanProduction());
            var text = File.ReadAllText(hit.File);

            var methodStart = text.IndexOf(AllowedMethod, StringComparison.Ordinal);
            Assert.True(methodStart >= 0, $"{AllowedMethod} not found in {hit.File}");

            var deleteAt = PhysicalDelete.Match(text).Index;
            Assert.True(deleteAt > methodStart,
                        $"the physical delete must appear inside {AllowedMethod}, not before it");

            // No other method may be declared between the guarded method's signature and the delete.
            var between = text.Substring(methodStart, deleteAt - methodStart);
            Assert.DoesNotContain("static Task", between);
            Assert.DoesNotContain("static async Task", between);
        }

        /// <summary>
        /// The fence is only worth anything if it can go red. This test states the mutation explicitly so
        /// the observation is reproducible: adding any second physical-delete statement under
        /// <c>SmartStudyPlanner/</c> must break
        /// <see cref="ProductionCode_ContainsExactlyOnePhysicalDeleteSite"/>. The scan below is the same
        /// one that test uses, so a scanner that silently matched nothing would fail here too.
        /// </summary>
        [Fact]
        public void TheScanner_ActuallyMatchesTheKnownSite()
        {
            var productionRoot = ProductionRoot();
            Assert.True(Directory.Exists(productionRoot), productionRoot);

            var files = SourceFiles(productionRoot).ToList();
            Assert.True(files.Count > 50, $"expected a full production tree, scanned {files.Count} files");

            // The scanner is not vacuous: it finds the site we know exists...
            Assert.NotEmpty(ScanProduction());

            // ...and it recognises the shapes a second site could take.
            Assert.Matches(PhysicalDelete, "db.Database.ExecuteSqlRaw(\"DELETE FROM StudyTasks\")");
            Assert.Matches(PhysicalDelete, "await db.TaskNotes.ExecuteDeleteAsync(ct)");
            Assert.Matches(PhysicalDelete, "delete   from  X");
            Assert.DoesNotMatch(PhysicalDelete, "db.TaskNotes.Remove(note)");   // soft delete stays legal
        }

        private static IEnumerable<(string File, string Line)> ScanProduction()
        {
            foreach (var file in SourceFiles(ProductionRoot()))
            {
                foreach (var line in File.ReadLines(file))
                {
                    if (PhysicalDelete.IsMatch(line)) yield return (file, line.Trim());
                }
            }
        }

        private static IEnumerable<string> SourceFiles(string root) =>
            Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                              && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        /// <summary>
        /// The production project directory, by EXACT name. <c>SmartStudyPlanner</c> and
        /// <c>SmartStudyPlanner.Tests</c> are siblings, so a prefix match would pull the test project in.
        /// </summary>
        private static string ProductionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SmartStudyPlanner.slnx")))
                dir = dir.Parent;

            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "SmartStudyPlanner");
        }
    }
}
