using System;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 1 (fence spec §4, plan §9). Pure: keys on persisted columns, never on
    /// names/strings beyond the fixed structural-field table below (E-11 precedent). Unknown data
    /// fails closed to <see cref="ConflictShape.Unsupported"/>; it never falls through to a guessed
    /// known shape.
    /// </summary>
    public static class ConflictShapeClassifier
    {
        public static ConflictShape Classify(SyncConflictRecordRow unresolved)
        {
            if (unresolved is null) throw new ArgumentNullException(nameof(unresolved));

            if (unresolved.Status != ConflictRecordStatus.Unresolved)
                return ConflictShape.Unsupported;

            return unresolved.Kind switch
            {
                ConflictKind.StructuralConflict => ClassifyStructural(unresolved),
                ConflictKind.ConstraintConflict => ClassifyConstraint(unresolved),
                _ => ConflictShape.Unsupported, // FieldConflict / TombstoneConflict / unknown enum int
            };
        }

        private static ConflictShape ClassifyStructural(SyncConflictRecordRow row)
        {
            if (row.EntityId is null) return ConflictShape.Unsupported;
            if (!IsKnownStructuralField(row.EntityType, row.FieldName)) return ConflictShape.Unsupported;
            if (row.StructuralReason is null) return ConflictShape.Unsupported;

            var localPresent = row.LocalEntityId is not null;
            var basePresent = row.BaseEntityId is not null;

            return row.StructuralReason.Value switch
            {
                StructuralReason.ConcurrentReparent =>
                    localPresent && basePresent ? ConflictShape.ConcurrentReparent : ConflictShape.Unsupported,

                StructuralReason.ParentTombstoned => (localPresent, basePresent) switch
                {
                    (true, true) => ConflictShape.ParentTombstoned,
                    (false, false) => ConflictShape.AbsentLocalParentTombstoned,
                    // Local absent/Base present, or Local present/Base absent: malformed, fail closed.
                    _ => ConflictShape.Unsupported,
                },

                _ => ConflictShape.Unsupported, // unknown StructuralReason enum value
            };
        }

        private static bool IsKnownStructuralField(string entityType, string? field) =>
            (entityType == SyncEntityTypes.MonHoc && field == "MaHocKy") ||
            (entityType == SyncEntityTypes.StudyTask && field == "MaMonHoc");

        private static ConflictShape ClassifyConstraint(SyncConflictRecordRow row)
        {
            if (row.EntityType != SyncEntityTypes.TaskNote) return ConflictShape.Unsupported;
            if (row.ConstraintKey != "MaTask") return ConflictShape.Unsupported;

            return row.ConstraintValue is not null && Guid.TryParseExact(row.ConstraintValue, "D", out _)
                ? ConflictShape.ConstraintOccupancy
                : ConflictShape.Unsupported;
        }
    }
}
