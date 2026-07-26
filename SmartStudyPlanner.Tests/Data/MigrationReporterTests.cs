using System;
using System.Linq;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// T1.8 evidence utility: row-count + checksum per table, the acceptance-criterion
    /// evidence for "existing DBs upgrade in place losslessly."
    /// </summary>
    public class MigrationReporterTests
    {
        [Fact]
        public void Capture_ReturnsRowCountAndChecksumPerTable()
        {
            using var conn = TestDb.OpenConnection();
            using (var seed = TestDb.Create(conn))
            {
                seed.HocKys.Add(new HocKy("HK", DateTime.Today));
                seed.SaveChanges();
            }

            using var db = TestDb.Create(conn);
            var snapshots = MigrationReporter.Capture(db);

            var hocKySnap = snapshots.Single(s => s.Table == "HocKys");
            Assert.Equal(1, hocKySnap.RowCount);
            Assert.False(string.IsNullOrEmpty(hocKySnap.Checksum));

            var monHocSnap = snapshots.Single(s => s.Table == "MonHocs");
            Assert.Equal(0, monHocSnap.RowCount);
        }

        [Fact]
        public void CaptureTable_RestrictedToCommonColumns_MatchesAcrossSchemaUpgrade()
        {
            using var conn = TestDb.OpenConnection();
            // EnsureCreated first (all six tables in current shape), then downgrade just HocKys
            // to its pre-Epic-1 shape -- mirrors TelemetrySchemaDualPathTests' downgrade pattern.
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated */ }
            using (var raw = conn.CreateCommand())
            {
                raw.CommandText = "DROP TABLE HocKys";
                raw.ExecuteNonQuery();
            }
            using (var raw = conn.CreateCommand())
            {
                raw.CommandText = @"CREATE TABLE HocKys (
                    MaHocKy TEXT NOT NULL PRIMARY KEY, Ten TEXT NULL, NgayBatDau TEXT NOT NULL,
                    IsSeeded INTEGER NOT NULL DEFAULT 0)";
                raw.ExecuteNonQuery();
            }
            using (var raw = conn.CreateCommand())
            {
                raw.CommandText = "INSERT INTO HocKys (MaHocKy, Ten, NgayBatDau, IsSeeded) VALUES ('11111111-1111-1111-1111-111111111111', 'HK1', '2026-01-01', 0)";
                raw.ExecuteNonQuery();
            }

            var preUpgradeColumns = new[] { "MaHocKy", "Ten", "NgayBatDau", "IsSeeded" };
            TableSnapshot before;
            using (var db = TestDb.Create(conn))
            {
                before = MigrationReporter.CaptureTable(db, "HocKys", preUpgradeColumns);
            }

            using (var migrate = TestDb.Create(conn))
            {
                SyncSchema.EnsureColumns(migrate);
            }

            TableSnapshot after;
            using (var db = TestDb.Create(conn))
            {
                after = MigrationReporter.CaptureTable(db, "HocKys", preUpgradeColumns);
            }

            Assert.Equal(before.RowCount, after.RowCount);
            Assert.Equal(before.Checksum, after.Checksum); // pre-existing columns byte-for-byte unchanged
        }
    }
}
