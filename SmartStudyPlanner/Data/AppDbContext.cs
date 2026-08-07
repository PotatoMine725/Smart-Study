using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Models.Telemetry;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.Soe;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SmartStudyPlanner.Data
{
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

        public AppDbContext() { }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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
        }

        // 4. SINGLE STAMPING SEAM (Epic 1 / D-I, M1.1 scope): every write across the 9
        // repositories + App.xaml.cs routes through DbSet Add/Update/Remove into one of these
        // two overloads (SaveChanges()/SaveChangesAsync() are non-virtual wrappers around them).
        // No production entity implements ISyncMetadata yet (M1.2's T1.1), so this is currently
        // a no-op pass-through for all real writes — see SyncMetadataStampingTests for coverage.
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            SyncStamper.Apply(ChangeTracker, Clock, DeviceIdProvider());
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            SyncStamper.Apply(ChangeTracker, Clock, DeviceIdProvider());
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}