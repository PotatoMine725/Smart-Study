using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;

namespace SmartStudyPlanner.Tests.TestDoubles
{
    /// <summary>
    /// An <see cref="AppDbContext"/> that throws on the N-th <c>SaveChangesAsync</c>. Used to exercise
    /// the apply session's transaction boundary and context lifetime (PR-5 tests G and H) at the one
    /// point that is otherwise hard to reach from outside: DoR §11.2 puts TWO saves inside one
    /// transaction (the apply write, then the baseline upsert), and the interesting failure is the
    /// second one — after EF has already accepted the first with <c>acceptAllChangesOnSuccess</c>.
    /// <para>
    /// Deliberately a subclass rather than a mock: the transaction, the triggers and the real stamping
    /// seam all still run, so what is being tested is the session's behaviour against a real database
    /// and only the failure point is injected.
    /// </para>
    /// </summary>
    internal sealed class FailingSaveDbContext : AppDbContext
    {
        private readonly int _failOnSaveNumber;

        public FailingSaveDbContext(DbContextOptions<AppDbContext> options, int failOnSaveNumber)
            : base(options)
            => _failOnSaveNumber = failOnSaveNumber;

        public int SaveAttempts { get; private set; }

        public bool WasDisposed { get; private set; }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            SaveAttempts++;
            if (SaveAttempts == _failOnSaveNumber)
                throw new InvalidOperationException($"injected failure on save #{SaveAttempts}");

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override void Dispose()
        {
            WasDisposed = true;
            base.Dispose();
        }

        public override ValueTask DisposeAsync()
        {
            WasDisposed = true;
            return base.DisposeAsync();
        }
    }
}
