using System.Linq;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// E-2 (c), owner D-1 (2026-10-03): <see cref="SyncStamper"/> classifies by
    /// <see cref="MergeSurfaceRegistry"/> directly. An entity that fails the stamper's lookup is
    /// "unclassified" and silently falls back to always-stamp, so these guards go through the EXACT
    /// lookup the stamper uses (<see cref="SyncStamper.TryGetSpec"/>) rather than a test-side map.
    /// </summary>
    public class SyncStamperClassificationGuardTests
    {
        [Fact]
        public void EverySyncedEntity_ResolvesThroughTheStampersRegistryLookup()
        {
            using var fx = new SyncApplyFixture();
            using var ctx = fx.NewContext();

            var synced = ctx.Model.GetEntityTypes()
                .Where(t => typeof(ISyncMetadata).IsAssignableFrom(t.ClrType))
                .ToList();
            Assert.Equal(MergeSurfaceRegistry.All.Count, synced.Count);

            foreach (var t in synced)
                Assert.True(SyncStamper.TryGetSpec(t, out _), $"{t.ClrType.Name} is not classified for the stamper");
        }

        [Fact]
        public void EveryDerivedField_IsAMappedPropertyOfItsEntity()
        {
            using var fx = new SyncApplyFixture();
            using var ctx = fx.NewContext();

            var derived = 0;
            foreach (var t in ctx.Model.GetEntityTypes().Where(t => typeof(ISyncMetadata).IsAssignableFrom(t.ClrType)))
            {
                Assert.True(SyncStamper.TryGetSpec(t, out var spec));
                foreach (var f in spec.Fields.Where(f => f.Class == FieldClass.Derived))
                {
                    Assert.NotNull(t.FindProperty(f.Name));
                    derived++;
                }
            }
            Assert.Equal(3, derived);   // DiemUuTien, MucDoCanhBao, IsSeeded (D9-T3)
        }
    }
}
