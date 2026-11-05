using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartStudyPlanner.Sync.Merge
{
    /// <summary>Classification of every EF-mapped, non-navigation property (DoR §4.2).</summary>
    public enum FieldClass
    {
        Identity,        // PK -- carried in EntityRef, never in SnapshotJson (D2)
        Structural,      // D4 FK: MonHoc.MaHocKy, StudyTask.MaMonHoc
        ConstraintScope, // D5 scope key: TaskNote.MaTask
        Merge,           // ordinary LWW field
        CopyOnCreate,    // creation provenance / creation-time reference, immutable (D9-T3)
        Derived,         // derived or dev state, excluded (D9-T3)
        NotMapped,       // [NotMapped], no column
        SyncMetadata,    // Rev / ModifiedAtUtc / ModifiedByDeviceId
        Tombstone        // IsDeleted / DeletedAtUtc
    }

    public sealed record FieldSpec(string Name, FieldClass Class, Type ValueType, bool IsUtc);

    /// <summary>Field order is the canonical order (DoR §5.1 rule 3).</summary>
    public sealed record EntitySpec(string EntityType, IReadOnlyList<FieldSpec> Fields)
    {
        /// <summary>
        /// The fields that appear in SnapshotJson, in canonical order: everything except
        /// Identity / Derived / NotMapped / SyncMetadata / Tombstone (DoR §5.1 rule 3).
        /// </summary>
        public IReadOnlyList<FieldSpec> SnapshotFields { get; } = Fields
            .Where(f => f.Class is FieldClass.Structural or FieldClass.ConstraintScope
                                or FieldClass.Merge or FieldClass.CopyOnCreate)
            .ToArray();
    }

    /// <summary>
    /// The frozen six-entity merge-surface registry (DoR §4.2). Encoded verbatim from the
    /// ratified table. Deliberately references NO type from <c>SmartStudyPlanner.Models</c>:
    /// enums are carried as their underlying <see cref="int"/>, status strings as
    /// <see cref="string"/>. The reconciliation against EF's model lives in the test project
    /// (MergeSurfaceRegistryGuardTests, DoR §4.3), where EF is allowed.
    /// </summary>
    public static class MergeSurfaceRegistry
    {
        // Sync-metadata block, identical on all six entities (DoR §4.2). Rev is registered as
        // SyncMetadata so the guard test can see it is classified, and is NEVER in a snapshot.
        private static IEnumerable<FieldSpec> MetadataBlock() => new[]
        {
            new FieldSpec("Rev", FieldClass.SyncMetadata, typeof(long), false),
            new FieldSpec("ModifiedAtUtc", FieldClass.SyncMetadata, typeof(DateTime), true),
            new FieldSpec("ModifiedByDeviceId", FieldClass.SyncMetadata, typeof(string), false),
            new FieldSpec("IsDeleted", FieldClass.Tombstone, typeof(bool), false),
            new FieldSpec("DeletedAtUtc", FieldClass.Tombstone, typeof(DateTime), true),
        };

        private static EntitySpec Spec(string entityType, params FieldSpec[] fields) =>
            new(entityType, fields.Concat(MetadataBlock()).ToArray());

        private static readonly EntitySpec HocKySpec = Spec(SyncEntityTypes.HocKy,
            new FieldSpec("MaHocKy", FieldClass.Identity, typeof(Guid), false),
            new FieldSpec("Ten", FieldClass.Merge, typeof(string), false),
            new FieldSpec("NgayBatDau", FieldClass.Merge, typeof(DateTime), false),   // WallClock
            new FieldSpec("IsSeeded", FieldClass.Derived, typeof(bool), false),       // D9-T3
            new FieldSpec("NgayKetThuc", FieldClass.NotMapped, typeof(DateTime), false),
            new FieldSpec("IsNgayKetThucAuto", FieldClass.NotMapped, typeof(bool), false));

        private static readonly EntitySpec MonHocSpec = Spec(SyncEntityTypes.MonHoc,
            new FieldSpec("MaMonHoc", FieldClass.Identity, typeof(Guid), false),
            new FieldSpec("MaHocKy", FieldClass.Structural, typeof(Guid), false),     // D4
            new FieldSpec("TenMonHoc", FieldClass.Merge, typeof(string), false),
            new FieldSpec("SoTinChi", FieldClass.Merge, typeof(int), false));

        private static readonly EntitySpec StudyTaskSpec = Spec(SyncEntityTypes.StudyTask,
            new FieldSpec("MaTask", FieldClass.Identity, typeof(Guid), false),
            new FieldSpec("MaMonHoc", FieldClass.Structural, typeof(Guid), false),    // D4
            new FieldSpec("TenTask", FieldClass.Merge, typeof(string), false),
            new FieldSpec("HanChot", FieldClass.Merge, typeof(DateTime), false),      // WallClock
            new FieldSpec("TrangThai", FieldClass.Merge, typeof(string), false),
            new FieldSpec("LoaiTask", FieldClass.Merge, typeof(int), false),          // enum -> underlying int
            new FieldSpec("DoKho", FieldClass.Merge, typeof(int), false),
            new FieldSpec("ThoiGianDaHoc", FieldClass.Merge, typeof(int), false),
            new FieldSpec("NgayHoanThanh", FieldClass.Merge, typeof(DateTime), false),// WallClock, nullable
            new FieldSpec("DiemUuTien", FieldClass.Derived, typeof(double), false),   // D9-T3
            new FieldSpec("MucDoCanhBao", FieldClass.Derived, typeof(string), false));// D9-T3

        private static readonly EntitySpec StudyLogSpec = Spec(SyncEntityTypes.StudyLog,
            new FieldSpec("Id", FieldClass.Identity, typeof(Guid), false),
            new FieldSpec("MaTask", FieldClass.CopyOnCreate, typeof(Guid), false),    // D9-T3
            new FieldSpec("NgayHoc", FieldClass.Merge, typeof(DateTime), false),      // WallClock
            new FieldSpec("SoPhutHoc", FieldClass.Merge, typeof(int), false),
            new FieldSpec("SoPhutDuKien", FieldClass.Merge, typeof(int), false),
            new FieldSpec("DaHoanThanh", FieldClass.Merge, typeof(bool), false),
            new FieldSpec("GhiChu", FieldClass.Merge, typeof(string), false),
            new FieldSpec("CreatedAtUtc", FieldClass.CopyOnCreate, typeof(DateTime), true),
            new FieldSpec("DeviceId", FieldClass.CopyOnCreate, typeof(string), false));

        private static readonly EntitySpec TaskNoteSpec = Spec(SyncEntityTypes.TaskNote,
            new FieldSpec("Id", FieldClass.Identity, typeof(Guid), false),
            new FieldSpec("MaTask", FieldClass.ConstraintScope, typeof(Guid), false), // D5
            new FieldSpec("Content", FieldClass.Merge, typeof(string), false));

        private static readonly EntitySpec TaskReferenceLinkSpec = Spec(SyncEntityTypes.TaskReferenceLink,
            new FieldSpec("Id", FieldClass.Identity, typeof(Guid), false),
            new FieldSpec("MaTask", FieldClass.CopyOnCreate, typeof(Guid), false),    // D9-T3
            new FieldSpec("Title", FieldClass.Merge, typeof(string), false),
            new FieldSpec("Url", FieldClass.Merge, typeof(string), false),
            new FieldSpec("Category", FieldClass.Merge, typeof(string), false),
            new FieldSpec("SortOrder", FieldClass.Merge, typeof(int), false),
            new FieldSpec("CreatedAtUtc", FieldClass.CopyOnCreate, typeof(DateTime), true));

        private static readonly IReadOnlyDictionary<string, EntitySpec> Specs =
            new Dictionary<string, EntitySpec>(StringComparer.Ordinal)
            {
                [SyncEntityTypes.HocKy] = HocKySpec,
                [SyncEntityTypes.MonHoc] = MonHocSpec,
                [SyncEntityTypes.StudyTask] = StudyTaskSpec,
                [SyncEntityTypes.StudyLog] = StudyLogSpec,
                [SyncEntityTypes.TaskNote] = TaskNoteSpec,
                [SyncEntityTypes.TaskReferenceLink] = TaskReferenceLinkSpec,
            };

        /// <summary>All six entity specs, in registry order.</summary>
        public static IReadOnlyList<EntitySpec> All { get; } = new[]
        {
            HocKySpec, MonHocSpec, StudyTaskSpec, StudyLogSpec, TaskNoteSpec, TaskReferenceLinkSpec
        };

        public static bool TryGet(string entityType, out EntitySpec spec) =>
            Specs.TryGetValue(entityType, out spec!);

        /// <summary>Throws <see cref="SnapshotContractException"/> when the type is not on the surface.</summary>
        public static EntitySpec Get(string entityType) =>
            Specs.TryGetValue(entityType, out var s)
                ? s
                : throw new SnapshotContractException(
                    SnapshotContractReason.UnknownEntityType,
                    $"Unknown entityType '{entityType}'.");
    }
}
