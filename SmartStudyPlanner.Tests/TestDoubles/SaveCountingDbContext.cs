using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;

namespace SmartStudyPlanner.Tests.TestDoubles
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 2 (mission §19, X-8). An <see cref="AppDbContext"/> that counts every
    /// <c>SaveChanges</c>/<c>SaveChangesAsync</c> call instead of performing it, so a test can prove the
    /// fence never saves without relying on "nothing changed" as a weaker, coincidental signal (a save of
    /// zero pending changes would otherwise look identical to no save at all).
    /// <para>
    /// Same idiom as <see cref="FailingSaveDbContext"/>: a real subclass over the real database/triggers,
    /// with only the one behaviour under test replaced.
    /// </para>
    /// </summary>
    internal sealed class SaveCountingDbContext : AppDbContext
    {
        public SaveCountingDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public int SaveCount { get; private set; }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            SaveCount++;
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}
