using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Models.Telemetry;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.Soe;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SmartStudyPlanner.Data
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-2, DoR §10.1). Per-entry intent handed to <see cref="SyncStamper"/> for
    /// one SaveChanges. Only one member today: the entry already carries the winning remote/base
    /// provenance and must keep it.
    /// </summary>
    public enum SyncApplyIntent
    {
        PreserveProvenance = 1
    }

    // BẮT BUỘC phải kế thừa từ DbContext của Entity Framework
    public class AppDbContext : DbContext
    {
        // Sync-metadata stamping clock seam — settable for deterministic tests, mirrors the
        // repo's existing FakeClock convention. Not IClock/DI: AppDbContext is parameterless-
        // constructed (see DeviceHelper reuse below), so a plain delegate keeps Data decoupled
        // from Services.Strategies.
        public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

        // Cùng kiểu seam với Clock: composition root gán DeviceIdentity.GetId vào đây.
        // Default vẫn là DeviceHelper.GetId (thuần tính toán, không I/O) nên test và
        // bootstrap dựng AppDbContext trực tiếp không đụng vào %APPDATA%.
        public Func<string> DeviceIdProvider { get; set; } = () => DeviceHelper.GetId();

        // Epic 2 / T2.4 (PR-2, DoR §10.1) — sync-apply intent for the *next* SaveChanges only.
        // Reference equality, not entity equality: the intent belongs to the exact instance the
        // apply session is saving, and two distinct instances of the same row (one loaded
        // AsNoTracking for the merge, one tracked for the write) must not share it.
        private readonly Dictionary<object, SyncApplyIntent> _syncApplyIntents =
            new(ReferenceEqualityComparer.Instance);

        public AppDbContext() { }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        /// <summary>
        /// Marks one tracked entity as sync-applied: <see cref="SyncStamper"/> will keep the
        /// provenance already written on it and only bump the local Rev. Scoped to the next
        /// SaveChanges/SaveChangesAsync on this context — the map is cleared in a finally, so a
        /// failed save cannot leak sync intent into a later, unrelated save. There is no global
        /// "sync mode": every entity the apply session wants preserved must be marked by instance.
        /// </summary>
        public void MarkSyncApplied(object entity) => _syncApplyIntents[entity] = SyncApplyIntent.PreserveProvenance;

        // 1. KHAI BÁO CÁC BẢNG TRONG DATABASE
        // Mỗi DbSet đại diện cho một Bảng (Table) trong CSDL SQLite
        public DbSet<HocKy> HocKys { get; set; }
        public DbSet<MonHoc> MonHocs { get; set; }
        public DbSet<StudyTask> StudyTasks { get; set; }
        public DbSet<StudyLog> StudyLogs { get; set; }
        public DbSet<TaskNote> TaskNotes => Set<TaskNote>();
        public DbSet<TaskReferenceLink> TaskReferenceLinks => Set<TaskReferenceLink>();
        public DbSet<DifficultyLabelLog> DifficultyLabelLogs => Set<DifficultyLabelLog>();
        public DbSet<WeightChangeLog> WeightChangeLogs => Set<WeightChangeLog>();
        public DbSet<StudyTimeOutcomeLog> StudyTimeOutcomeLogs => Set<StudyTimeOutcomeLog>();

        // T3.7 (Epic 3, Card G) — telemetry cho IScheduleOptimizer.Optimize (G2-6). Tách bảng
        // (Data/TelemetrySchema.cs:EnsureOptimizerRunLogTable) khỏi EnsureTables (M8) có chủ đích —
        // xem doc comment của method đó.
        public DbSet<OptimizerRunLogRow> OptimizerRunLogs => Set<OptimizerRunLogRow>();

        // Epic 2 / M2.1 (T1.4) — per-peer last-synced base-snapshot store. Bookkeeping
        // table, not a synced business entity (see SyncBaseSnapshotRow's own doc comment
        // for why it must not implement ISyncMetadata).
        public DbSet<Sync.SyncBaseSnapshotRow> SyncBaseSnapshots => Set<Sync.SyncBaseSnapshotRow>();

        // Epic 2 / T2.4 (PR-4) — persistent ConflictRecord staging boundary (D6/D7/D8, D9-T1..T6).
        // Bookkeeping table, not a synced business entity (see SyncConflictRecordRow's own doc
        // comment for why it must not implement ISyncMetadata).
        public DbSet<Sync.SyncConflictRecordRow> SyncConflictRecords => Set<Sync.SyncConflictRecordRow>();

        // 2. CẤU HÌNH ĐƯỜNG DẪN LƯU FILE SQLITE
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured) return;
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartStudyData.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        // 3. CẤU HÌNH QUAN HỆ GIỮA CÁC BẢNG (Tùy chọn nhưng nên có để tránh lỗi Xóa dây chuyền)
        //
        // Epic 1 / M1.2 (G1): every OnDelete(DeleteBehavior.Cascade) below is now inert at the
        // SQL level -- SyncStamper converts a Remove() into a soft IsDeleted update before
        // SaveChanges executes, so no real DELETE (and therefore no DB-level ON DELETE CASCADE)
        // ever fires. The config is kept because it still drives EF Core's in-memory ChangeTracker
        // cascade *fixup*: when a tracked parent is Remove()d, EF marks its *loaded* children
        // Deleted too, which is what lets SyncStamper cascade-tombstone them in the same
        // SaveChanges call. See docs/plans/2026-07-03-g1-soft-delete-cascade.md.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Khi xóa Học Kỳ -> Tự động xóa sạch Môn Học bên trong
            modelBuilder.Entity<HocKy>()
                .HasMany(hk => hk.DanhSachMonHoc)
                .WithOne()
                .HasForeignKey(mh => mh.MaHocKy)
                .OnDelete(DeleteBehavior.Cascade);

            // Khi xóa Môn Học -> Tự động xóa sạch Bài Tập bên trong
            modelBuilder.Entity<MonHoc>()
                .HasMany(mh => mh.DanhSachTask)
                .WithOne()
                .HasForeignKey(t => t.MaMonHoc)
                .OnDelete(DeleteBehavior.Cascade);

            // TaskNote: 1-1 với StudyTask, cascade delete
            modelBuilder.Entity<TaskNote>(b =>
            {
                b.HasKey(n => n.Id);
                b.HasIndex(n => n.MaTask).IsUnique();
                b.HasOne<StudyTask>()
                 .WithOne()
                 .HasForeignKey<TaskNote>(n => n.MaTask)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // TaskReferenceLink: 1-N với StudyTask, cascade delete
            modelBuilder.Entity<TaskReferenceLink>(b =>
            {
                b.HasKey(l => l.Id);
                b.HasOne<StudyTask>()
                 .WithMany()
                 .HasForeignKey(l => l.MaTask)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Telemetry log tables — standalone, no FK to StudyTask (MaTask nullable reference only)
            modelBuilder.Entity<DifficultyLabelLog>(b => b.HasKey(e => e.Id));
            modelBuilder.Entity<WeightChangeLog>(b => b.HasKey(e => e.Id));
            modelBuilder.Entity<StudyTimeOutcomeLog>(b => b.HasKey(e => e.Id));

            // T3.7 (Epic 3, Card G) — cùng shape "standalone, no FK" như ba bảng telemetry M8 ở
            // trên; xem OptimizerRunLogRow's doc comment cho lý do denormalize.
            modelBuilder.Entity<OptimizerRunLogRow>(b => b.HasKey(e => e.Id));

            // Epic 2 / M2.1 (T1.4) — per-peer last-synced base-snapshot store, composite key.
            modelBuilder.Entity<Sync.SyncBaseSnapshotRow>(b =>
                b.HasKey(s => new { s.PeerDeviceId, s.EntityType, s.EntityId }));

            // Epic 2 / T2.4 (PR-4) — ConflictRecord staging boundary (DoR §8). The filtered unique
            // index enforces D9-T6 ("at most one Unresolved record per logical scope") at the
            // database level. EnsureCreated() builds the table + both indexes from this config on a
            // fresh DB; Data/SyncConflictRecordSchema.EnsureTable patches the SAME table+indexes onto
            // every other DB, plus the three triggers this config cannot express (EF has no trigger
            // concept), and runs unconditionally at every startup because EnsureCreated() never
            // creates triggers regardless of whether the table itself is new or pre-existing.
            modelBuilder.Entity<Sync.SyncConflictRecordRow>(b =>
            {
                // D4/D9-T4 amendment (2026-09-10): the three local-candidate columns are nullable, so
                // this CHECK carries what their NOT NULL constraints used to -- present-or-absent
                // together, absence legal only for Kind = 1 (StructuralConflict). Declared here AND in
                // SyncConflictRecordSchema.CreateTableSql under the same name, because the two creation
                // paths must converge (SyncConflictRecordSchemaDualPathTests) and because that name is
                // what the migration probes to decide whether a database still needs the rebuild.
                b.ToTable("SyncConflictRecords", t => t.HasCheckConstraint(
                    "CK_SyncConflictRecords_LocalCandidate",
                    "(LocalEntityId IS NULL) = (LocalSnapshotJson IS NULL)" +
                    " AND (LocalEntityId IS NULL) = (LocalFingerprint IS NULL)" +
                    " AND (LocalEntityId IS NOT NULL OR Kind = 1)"));
                b.HasKey(r => r.ConflictId);
                b.HasIndex(r => r.ConflictKey).IsUnique().HasDatabaseName("IX_SyncConflictRecords_ConflictKey");
                b.HasIndex(r => r.ScopeKey).IsUnique()
                    .HasFilter("Status = 0")
                    .HasDatabaseName("IX_SyncConflictRecords_OneUnresolvedPerScope");
                b.Property(r => r.LocalWithdrawal).HasDefaultValue(Sync.ConflictLocalWithdrawal.None);
            });
        }

        // 4. SINGLE STAMPING SEAM (Epic 1 / D-I, M1.1 scope): every write across the 9
        // repositories + App.xaml.cs routes through DbSet Add/Update/Remove into one of these
        // two overloads (SaveChanges()/SaveChangesAsync() are non-virtual wrappers around them).
        // All six synced entities (HocKy, MonHoc, StudyTask, StudyLog, TaskNote,
        // TaskReferenceLink — shipped in M1.2/M1.3) implement ISyncMetadata and get stamped
        // here; SyncBaseSnapshotRow deliberately does not (see its own doc comment) and is
        // skipped — see SyncMetadataStampingTests for coverage.
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            try
            {
                SyncStamper.Apply(ChangeTracker, Clock, DeviceIdProvider(), _syncApplyIntents);
                return base.SaveChanges(acceptAllChangesOnSuccess);
            }
            finally { _syncApplyIntents.Clear(); }
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            try
            {
                SyncStamper.Apply(ChangeTracker, Clock, DeviceIdProvider(), _syncApplyIntents);
                return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }
            finally { _syncApplyIntents.Clear(); }
        }
    }
}