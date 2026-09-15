using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SmartStudyPlanner.Sync.Fence;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 2 (mission §20; plan §13, §16.2 X-4/X-9). Source-level guards that prevent
    /// a future accidental migration of mutation/execution behaviour into <c>Sync/Fence/</c>, and that
    /// no semantic priority among conflict shapes/kinds is ever introduced. Same idiom as
    /// <c>SyncApplyAuditFenceTests</c>: every scanner carries a non-vacuity self-check so it cannot
    /// silently stop matching anything.
    /// </summary>
    public class FenceSourceFenceTests
    {
        // Bare method-name identifiers that must never appear in Sync/Fence production code.
        private static readonly Regex ForbiddenBareCall = new(
            @"\b(SaveChanges\w*|MarkSyncApplied|StageAsync|MarkResolvedAsync|UpsertAsync|ExecuteSql\w*|BeginTransaction\w*)\s*\(",
            RegexOptions.Compiled);

        // EF mutation calls against one of the six synced DbSets or the two bookkeeping DbSets.
        private static readonly Regex ForbiddenDbSetMutation = new(
            @"\b(HocKys|MonHocs|StudyTasks|StudyLogs|TaskNotes|TaskReferenceLinks|SyncConflictRecords|SyncBaseSnapshots)" +
            @"\.(Add|AddRange|Update|UpdateRange|Remove|RemoveRange|Attach|AttachRange)\s*\(",
            RegexOptions.Compiled);

        // A sort/comparer keyed on ConflictShape or ConflictKind -- the one thing spec §7.2/mission §8
        // forbids as a GLOBAL priority. RoutingStage/Stage-keyed ordering (deterministic reporting only,
        // mission §7) is explicitly NOT this.
        private static readonly Regex ShapeOrKindPriorityOrdering = new(
            @"(OrderBy|OrderByDescending|ThenBy|ThenByDescending)\s*\([^)]*\.(Shape|Kind)\b" +
            @"|IComparer\s*<\s*Conflict(Shape|Kind)\s*>",
            RegexOptions.Compiled);

        private static readonly Regex CatchMutationRejected = new(
            @"catch\s*\([^)]*MutationRejectedException", RegexOptions.Compiled);

        private static string FenceRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SmartStudyPlanner.slnx")))
                dir = dir.Parent;

            Assert.NotNull(dir);
            var root = Path.Combine(dir!.FullName, "SmartStudyPlanner", "Sync", "Fence");
            Assert.True(Directory.Exists(root), root);
            return root;
        }

        private static IReadOnlyList<string> FenceSourceFiles() =>
            Directory.EnumerateFiles(FenceRoot(), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .ToList();

        [Fact]
        public void Scanner_IsNotVacuous_CoversAFullSetOfFenceFiles()
        {
            var files = FenceSourceFiles();
            Assert.True(files.Count >= 15, $"expected the full Slice 1+2 Sync/Fence tree, scanned {files.Count} files");
        }

        // ------------------------------------------------------------------ X-9: read-only (no forbidden write API)

        [Fact]
        public void SyncFence_ContainsNoBareForbiddenCall()
        {
            var hits = ScanFor(ForbiddenBareCall);
            Assert.Equal(Array.Empty<string>(), hits.Select(h => $"{h.File}: {h.Line}").ToArray());
        }

        [Fact]
        public void SyncFence_ContainsNoDbSetMutationCall()
        {
            var hits = ScanFor(ForbiddenDbSetMutation);
            Assert.Equal(Array.Empty<string>(), hits.Select(h => $"{h.File}: {h.Line}").ToArray());
        }

        [Fact]
        public void ForbiddenCallScanners_AreNotVacuous_AndDoNotFlagOrdinaryListAdd()
        {
            Assert.Matches(ForbiddenBareCall, "await ctx.SaveChangesAsync();");
            Assert.Matches(ForbiddenBareCall, "SyncConflictRecordStore.StageAsync(db, candidate);");
            Assert.Matches(ForbiddenDbSetMutation, "db.StudyTasks.Remove(x)");
            Assert.Matches(ForbiddenDbSetMutation, "ctx.SyncConflictRecords.Add(row)");

            // The negative case that keeps this scanner honest: ordinary in-memory list building
            // (exactly what ImpactResolver/ConflictDependencySelector do) must NOT match.
            Assert.DoesNotMatch(ForbiddenDbSetMutation, "rows.Add(new ImpactRow(...))");
            Assert.DoesNotMatch(ForbiddenBareCall, "list.Add(row); scopeKeys.Add(key);");
        }

        // ------------------------------------------------------------------ X-4: no semantic priority

        [Fact]
        public void SyncFence_ContainsNoOrderingKeyedOnConflictShapeOrKind()
        {
            var hits = ScanFor(ShapeOrKindPriorityOrdering);
            Assert.Equal(Array.Empty<string>(), hits.Select(h => $"{h.File}: {h.Line}").ToArray());
        }

        [Fact]
        public void ShapeOrKindPriorityScanner_IsNotVacuous_AndDoesNotFlagStageOrdering()
        {
            Assert.Matches(ShapeOrKindPriorityOrdering, "results.OrderBy(r => r.Shape)");
            Assert.Matches(ShapeOrKindPriorityOrdering, "class X : IComparer<ConflictShape>");

            // Sorting by RoutingStage/Stage for deterministic reporting is explicitly allowed
            // (mission §7, spec §7.1 step 8) and must not be flagged as a priority.
            Assert.DoesNotMatch(ShapeOrKindPriorityOrdering, "results.OrderBy(r => r.Stage).ThenBy(r => r.ScopeKey, StringComparer.Ordinal).ThenBy(r => r.ConflictId)");
        }

        // ------------------------------------------------------------------ X-5: no MutationOrigin outside MutationRequest.cs

        // Files allowed to mention "MutationOrigin" at all: the enum's own definition
        // (MutationRequest.cs), and a Slice-1 doc comment on the interface explaining the invariant
        // in prose (IConflictFencePolicy.cs: "...never takes a MutationOrigin (no local bypass)").
        // Neither declares a parameter, field, or local of that type outside a comment.
        private static readonly HashSet<string> FilesAllowedToMentionMutationOrigin =
            new(StringComparer.Ordinal) { "MutationRequest.cs", "IConflictFencePolicy.cs" };

        [Fact]
        public void MutationOrigin_IsReferencedOnlyWhereExpected_NeverAsAnActualPolicyOrRouterParameter()
        {
            var files = FenceSourceFiles();
            var referencing = files
                .Where(f => File.ReadAllText(f).Contains("MutationOrigin", StringComparison.Ordinal))
                .Select(f => Path.GetFileName(f)!)
                .ToArray();

            Assert.Equal(FilesAllowedToMentionMutationOrigin, referencing.ToHashSet(StringComparer.Ordinal));

            // Positive proof it is not just absent from the SOURCE of the Slice-2 files -- the method
            // signatures of the Slice-2 orchestration types are checked by reflection: none of them
            // declares a MutationOrigin parameter or property.
            foreach (var type in new[] { typeof(ImpactResolver), typeof(ConflictDependencySelector), typeof(FenceRouter) })
            {
                foreach (var method in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                                                      | System.Reflection.BindingFlags.NonPublic))
                {
                    foreach (var p in method.GetParameters())
                        Assert.NotEqual(typeof(SmartStudyPlanner.Sync.Fence.MutationOrigin), p.ParameterType);
                }
            }
        }

        // ------------------------------------------------------------------ N-10: rejection is not swallowed here

        [Fact]
        public void SyncFence_NeverCatchesMutationRejectedException()
        {
            var hits = ScanFor(CatchMutationRejected);
            Assert.Equal(Array.Empty<string>(), hits.Select(h => $"{h.File}: {h.Line}").ToArray());
        }

        [Fact]
        public void CatchMutationRejectedScanner_IsNotVacuous()
        {
            Assert.Matches(CatchMutationRejected, "catch (MutationRejectedException ex)");
            Assert.Matches(CatchMutationRejected, "catch(SmartStudyPlanner.Sync.Fence.MutationRejectedException e)");
            Assert.DoesNotMatch(CatchMutationRejected, "catch (InvalidOperationException ex)");
        }

        private static IEnumerable<(string File, string Line)> ScanFor(Regex pattern)
        {
            foreach (var file in FenceSourceFiles())
            {
                foreach (var line in File.ReadLines(file))
                {
                    if (pattern.IsMatch(line)) yield return (file, line.Trim());
                }
            }
        }
    }
}
