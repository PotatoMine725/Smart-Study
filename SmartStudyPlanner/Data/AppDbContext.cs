using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Models;
using System;
using System.IO;

namespace SmartStudyPlanner.Data
{
    // BẮT BUỘC phải kế thừa từ DbContext của Entity Framework
    public class AppDbContext : DbContext
    {
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

        // 2. CẤU HÌNH ĐƯỜNG DẪN LƯU FILE SQLITE
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured) return;
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartStudyData.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        // 3. CẤU HÌNH QUAN HỆ GIỮA CÁC BẢNG (Tùy chọn nhưng nên có để tránh lỗi Xóa dây chuyền)
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
        }
    }
}