using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// All other Data tests drive AppDbContext against an externally-owned, always-open
    /// ":memory:" SqliteConnection. That can't exercise two things production actually does on
    /// every launch: DbBackup.CreateBackup copying a real, closed .db FILE, and AppDbContext
    /// opening/closing ITS OWN connection to that file (ColumnExists' open/close dance). This
    /// drives AppStartup.EnsureDatabaseReady -- the exact sequence App.OnStartup calls -- against
    /// a real file-based DB, closing that gap.
    /// </summary>
    public class AppStartupFileBasedTests : IDisposable
    {
        private readonly string _tempDir = Directory.CreateTempSubdirectory().FullName;

        public void Dispose()
        {
            SqliteConnection.ClearAllPools(); // release any pooled file handles before cleanup
            Directory.Delete(_tempDir, recursive: true);
        }

        [Fact]
        public void EnsureDatabaseReady_OnFileBasedPreEpic1Db_UpgradesAndBacksUpFile()
        {
            var dbPath = Path.Combine(_tempDir, "SmartStudyData.db");
            var connectionString = $"Data Source={dbPath}";

            // Seed a pre-Epic-1 shape DB on a real file-based connection that is opened and
            // fully closed before AppStartup touches it -- mirrors a real alpha-tester DB file.
            using (var seedConn = new SqliteConnection(connectionString))
            {
                seedConn.Open();
                void Exec(string sql)
                {
                    using var cmd = seedConn.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
                Exec(@"CREATE TABLE HocKys (
                    MaHocKy TEXT NOT NULL PRIMARY KEY, Ten TEXT NULL, NgayBatDau TEXT NOT NULL,
                    IsSeeded INTEGER NOT NULL DEFAULT 0)");
                Exec(@"CREATE TABLE MonHocs (
                    MaMonHoc TEXT NOT NULL PRIMARY KEY, MaHocKy TEXT NOT NULL, TenMonHoc TEXT NULL,
                    SoTinChi INTEGER NOT NULL)");
                Exec(@"CREATE TABLE StudyTasks (
                    MaTask TEXT NOT NULL PRIMARY KEY, MaMonHoc TEXT NOT NULL, TenTask TEXT NULL,
                    HanChot TEXT NOT NULL, TrangThai TEXT NULL, LoaiTask INTEGER NOT NULL,
                    DiemUuTien REAL NOT NULL, MucDoCanhBao TEXT NULL, DoKho INTEGER NOT NULL,
                    ThoiGianDaHoc INTEGER NOT NULL DEFAULT 0, NgayHoanThanh TEXT NULL)");
                Exec(@"CREATE TABLE StudyLogs (
                    Id TEXT NOT NULL PRIMARY KEY, MaTask TEXT NOT NULL, NgayHoc TEXT NOT NULL,
                    SoPhutHoc INTEGER NOT NULL, SoPhutDuKien INTEGER NOT NULL, DaHoanThanh INTEGER NOT NULL,
                    GhiChu TEXT NULL, CreatedAtUtc TEXT NOT NULL, DeviceId TEXT NOT NULL DEFAULT '',
                    IsDeleted INTEGER NOT NULL DEFAULT 0)");
                Exec(@"CREATE TABLE TaskNotes (
                    Id TEXT NOT NULL PRIMARY KEY, MaTask TEXT NOT NULL, Content TEXT NULL,
                    UpdatedAtUtc TEXT NOT NULL)");
                Exec(@"CREATE TABLE TaskReferenceLinks (
                    Id TEXT NOT NULL PRIMARY KEY, MaTask TEXT NOT NULL, Title TEXT NOT NULL DEFAULT '',
                    Url TEXT NOT NULL DEFAULT '', Category TEXT NULL, SortOrder INTEGER NOT NULL DEFAULT 0,
                    CreatedAtUtc TEXT NOT NULL DEFAULT '')");
                Exec("INSERT INTO HocKys (MaHocKy, Ten, NgayBatDau, IsSeeded) VALUES ('11111111-1111-1111-1111-111111111111', 'HK1', '2026-01-01', 0)");
            }
            Assert.True(File.Exists(dbPath));

            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
            using (var db = new AppDbContext(options))
            {
                db.Clock = () => new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc);
                var ex = Record.Exception(() => AppStartup.EnsureDatabaseReady(db, dbPath));
                Assert.Null(ex); // must not throw booting a real file-based pre-Epic-1 DB
            }

            var backupFiles = Directory.GetFiles(_tempDir, "SmartStudyData.*.bak.db");
            Assert.Single(backupFiles);

            using var verify = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options);
            var hocKy = verify.HocKys.Single();
            Assert.Equal("HK1", hocKy.Ten);
            Assert.False(hocKy.IsDeleted);
            Assert.False(string.IsNullOrEmpty(hocKy.ModifiedByDeviceId));
        }

        [Fact]
        public void EnsureDatabaseReady_OnPreT37Db_TaoLaiBangOptimizerRunLogs()
        {
            // T3.7 (Epic 3, Card G) — OptimizerRunLogSchemaTests chốt chính SEAM
            // (TelemetrySchema.EnsureOptimizerRunLogTable vá đúng bảng). Nó KHÔNG chốt việc
            // AppStartup.EnsureDatabaseReady thật sự GỌI seam đó: gỡ dòng gọi ở AppStartup.cs
            // vẫn để cả hai test seam kia xanh, và DB người dùng thật sẽ thiếu bảng mà không có
            // tín hiệu nào. Test này đóng đúng khoảng trống "call site bị gỡ trong im lặng",
            // trên file DB thật, qua đúng entry point App.OnStartup dùng.
            var dbPath = Path.Combine(_tempDir, "SmartStudyData.db");
            var connectionString = $"Data Source={dbPath}";
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;

            // DB "đời mới" rồi hạ cấp thành pre-T3.7 bằng cách bỏ đúng bảng của Card G.
            using (var seed = new AppDbContext(options))
            {
                seed.Database.EnsureCreated();
                seed.Database.ExecuteSqlRaw("DROP TABLE OptimizerRunLogs");
            }
            Assert.Equal(0, TableCount(connectionString, "OptimizerRunLogs"));

            using (var db = new AppDbContext(options))
            {
                AppStartup.EnsureDatabaseReady(db, dbPath);
            }

            Assert.Equal(1, TableCount(connectionString, "OptimizerRunLogs"));
        }

        [Fact]
        public void EnsureDatabaseReady_OnPreEpic2Db_TaoLaiBangSyncBaseSnapshots()
        {
            // Epic 2 / M2.1 (T1.4) — same gap this closes for OptimizerRunLogs above:
            // SyncBaseSnapshotSchemaDualPathTests chốt chính SEAM
            // (SyncBaseSnapshotSchema.EnsureTable vá đúng bảng). Nó KHÔNG chốt việc
            // AppStartup.EnsureDatabaseReady thật sự GỌI seam đó — gỡ dòng gọi ở
            // AppStartup.cs vẫn để test seam kia xanh, và DB người dùng thật sẽ thiếu bảng
            // mà không có tín hiệu nào.
            var dbPath = Path.Combine(_tempDir, "SmartStudyData.db");
            var connectionString = $"Data Source={dbPath}";
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;

            // DB "đời mới" rồi hạ cấp thành pre-T1.4 bằng cách bỏ đúng bảng của M2.1.
            using (var seed = new AppDbContext(options))
            {
                seed.Database.EnsureCreated();
                seed.Database.ExecuteSqlRaw("DROP TABLE SyncBaseSnapshots");
            }
            Assert.Equal(0, TableCount(connectionString, "SyncBaseSnapshots"));

            using (var db = new AppDbContext(options))
            {
                AppStartup.EnsureDatabaseReady(db, dbPath);
            }

            Assert.Equal(1, TableCount(connectionString, "SyncBaseSnapshots"));
        }

        /// <summary>
        /// PR-B (DoR F-e/§14.2) at the startup level, on a real DB FILE. The seam tests in
        /// <see cref="SyncSchemaDualPathTests"/> prove EnsureColumns repairs the shape; they do
        /// NOT prove EnsureDatabaseReady still *reaches* it once the D-I columns are present --
        /// that is exactly the gate that was closed (NeedsUpgrade returned false), which is why
        /// every already-upgraded DB stayed broken. Also asserts the destructive DROP COLUMN runs
        /// under the existing DbBackup gate, and only once.
        /// </summary>
        [Fact]
        public async Task EnsureDatabaseReady_OnUpgradedDbStillCarryingLegacyNoteColumn_RepairsUnderBackupGate()
        {
            var dbPath = Path.Combine(_tempDir, "SmartStudyData.db");
            var connectionString = $"Data Source={dbPath}";
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;

            // Current-shape DB, then downgrade ONLY TaskNotes back to the state a previously
            // upgraded DB is really in: D-I columns present, legacy UpdatedAtUtc NOT NULL still there.
            Guid maTask;
            using (var seed = new AppDbContext(options))
            {
                seed.Database.EnsureCreated();
                var hocKy = new HocKy("HK1", DateTime.Today);
                var monHoc = new MonHoc("MH1", 3) { MaHocKy = hocKy.MaHocKy };
                var task = new StudyTask("Task A", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2)
                {
                    MaMonHoc = monHoc.MaMonHoc,
                    MucDoCanhBao = "An toàn",
                };
                monHoc.DanhSachTask.Add(task);
                hocKy.DanhSachMonHoc.Add(monHoc);
                seed.HocKys.Add(hocKy);
                await seed.SaveChangesAsync();
                maTask = task.MaTask;

                seed.Database.ExecuteSqlRaw("DROP TABLE TaskNotes");
                seed.Database.ExecuteSqlRaw(@"CREATE TABLE TaskNotes (
                    Id TEXT NOT NULL PRIMARY KEY, MaTask TEXT NOT NULL, Content TEXT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    Rev INTEGER NOT NULL DEFAULT 0, ModifiedAtUtc TEXT NULL,
                    ModifiedByDeviceId TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
                    DeletedAtUtc TEXT NULL,
                    FOREIGN KEY (MaTask) REFERENCES StudyTasks (MaTask) ON DELETE CASCADE)");
                seed.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IX_TaskNotes_MaTask ON TaskNotes (MaTask)");
            }
            SqliteConnection.ClearAllPools(); // close the file the way a real launch would find it

            using (var db = new AppDbContext(options))
            {
                db.Clock = () => new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
                AppStartup.EnsureDatabaseReady(db, dbPath);
            }

            Assert.Single(Directory.GetFiles(_tempDir, "SmartStudyData.*.bak.db")); // destructive step is backed up
            Assert.False(ColumnExists(connectionString, "TaskNotes", "UpdatedAtUtc"));

            // The whole point: an EF insert now works on a DB that rejected every one before.
            using (var write = new AppDbContext(options))
            {
                write.TaskNotes.Add(new TaskNote { MaTask = maTask, Content = "ghi chú" });
                await write.SaveChangesAsync();
            }

            // Second "launch": repaired DB must not re-enter the upgrade path or pile up backups.
            using (var db2 = new AppDbContext(options))
            {
                db2.Clock = () => new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
                AppStartup.EnsureDatabaseReady(db2, dbPath);
            }
            Assert.Single(Directory.GetFiles(_tempDir, "SmartStudyData.*.bak.db"));

            using var verify = new AppDbContext(options);
            Assert.Equal("ghi chú", (await verify.TaskNotes.SingleAsync()).Content);
        }

        private static bool ColumnExists(string connectionString, string table, string column)
        {
            using var conn = new SqliteConnection(connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table})";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(reader.GetOrdinal("name")), column, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static int TableCount(string connectionString, string table)
        {
            using var conn = new SqliteConnection(connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n";
            cmd.Parameters.AddWithValue("$n", table);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        [Fact]
        public void EnsureDatabaseReady_CalledTwice_IsIdempotentAndDoesNotDuplicateBackups()
        {
            var dbPath = Path.Combine(_tempDir, "SmartStudyData.db");
            var connectionString = $"Data Source={dbPath}";
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;

            using (var db = new AppDbContext(options))
            {
                AppStartup.EnsureDatabaseReady(db, dbPath); // fresh DB, first "launch"
            }

            var ex = Record.Exception(() =>
            {
                using var db2 = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options);
                AppStartup.EnsureDatabaseReady(db2, dbPath); // second "launch" -- already migrated
            });

            Assert.Null(ex);
            Assert.Empty(Directory.GetFiles(_tempDir, "*.bak.db")); // already-migrated DB -> no backup taken
        }
    }
}
