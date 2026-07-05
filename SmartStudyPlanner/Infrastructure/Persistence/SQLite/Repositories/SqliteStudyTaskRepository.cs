using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories
{
    public sealed class SqliteStudyTaskRepository : IStudyTaskRepository
    {
        private readonly Func<AppDbContext> _ctxFactory;

        public SqliteStudyTaskRepository(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public async Task<StudyTask?> GetAsync(Guid maTask, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            return await db.StudyTasks.FirstOrDefaultAsync(t => t.MaTask == maTask && !t.IsDeleted, ct);
        }

        public async Task<List<StudyTask>> GetByMonHocAsync(Guid maMonHoc, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            return await db.StudyTasks.Where(t => t.MaMonHoc == maMonHoc && !t.IsDeleted).ToListAsync(ct);
        }

        public async Task<List<StudyTask>> GetAllAsync(CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            return await db.StudyTasks.Where(t => !t.IsDeleted).ToListAsync(ct);
        }

        public async Task AddAsync(StudyTask task, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            db.StudyTasks.Add(task);
            await db.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(StudyTask task, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            db.StudyTasks.Update(task);
            await db.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Guid maTask, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            var existing = await db.StudyTasks.FirstOrDefaultAsync(t => t.MaTask == maTask, ct);
            if (existing is null) return;
            db.StudyTasks.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}
