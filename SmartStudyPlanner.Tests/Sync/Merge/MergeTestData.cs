using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Tests.Sync.Merge
{
    /// <summary>
    /// Builders for the pure merge core (Epic 2 / T2.3). Deliberately construct
    /// <see cref="EntitySnapshot"/> by hand -- the entity to snapshot mapper is T2.4 (PR-3),
    /// and the core must be provable without EF.
    /// </summary>
    internal static class MergeTestData
    {
        public static readonly Guid G1 = new("0f3a1b2c-0000-0000-0000-000000000001");
        public static readonly Guid G2 = new("0f3a1b2c-0000-0000-0000-000000000002");
        public static readonly Guid G3 = new("0f3a1b2c-0000-0000-0000-000000000003");

        public static readonly DateTime Utc0 =
            new DateTime(2026, 9, 8, 1, 2, 3, DateTimeKind.Utc).AddTicks(1234567);
        public static readonly DateTime Wall0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        public static Provenance Prov(long ticks, string device, bool deleted = false, DateTime? deletedAt = null)
            => new(new DateTime(ticks, DateTimeKind.Utc), device, deleted, deletedAt);

        public static Provenance Live(long ticks = 1000, string device = "dev-a")
            => Prov(ticks, device);

        public static Provenance Dead(long ticks = 1000, string device = "dev-a")
            => Prov(ticks, device, true, new DateTime(ticks, DateTimeKind.Utc));

        private static FieldValue DefaultFor(FieldSpec f)
        {
            if (f.ValueType == typeof(string)) return new StringValue("v-" + f.Name);
            if (f.ValueType == typeof(int)) return new Int32Value(0);
            if (f.ValueType == typeof(bool)) return new BoolValue(false);
            if (f.ValueType == typeof(Guid)) return new GuidValue(G1);
            if (f.ValueType == typeof(DateTime)) return f.IsUtc
                ? new UtcValue(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                : new WallClockValue(Wall0);
            throw new InvalidOperationException("No default for " + f.ValueType);
        }

        /// <summary>A fully populated snapshot for <paramref name="entityType"/> with the registry's field set.</summary>
        public static EntitySnapshot Snap(string entityType, Provenance? provenance = null,
                                          params (string Field, FieldValue Value)[] overrides)
        {
            var spec = MergeSurfaceRegistry.Get(entityType);
            var fields = new Dictionary<string, FieldValue>(StringComparer.Ordinal);
            foreach (var f in spec.SnapshotFields) fields[f.Name] = DefaultFor(f);
            foreach (var (name, value) in overrides)
            {
                if (!fields.ContainsKey(name))
                    throw new InvalidOperationException($"'{name}' is not a snapshot field of {entityType}.");
                fields[name] = value;
            }
            return new EntitySnapshot(entityType, provenance ?? Live(), fields);
        }

        public static EntitySnapshot With(this EntitySnapshot s, string field, FieldValue value)
        {
            var fields = new Dictionary<string, FieldValue>(s.Fields, StringComparer.Ordinal);
            if (!fields.ContainsKey(field))
                throw new InvalidOperationException($"'{field}' is not a snapshot field of {s.EntityType}.");
            fields[field] = value;
            return s with { Fields = fields };
        }

        public static EntitySnapshot WithProv(this EntitySnapshot s, Provenance p) => s with { Provenance = p };

        /// <summary>Same content, different dictionary insertion order (test E-2).</summary>
        public static EntitySnapshot Shuffled(this EntitySnapshot s, int seed = 7)
        {
            var rnd = new Random(seed);
            var reordered = s.Fields.OrderBy(_ => rnd.Next()).ToList();
            var fields = new Dictionary<string, FieldValue>(StringComparer.Ordinal);
            foreach (var kv in reordered) fields[kv.Key] = kv.Value;
            return s with { Fields = fields };
        }

        public static EntityRef Ref(string entityType, Guid id) => new(entityType, id);

        /// <summary>First <see cref="FieldClass.Merge"/> field of a spec, used by the per-entity theories.</summary>
        public static string FirstMergeField(string entityType) =>
            MergeSurfaceRegistry.Get(entityType).Fields.First(f => f.Class == FieldClass.Merge).Name;

        public static FieldValue Bump(FieldValue v) => v switch
        {
            StringValue s => new StringValue((s.Value ?? "") + "-x"),
            Int32Value i => new Int32Value(i.Value + 1),
            BoolValue b => new BoolValue(!b.Value),
            GuidValue g => new GuidValue(g.Value == G1 ? G2 : G1),
            WallClockValue w => new WallClockValue((w.Value ?? Wall0).AddDays(1)),
            UtcValue u => new UtcValue((u.Value ?? Utc0).AddDays(1)),
            _ => throw new InvalidOperationException("unhandled " + v.GetType().Name)
        };

        public static IEnumerable<object[]> AllEntityTypes() => new[]
        {
            new object[] { SyncEntityTypes.HocKy },
            new object[] { SyncEntityTypes.MonHoc },
            new object[] { SyncEntityTypes.StudyTask },
            new object[] { SyncEntityTypes.StudyLog },
            new object[] { SyncEntityTypes.TaskNote },
            new object[] { SyncEntityTypes.TaskReferenceLink },
        };
    }
}
