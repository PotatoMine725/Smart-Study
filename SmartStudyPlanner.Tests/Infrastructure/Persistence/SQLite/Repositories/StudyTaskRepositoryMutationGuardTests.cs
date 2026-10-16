using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Repositories
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 5 — OD-5 (a) (rulings 2026-10-03 §3): <c>IStudyTaskRepository</c>'s
    /// mutations (<c>AddAsync</c>/<c>UpdateAsync</c>/<c>DeleteAsync</c>) have zero production callers and
    /// are not routed through the fence. This guard keeps it that way: a production call would write a
    /// synced row past the fence, so it must fail here and be routed (or ruled) first.
    /// <para>
    /// Matched by RECEIVER TYPE, not by method name — other repositories (telemetry, logs) have
    /// <c>AddAsync</c> too. Per file: every identifier declared as <c>IStudyTaskRepository</c> /
    /// <c>SqliteStudyTaskRepository</c> (field, parameter, local, <c>var x = new SqliteStudyTaskRepository(..)</c>),
    /// every identifier assigned from the service locator (<c>var x = ServiceLocator.Get&lt;IStudyTaskRepository&gt;()</c>,
    /// also <c>GetRequiredService</c>/<c>GetService</c>; review #114 F-2), plus the receiver-less forms
    /// <c>Get&lt;IStudyTaskRepository&gt;()</c>, <c>GetRequiredService&lt;IStudyTaskRepository&gt;()</c> and
    /// <c>new SqliteStudyTaskRepository(..)</c> chained straight into a mutation.
    /// Known blind spots: a receiver obtained by a cast, or passed through another type or file.
    /// </para>
    /// </summary>
    public class StudyTaskRepositoryMutationGuardTests
    {
        private const string Mutation = @"\s*\??\.\s*(AddAsync|UpdateAsync|DeleteAsync)\s*\(";

        private static readonly Regex DeclaredReceiver = new(
            @"\b(?:IStudyTaskRepository|SqliteStudyTaskRepository)\??\s+(?<name>[A-Za-z_]\w*)\b(?!\s*\()" +
            @"|(?<name>[A-Za-z_]\w*)\s*=\s*new\s+SqliteStudyTaskRepository\s*\(" +
            @"|(?<name>[A-Za-z_]\w*)\s*=\s*(?:[\w.]+\s*\.\s*)?(?:Get|GetRequiredService|GetService)\s*<\s*IStudyTaskRepository\s*>\s*\(\s*\)",
            RegexOptions.Compiled);

        private static readonly Regex ReceiverlessCall = new(
            @"(?:Get|GetRequiredService|GetService)\s*<\s*IStudyTaskRepository\s*>\s*\(\s*\)\s*!?" + Mutation +
            @"|new\s+SqliteStudyTaskRepository\s*\([^;]*?\)" + Mutation,
            RegexOptions.Compiled);

        /// <summary>The offending lines of one source text (file-local receiver resolution).</summary>
        internal static IReadOnlyList<string> FindMutationCalls(string source)
        {
            var names = DeclaredReceiver.Matches(source)
                .Select(m => m.Groups["name"].Value)
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var onReceiver = names.Count == 0
                ? null
                : new Regex(@"(?<![\w.])(?:this\.)?(?:" + string.Join("|", names.Select(Regex.Escape)) + ")" + Mutation);

            return source.Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Where(l => ReceiverlessCall.IsMatch(l) || (onReceiver?.IsMatch(l) ?? false))
                .Select(l => l.Trim())
                .ToList();
        }

        [Fact]
        public void OD5_NoProductionCodeCallsStudyTaskRepositoryMutations()
        {
            var root = ProductionRoot();
            var files = SourceFiles(root).ToList();
            Assert.True(files.Count > 50, $"expected a full production tree, scanned {files.Count} files");

            var offenders = files
                .SelectMany(f => FindMutationCalls(File.ReadAllText(f)).Select(l => $"{Path.GetRelativePath(root, f)}: {l}"))
                .ToList();

            Assert.Empty(offenders);
        }

        /// <summary>
        /// Non-vacuity: the production tree really has typed receivers to resolve (the two consumers
        /// that only read), and the analysis flags each planted call shape while leaving the legal ones.
        /// </summary>
        [Fact]
        public void TheGuard_ActuallyMatchesPlantedCalls()
        {
            var root = ProductionRoot();
            var consumers = SourceFiles(root)
                .Where(f => DeclaredReceiver.IsMatch(File.ReadAllText(f)))
                .Select(Path.GetFileName)
                .ToList();
            Assert.Contains("OutcomeMaturationService.cs", consumers);
            Assert.Contains("WeightOptimizerViewModel.cs", consumers);

            const string field = "private readonly IStudyTaskRepository _tasks;\n";
            const string nullable = "private readonly IStudyTaskRepository? _studyTaskRepo;\n";
            Assert.Single(FindMutationCalls(field + "await _tasks.DeleteAsync(id);"));
            Assert.Single(FindMutationCalls(field + "await this._tasks.AddAsync(t, ct);"));
            Assert.Single(FindMutationCalls(nullable + "await _studyTaskRepo?.UpdateAsync(t)!;"));
            Assert.Single(FindMutationCalls("public X(IStudyTaskRepository repo) { }\nawait repo.AddAsync(t);"));
            Assert.Single(FindMutationCalls("var r = new SqliteStudyTaskRepository(factory);\nawait r.DeleteAsync(id);"));
            Assert.Single(FindMutationCalls("await ServiceLocator.Get<IStudyTaskRepository>().AddAsync(t);"));
            Assert.Single(FindMutationCalls("await sp.GetRequiredService<IStudyTaskRepository>().UpdateAsync(t);"));
            Assert.Single(FindMutationCalls("await new SqliteStudyTaskRepository(() => db).AddAsync(t);"));
            // Review #114 F-2: a local or field assigned from the service locator.
            Assert.Single(FindMutationCalls("var repo9 = ServiceLocator.Get<IStudyTaskRepository>();\nawait repo9.DeleteAsync(Guid.Empty, ct);"));
            Assert.Single(FindMutationCalls("_repo = sp.GetRequiredService<IStudyTaskRepository>()!;\nawait _repo.AddAsync(t);"));
            Assert.Single(FindMutationCalls("var r = Get<IStudyTaskRepository>();\nawait r?.UpdateAsync(t)!;"));

            Assert.Empty(FindMutationCalls(field + "var all = await _tasks.GetAllAsync(ct);"));
            Assert.Empty(FindMutationCalls(field + "private readonly IDifficultyLabelLogRepository _logRepo;\nawait _logRepo.AddAsync(entry);"));
            Assert.Empty(FindMutationCalls("private readonly IStudyTelemetry _telemetry;\nawait _telemetry.AddAsync(x);"));
            Assert.Empty(FindMutationCalls("var log = ServiceLocator.Get<IDifficultyLabelLogRepository>();\nawait log.AddAsync(x);"));
            Assert.Empty(FindMutationCalls("var repo9 = ServiceLocator.Get<IStudyTaskRepository>();\nvar all = await repo9.GetAllAsync(ct);"));
            Assert.Empty(FindMutationCalls(field + "// await _tasks.DeleteAsync(id);"));
            Assert.Empty(FindMutationCalls("public async Task AddAsync(StudyTask task, CancellationToken ct = default)"));
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
            var root = Path.Combine(dir!.FullName, "SmartStudyPlanner");
            Assert.True(Directory.Exists(root), root);
            return root;
        }
    }
}
