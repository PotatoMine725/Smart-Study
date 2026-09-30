using System;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// Epic 2 / T2.4 — <b>P0-b measurement</b> (plan §16.3 P0-b, §14 A-3). Characterization only: these
    /// tests pin what SQLite + Microsoft.Data.Sqlite + EF Core 10.0.5 <b>OBSERVED-ly do</b> on a
    /// file-backed database with two independent connections (2026-09-30). They assert no desired
    /// semantic. <b>Consumer: Slice 4</b> (§14 "evaluate the fence inside the executor's own write
    /// transaction") -- it cannot start without this measurement.
    /// <para>
    /// <b>Every leg runs in two journal modes.</b> <c>rollbackJournal: false</c> is the production-relevant
    /// one: no production code sets a journal mode, but <c>EnsureCreated</c> creates the file in WAL
    /// (read back as <c>wal</c> by <see cref="FileBackedSqliteFixture.ReadJournalMode"/>, and asserted
    /// below so a future EF default change turns this red instead of silently changing what is measured).
    /// <c>rollbackJournal: true</c> forces <c>delete</c> and is a supplementary variant only.
    /// </para>
    /// <para>
    /// <b>Instrument caveats (read before trusting a number).</b> "After how long" is bounded by the
    /// fixture's <c>Default Timeout</c> (1 s in the timing legs), NOT by SQLite: Microsoft.Data.Sqlite
    /// retries a busy statement until that timeout, including for <c>SQLITE_BUSY_SNAPSHOT</c> (extended
    /// code 517), which SQLite itself reports immediately. Production uses the library default (30 s);
    /// that value was NOT measured here. A timeout of 0 means "retry forever", not "fail now".
    /// </para>
    /// </summary>
    public class SqliteTransactionLockProbeTests
    {
        private const int BusyWindowMs = 1000;

        private readonly ITestOutputHelper _out;

        public SqliteTransactionLockProbeTests(ITestOutputHelper output) => _out = output;

        private sealed record Outcome(string Result, int? Code, int? Extended, long Ms)
        {
            public bool IsOk => Result == "OK";
            public bool IsBusy => Code == 5;
            public override string ToString() => $"{Result} code={Code?.ToString() ?? "-"} ext={Extended?.ToString() ?? "-"} after {Ms} ms";
        }

        private static async Task<Outcome> AttemptAsync(Func<Task> act)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await act();
                return new Outcome("OK", null, null, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                for (var e = ex; e != null; e = e.InnerException!)
                {
                    if (e is SqliteException sq)
                        return new Outcome("SqliteException", sq.SqliteErrorCode, sq.SqliteExtendedErrorCode, sw.ElapsedMilliseconds);
                    if (e.InnerException == null) break;
                }
                return new Outcome("OTHER:" + ex.GetType().Name, null, null, sw.ElapsedMilliseconds);
            }
        }

        private static void Exec(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The observation channel for "did A take the write lock at BEGIN?": an INDEPENDENT connection
        /// tries <c>BEGIN IMMEDIATE</c> with a 1 s busy window (the smallest usable value: see the class
        /// remarks on <c>Default Timeout=0</c>). BUSY after ~1 s => somebody else already holds
        /// RESERVED-or-above; OK => nobody does. Rolls back at once so the probe leaves nothing behind.
        /// The two CONTROL legs prove it can answer both ways.
        /// </summary>
        private static async Task<Outcome> ProbeWriterAdmittedAsync(FileBackedSqliteFixture fx)
        {
            using var b = fx.OpenConnection(timeoutSeconds: 1);
            var o = await AttemptAsync(() => { Exec(b, "BEGIN IMMEDIATE;"); return Task.CompletedTask; });
            if (o.IsOk) Exec(b, "ROLLBACK;");
            return o;
        }

        private static HocKy NewHocKy(string name) => new(name, new DateTime(2026, 1, 5));

        private static async Task<int> CountRowsAsync(FileBackedSqliteFixture fx)
        {
            using var ctx = fx.NewContext();
            return await ctx.HocKys.CountAsync();
        }

        private static void AssertBusy(Outcome o, int extendedCode)
        {
            Assert.False(o.IsOk, o.ToString());
            Assert.Equal(5, o.Code);
            Assert.Equal(extendedCode, o.Extended);
            Assert.InRange(o.Ms, BusyWindowMs - 100, 6 * BusyWindowMs); // waited the busy window, did not fail instantly
        }

        // ------------------------------------------------------------------ lock mode

        /// <summary>
        /// P0-b, question 1 — <b>OBSERVED 2026-09-30</b>: <c>Database.BeginTransactionAsync()</c> takes the
        /// WRITE lock at BEGIN (immediate mode), before any statement runs. An independent
        /// <c>BEGIN IMMEDIATE</c> is refused (BUSY after the 1 s window) while an EF transaction is merely
        /// open with no statement at all -- in both journal modes, with and without a prior SELECT, and
        /// with <c>IsolationLevel.Serializable</c> (same result). Consumed by Slice 4 §14: a fence
        /// evaluated inside the executor's transaction is therefore serialized against every other EF
        /// writer from BEGIN, not from its first write.
        /// <para>
        /// Discriminator: the two controls. A raw <c>BEGIN DEFERRED</c> is admitted (probe OK) and a raw
        /// <c>BEGIN IMMEDIATE</c> is refused, so a BUSY on the EF legs is a property of EF's BEGIN, not of
        /// a probe that always fails.
        /// </para>
        /// </summary>
        [Theory]
        [InlineData(false, "wal")]
        [InlineData(true, "delete")]
        public async Task P0b_LockMode_ObservedBehaviour(bool rollbackJournal, string expectedJournalMode)
        {
            using var fx = new FileBackedSqliteFixture(rollbackJournal);
            Assert.Equal(expectedJournalMode, fx.ReadJournalMode());

            using (var a = fx.NewContext())
            {
                await using var tx = await a.Database.BeginTransactionAsync();
                var noStatement = await ProbeWriterAdmittedAsync(fx);
                _ = await a.HocKys.CountAsync();
                var afterSelect = await ProbeWriterAdmittedAsync(fx);
                _out.WriteLine($"EF BeginTransactionAsync() no statement: {noStatement}; after one SELECT: {afterSelect}");
                AssertBusy(noStatement, 5);
                AssertBusy(afterSelect, 5);
            }

            using (var a = fx.NewContext())
            {
                await using var tx = await a.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var serializable = await ProbeWriterAdmittedAsync(fx);
                _out.WriteLine($"EF BeginTransactionAsync(Serializable) no statement: {serializable}");
                AssertBusy(serializable, 5);
            }

            using (var a = fx.OpenConnection())
            {
                Exec(a, "BEGIN DEFERRED;");
                var deferred = await ProbeWriterAdmittedAsync(fx);
                Exec(a, "ROLLBACK;");
                _out.WriteLine($"CONTROL raw BEGIN DEFERRED: {deferred}");
                Assert.True(deferred.IsOk, "control: a deferred BEGIN must admit the probe: " + deferred);
                Assert.True(deferred.Ms < BusyWindowMs / 2, deferred.ToString());
            }

            using (var a = fx.OpenConnection())
            {
                Exec(a, "BEGIN IMMEDIATE;");
                var immediate = await ProbeWriterAdmittedAsync(fx);
                Exec(a, "ROLLBACK;");
                _out.WriteLine($"CONTROL raw BEGIN IMMEDIATE: {immediate}");
                AssertBusy(immediate, 5);
            }
        }

        // ------------------------------------------------------------------ two EF transactions

        /// <summary>
        /// P0-b, question 2a — <b>OBSERVED 2026-09-30</b>: with two EF transactions the SECOND one waits
        /// at <c>BeginTransactionAsync()</c> (not at its first write). It was released ~465 ms in, right
        /// after the first committed, and then SAW the first transaction's committed row (count 1), i.e.
        /// its reads are taken after the lock is won, never before. Both rows end up committed. Both
        /// journal modes identical. Consumed by Slice 4 §14 (no stale read is possible between two EF
        /// writers).
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task P0b_SecondEfTransaction_WaitsAtBegin_ObservedBehaviour(bool rollbackJournal)
        {
            using var fx = new FileBackedSqliteFixture(rollbackJournal);

            using var a = fx.NewContext();
            await using var txA = await a.Database.BeginTransactionAsync();
            _ = await a.HocKys.CountAsync();
            a.HocKys.Add(NewHocKy("A"));
            await a.SaveChangesAsync();

            var bTask = Task.Run(async () =>
            {
                using var b = fx.NewContext(timeoutSeconds: 10);
                var sw = Stopwatch.StartNew();
                await using var txB = await b.Database.BeginTransactionAsync();
                var beginMs = sw.ElapsedMilliseconds;
                var seenByB = await b.HocKys.CountAsync();
                b.HocKys.Add(NewHocKy("B"));
                await b.SaveChangesAsync();
                await txB.CommitAsync();
                return (beginMs, seenByB);
            });

            await Task.Delay(400);
            var aCommit = await AttemptAsync(() => txA.CommitAsync());
            var (beginMs, seenByB) = await bTask;
            var rows = await CountRowsAsync(fx);
            _out.WriteLine($"A commit: {aCommit}; B BeginTransactionAsync returned after {beginMs} ms (A held ~400 ms); B saw {seenByB} row(s); final rows={rows}");

            Assert.True(aCommit.IsOk);
            Assert.InRange(beginMs, 350, 6000);   // it WAITED for A, and was not refused
            Assert.Equal(1, seenByB);             // its read came after A's commit
            Assert.Equal(2, rows);
        }

        /// <summary>
        /// P0-b, question 2b — <b>OBSERVED 2026-09-30</b>: when the first EF transaction never ends, the
        /// second one's <c>BeginTransactionAsync()</c> fails with <c>SQLITE_BUSY</c> (code 5, extended 5)
        /// after the fixture's 1 s window. The failure surfaces from BEGIN, before any fence read could
        /// run. Both journal modes identical. Consumed by Slice 4 §14 (failure ⇒ rollback ⇒ rethrow; the
        /// executor must not swallow it).
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task P0b_SecondEfTransaction_TimesOutAtBegin_ObservedBehaviour(bool rollbackJournal)
        {
            using var fx = new FileBackedSqliteFixture(rollbackJournal);

            using var a = fx.NewContext();
            await using var txA = await a.Database.BeginTransactionAsync();
            a.HocKys.Add(NewHocKy("A"));
            await a.SaveChangesAsync();

            using var b = fx.NewContext(timeoutSeconds: 1);
            var o = await AttemptAsync(async () => { await using var txB = await b.Database.BeginTransactionAsync(); });
            _out.WriteLine($"B BeginTransactionAsync while A's transaction stays open, B timeout 1 s: {o}");

            AssertBusy(o, 5);
        }

        // ------------------------------------------------------------------ read-then-write (stale read)

        /// <summary>
        /// P0-b, question 2c — the read-then-write outcome plan §14 (A-3) reasons about. EF itself cannot
        /// produce a stale read (2a), so it is manufactured with a raw <c>BEGIN DEFERRED</c> reader A
        /// (NOT what EF does -- see the lock-mode leg) racing an EF writer B. <b>OBSERVED 2026-09-30:</b>
        /// <list type="bullet">
        /// <item><b>WAL (production-relevant):</b> B commits at once (a WAL reader does not block a writer).
        /// A's later write fails with <c>SQLITE_BUSY_SNAPSHOT</c> (extended 517) -- reported only after
        /// the 1 s busy window, because Microsoft.Data.Sqlite retries it. A never writes; final rows = 1
        /// (B only). No write was committed on the stale read.</item>
        /// <item><b>rollback journal (supplementary):</b> B's commit is blocked by A's read lock (BUSY
        /// after the window) and A's write is blocked by B's pending write lock (BUSY): both fail. B still
        /// holds its lock until rolled back; after B's rollback the final row count is 0.</item>
        /// </list>
        /// In neither mode is a write committed on top of a stale read (the §14 claim, OBSERVED here for
        /// the deferred style only). Consumed by Slice 4 §14.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task P0b_ReadThenWrite_StaleRead_FromDeferredTransaction_ObservedBehaviour(bool rollbackJournal)
        {
            using var fx = new FileBackedSqliteFixture(rollbackJournal);

            using var a = fx.OpenConnection(timeoutSeconds: 1);
            Exec(a, "BEGIN DEFERRED;");
            long seen;
            using (var cmd = a.CreateCommand()) { cmd.CommandText = "SELECT COUNT(*) FROM HocKys;"; seen = (long)cmd.ExecuteScalar()!; }
            Assert.Equal(0, seen);

            using var b = fx.NewContext(timeoutSeconds: 1);
            await using var txB = await b.Database.BeginTransactionAsync();
            b.HocKys.Add(NewHocKy("B"));
            await b.SaveChangesAsync();
            var bCommit = await AttemptAsync(() => txB.CommitAsync());

            var aWrite = await AttemptAsync(() =>
            {
                Exec(a, "INSERT INTO HocKys (MaHocKy, Ten, NgayBatDau, IsSeeded, Rev, ModifiedAtUtc, ModifiedByDeviceId, IsDeleted) " +
                        $"VALUES ('{Guid.NewGuid():D}', 'A', '2026-01-05', 0, 1, '2026-01-01', 'A-DEVICE', 0);");
                return Task.CompletedTask;
            });

            // Release whatever is still held before counting: after a failed commit B still holds its
            // write lock and would block the counting reader in rollback-journal mode.
            if (!bCommit.IsOk) await txB.RollbackAsync();
            var aCommit = await AttemptAsync(() => { Exec(a, aWrite.IsOk ? "COMMIT;" : "ROLLBACK;"); return Task.CompletedTask; });
            var rows = await CountRowsAsync(fx);
            _out.WriteLine($"B(EF) commit: {bCommit}; A(deferred, read count={seen}) write: {aWrite}; A end: {aCommit}; final rows={rows}");

            Assert.True(aCommit.IsOk, aCommit.ToString());
            if (!rollbackJournal)
            {
                Assert.True(bCommit.IsOk, bCommit.ToString());
                AssertBusy(aWrite, 517);   // SQLITE_BUSY_SNAPSHOT
                Assert.Equal(1, rows);     // only B; A's stale-read write never landed
            }
            else
            {
                AssertBusy(bCommit, 5);
                AssertBusy(aWrite, 5);
                Assert.Equal(0, rows);     // neither landed
            }
        }
    }
}
