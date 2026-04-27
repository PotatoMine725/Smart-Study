using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Data
{
    // Class này ký hợp đồng với IStudyRepository
    public class StudyRepository : IStudyRepository
    {
        public async Task<HocKy> DocHocKyAsync()
        {
            using (var db = new AppDbContext())
            {
                // Dùng các hàm có chữ Async ở đuôi để luồng giao diện không bị đứng hình chờ đợi
                return await db.HocKys
                         .Include(hk => hk.DanhSachMonHoc)
                            .ThenInclude(mon => mon.DanhSachTask)
                         .FirstOrDefaultAsync();
            }
        }

        public async Task LuuHocKyAsync(HocKy hocKy)
        {
            if (hocKy == null) return;

            using (var db = new AppDbContext())
            {
                // BẢO MẬT 1: Bật Transaction. Nếu đang lưu mà cúp điện hoặc crash app, 
                // Database sẽ tự động hoàn tác (Rollback), dữ liệu cũ không bao giờ bị mất!
                using (var transaction = await db.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Kéo toàn bộ rễ của Học kỳ cũ lên
                        var hocKyCu = await db.HocKys
                            .Include(h => h.DanhSachMonHoc)
                            .ThenInclude(m => m.DanhSachTask)
                            .FirstOrDefaultAsync(h => h.MaHocKy == hocKy.MaHocKy);

                        // 2. Nếu có cũ -> Xóa sạch bách khỏi CSDL
                        if (hocKyCu != null)
                        {
                            db.HocKys.Remove(hocKyCu);
                            await db.SaveChangesAsync();
                        }

                        // BẢO MẬT 2: CHÌA KHÓA DIỆT LỖI TRACKING!
                        // Xóa sạch trí nhớ của EF Core để nó quên đi cái hocKyCu vừa bị xóa,
                        // dọn đường sạch sẽ để đón hocKy mới vào mà không bị "đụng" ID.
                        db.ChangeTracker.Clear();

                        // 3. Đắp nguyên cái ba-lô dữ liệu mới vào
                        db.HocKys.Add(hocKy);
                        await db.SaveChangesAsync();

                        // 4. Chốt giao dịch, ghi thẳng vào ổ cứng
                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        // Gặp biến -> Quay xe!
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task<List<HocKy>> LayDanhSachHocKyAsync()
        {
            using (var db = new AppDbContext())
            {
                // Dùng ToListAsync() để lôi TOÀN BỘ học kỳ có trong DB lên
                return await db.HocKys
                         .Include(hk => hk.DanhSachMonHoc)
                            .ThenInclude(mon => mon.DanhSachTask)
                         .ToListAsync();
            }
        }

        public async Task AddStudyLogAsync(StudyLog log)
        {
            using var db = new AppDbContext();
            db.StudyLogs.Add(log);
            await db.SaveChangesAsync();
        }

        public async Task<List<StudyLog>> GetStudyLogsAsync(HocKy hocKy)
        {
            using var db = new AppDbContext();
            var taskIds = hocKy.DanhSachMonHoc
                .SelectMany(m => m.DanhSachTask)
                .Select(t => t.MaTask)
                .ToHashSet();
            return await db.StudyLogs
                .Where(l => taskIds.Contains(l.MaTask))
                .OrderBy(l => l.NgayHoc)
                .ToListAsync();
        }

        public async Task<List<StudyLog>> GetStudyLogsSinceAsync(DateTime sinceUtc, CancellationToken ct = default)
        {
            using var db = new AppDbContext();
            return await db.StudyLogs
                .Where(l => l.CreatedAtUtc >= sinceUtc && !l.IsDeleted)
                .OrderBy(l => l.CreatedAtUtc)
                .ToListAsync(ct);
        }

        // M6.1 — Task Notes & Study Links

        public async Task<TaskEditorBundle?> GetTaskEditorBundleAsync(Guid taskId)
        {
            using var db = new AppDbContext();
            var task = await db.StudyTasks.FindAsync(taskId);
            if (task is null) return null;
            var note = await db.TaskNotes.FirstOrDefaultAsync(n => n.MaTask == taskId);
            var links = await db.TaskReferenceLinks
                .Where(l => l.MaTask == taskId)
                .OrderBy(l => l.SortOrder)
                .ToListAsync();
            return new TaskEditorBundle { Task = task, Note = note, Links = links };
        }

        public async Task UpsertTaskNoteAsync(Guid taskId, string? content)
        {
            using var db = new AppDbContext();
            var note = await db.TaskNotes.FirstOrDefaultAsync(n => n.MaTask == taskId);
            if (note is null)
                db.TaskNotes.Add(new TaskNote { MaTask = taskId, Content = content });
            else
            {
                note.Content = content;
                note.UpdatedAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
        }

        public Task<List<TaskReferenceLink>> GetTaskReferenceLinksAsync(Guid taskId)
        {
            using var db = new AppDbContext();
            return db.TaskReferenceLinks
                .Where(l => l.MaTask == taskId)
                .OrderBy(l => l.SortOrder)
                .ToListAsync();
        }

        public async Task AddTaskReferenceLinkAsync(TaskReferenceLink link)
        {
            using var db = new AppDbContext();
            db.TaskReferenceLinks.Add(link);
            await db.SaveChangesAsync();
        }

        public async Task UpdateTaskReferenceLinkAsync(TaskReferenceLink link)
        {
            using var db = new AppDbContext();
            db.TaskReferenceLinks.Update(link);
            await db.SaveChangesAsync();
        }

        public async Task DeleteTaskReferenceLinkAsync(Guid linkId)
        {
            using var db = new AppDbContext();
            var link = await db.TaskReferenceLinks.FindAsync(linkId);
            if (link is not null)
            {
                db.TaskReferenceLinks.Remove(link);
                await db.SaveChangesAsync();
            }
        }

        public async Task SaveTaskEditorBundleAsync(TaskEditorBundle bundle)
        {
            using var db = new AppDbContext();
            db.StudyTasks.Update(bundle.Task);

            if (bundle.Note is not null)
            {
                var existing = await db.TaskNotes.FirstOrDefaultAsync(n => n.MaTask == bundle.Task.MaTask);
                if (existing is null)
                    db.TaskNotes.Add(new TaskNote { MaTask = bundle.Task.MaTask, Content = bundle.Note.Content });
                else
                {
                    existing.Content = bundle.Note.Content;
                    existing.UpdatedAtUtc = DateTime.UtcNow;
                }
            }

            var existingLinks = await db.TaskReferenceLinks
                .Where(l => l.MaTask == bundle.Task.MaTask)
                .ToListAsync();
            var incomingIds = bundle.Links.Select(l => l.Id).ToHashSet();
            var existingIds = existingLinks.Select(l => l.Id).ToHashSet();

            db.TaskReferenceLinks.RemoveRange(existingLinks.Where(l => !incomingIds.Contains(l.Id)));

            foreach (var link in bundle.Links)
            {
                if (existingIds.Contains(link.Id))
                    db.TaskReferenceLinks.Update(link);
                else
                    db.TaskReferenceLinks.Add(link);
            }

            await db.SaveChangesAsync();
        }
    }
}