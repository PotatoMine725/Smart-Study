using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 5 — X-19 (plan §16.2, §13 role table): tracker writes to the six synced
    /// tables, and FK reassignments, happen only in the allowlisted writer files. A mutation added
    /// anywhere else (a VM, a new repository) would bypass the fence without anything going red.
    /// <para>
    /// What is matched, per production line:
    /// <list type="bullet">
    /// <item>a DbSet-typed write — <c>.HocKys/.MonHocs/.StudyTasks/.StudyLogs/.TaskNotes/.TaskReferenceLinks</c>
    /// then <c>.Add/AddRange/Update/UpdateRange/Remove/RemoveRange/Attach/AttachRange(</c>;</item>
    /// <item>the generic form on an <c>AppDbContext</c> receiver named <c>db</c>, <c>ctx</c>, <c>context</c>
    /// or <c>.Db</c> (the two sync sites write that way);</item>
    /// <item>a member-access FK assignment <c>x.MaMonHoc =</c> / <c>x.MaHocKy =</c> (not <c>==</c>, not an
    /// object initializer: the model constructors set those PKs themselves).</item>
    /// </list>
    /// Known blind spots, reported rather than scanned (Slice 5 report §3d): raw SQL
    /// (<c>ConflictStaging</c>'s M5 delete — fenced by <c>SyncApplyAuditFenceTests</c> — and
    /// <c>AppStartup</c>'s <c>UPDATE HocKys SET IsSeeded</c>), <c>Entry(..).State =</c>
    /// (<c>SyncStamper</c>, <c>SyncBaseSnapshotStore</c>), generic writes on receivers with other names,
    /// <c>ExecuteUpdate</c>, and <c>.MaTask =</c> (the editor's item VM assigns a property of that name).
    /// </para>
    /// </summary>
    public class SyncedEntityWritePathFenceTests
    {
        private static readonly Regex SyncedWrite = new(
            @"\.(HocKys|MonHocs|StudyTasks|StudyLogs|TaskNotes|TaskReferenceLinks)\s*\.\s*(Add|AddRange|Update|UpdateRange|Remove|RemoveRange|Attach|AttachRange)\s*\(" +
            @"|(\bdb|\bctx|\bcontext|\.Db)\s*\.\s*(Add|AddRange|Update|UpdateRange|Remove|RemoveRange|Attach|AttachRange)\s*\(" +
            @"|\.\s*(MaMonHoc|MaHocKy)\s*=(?![=>])",
            RegexOptions.Compiled);

        /// <summary>Paths relative to the production project root, '/'-separated.</summary>
        private static readonly IReadOnlyDictionary<string, string> Allowlist = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Infrastructure/Persistence/SQLite/Mutations/SemesterGraphWriter.cs"] = "local semester writer (Slice 3/4), runs only after a passed fence",
            ["Infrastructure/Persistence/SQLite/Mutations/TaskEditorWriter.cs"] = "local task-editor writer (Slice 5), runs only after a passed fence",
            ["Infrastructure/Persistence/SQLite/TaskCascadeHelper.cs"] = "cascade of a task tombstone already decided by the semester fence",
            ["Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs"] = "read-time MonHoc dedup re-points task FKs in the returned graph",
            ["Sync/Apply/SyncApplySession.cs"] = "sync apply (Slice 6, gated on OD-1)",
            ["Sync/Apply/ConflictResolver.cs"] = "conflict resolution writes",
            ["Sync/Apply/EntitySnapshotMapper.cs"] = "snapshot -> entity mapping for sync apply",
            ["Infrastructure/Persistence/SQLite/Repositories/SqliteStudyTaskRepository.cs"] = "OD-5 (a): zero production callers, held by the OD-5 guard",
            ["Infrastructure/Persistence/SQLite/Repositories/SqliteStudyLogRepository.cs"] =
                "owner 2026-10-16: append-only focus-session log, outside the fence by design (StudyLog.MaTask is not fence-routable)",
        };

        [Fact]
        public void X19_SyncedWrites_OnlyInAllowlistedFiles()
        {
            var offenders = ScanProduction()
                .Where(hit => !Allowlist.ContainsKey(hit.RelativePath))
                .Select(hit => $"{hit.RelativePath}:{hit.LineNumber}: {hit.Line}")
                .ToList();

            Assert.Empty(offenders);
        }

        /// <summary>An allowlist entry with no hit is stale: it would silently admit a future write there.</summary>
        [Fact]
        public void X19_EveryAllowlistedFile_StillHasAWriteSite()
        {
            var filesWithHits = ScanProduction().Select(h => h.RelativePath).ToHashSet(StringComparer.Ordinal);

            Assert.Empty(Allowlist.Keys.Where(f => !filesWithHits.Contains(f)));
        }

        /// <summary>
        /// Non-vacuity (pattern of <c>SyncApplyAuditFenceTests.TheScanner_ActuallyMatchesTheKnownSite</c>):
        /// the scan reads the full production tree, finds the sites known to exist, and recognises every
        /// shape a new site could take — while not flagging the shapes that are legal.
        /// </summary>
        [Fact]
        public void TheScanner_ActuallyMatchesTheKnownSites()
        {
            var root = ProductionRoot();
            Assert.True(Directory.Exists(root), root);
            var files = SourceFiles(root).ToList();
            Assert.True(files.Count > 50, $"expected a full production tree, scanned {files.Count} files");

            var hits = ScanProduction().ToList();
            Assert.Contains(hits, h => h.RelativePath.EndsWith("SemesterGraphWriter.cs", StringComparison.Ordinal) && h.Line.Contains("db.StudyTasks.Remove", StringComparison.Ordinal));
            Assert.Contains(hits, h => h.RelativePath.EndsWith("SyncApplySession.cs", StringComparison.Ordinal) && h.Line.Contains("st.Db.Add", StringComparison.Ordinal));

            Assert.Matches(SyncedWrite, "db.StudyTasks.Remove(x);");                 // the plan's mutant, in a VM
            Assert.Matches(SyncedWrite, "ctx.TaskNotes.Add(new TaskNote());");
            Assert.Matches(SyncedWrite, "_db.TaskReferenceLinks .RemoveRange(links);");
            Assert.Matches(SyncedWrite, "db.MonHocs.Update(mon);");
            Assert.Matches(SyncedWrite, "st.Db.Add(tracked);");
            Assert.Matches(SyncedWrite, "db.Attach(task);");
            Assert.Matches(SyncedWrite, "task.MaMonHoc = other;");
            Assert.Matches(SyncedWrite, "mon.MaHocKy =hk;");

            Assert.DoesNotMatch(SyncedWrite, "db.DifficultyLabelLogs.Add(entry);");   // non-synced telemetry table
            Assert.DoesNotMatch(SyncedWrite, "MaMonHoc = MonHocHienTai.MaMonHoc,");    // object initializer
            Assert.DoesNotMatch(SyncedWrite, "if (t.MaMonHoc == m.MaMonHoc)");
            Assert.DoesNotMatch(SyncedWrite, "Func<StudyTask, Guid> f = t => t.MaMonHoc;");
            Assert.DoesNotMatch(SyncedWrite, "await db.TaskNotes.FirstOrDefaultAsync(n => n.MaTask == id);");
        }

        private static IEnumerable<(string RelativePath, int LineNumber, string Line)> ScanProduction()
        {
            var root = ProductionRoot();
            foreach (var file in SourceFiles(root))
            {
                var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                var lineNumber = 0;
                foreach (var line in File.ReadLines(file))
                {
                    lineNumber++;
                    // Whole-line `//` comments (doc comments included) name these APIs in prose; they are
                    // not write sites. A trailing comment is not stripped: "//" also occurs inside URLs.
                    if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                    if (SyncedWrite.IsMatch(line)) yield return (relative, lineNumber, line.Trim());
                }
            }
        }

        private static IEnumerable<string> SourceFiles(string root) =>
            Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                              && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        /// <summary>The production project directory, by EXACT name (a prefix would pull the test project in).</summary>
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
