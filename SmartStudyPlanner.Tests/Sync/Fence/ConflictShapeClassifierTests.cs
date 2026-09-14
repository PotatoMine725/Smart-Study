using System;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 1 (fence spec §4, plan §9). <see cref="ConflictShapeClassifier"/> is pure
    /// and keys on persisted columns only. Covers every supported tuple plus the Unsupported cases the
    /// mission brief §4 names explicitly. Unknown data must fail closed, never fall through to a
    /// known shape.
    /// </summary>
    public class ConflictShapeClassifierTests
    {
        private static readonly Guid EntityId = new("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid LocalId = new("aaaaaaaa-0000-0000-0000-000000000002");
        private static readonly Guid BaseId = new("aaaaaaaa-0000-0000-0000-000000000003");

        private static SyncConflictRecordRow Structural(
            string entityType, string field, StructuralReason? reason,
            Guid? localId, Guid? baseId, Guid? entityId = null,
            ConflictRecordStatus status = ConflictRecordStatus.Unresolved) => new()
        {
            ConflictId = Guid.NewGuid(),
            ScopeKey = $"{entityType}|{(entityId ?? EntityId):D}|{field}",
            Kind = ConflictKind.StructuralConflict,
            EntityType = entityType,
            EntityId = entityId ?? EntityId,
            FieldName = field,
            StructuralReason = reason,
            LocalEntityId = localId,
            BaseEntityId = baseId,
            RemoteEntityId = Guid.NewGuid(),
            RemoteSnapshotJson = "{}",
            RemoteFingerprint = "fp",
            Status = status,
        };

        private static SyncConflictRecordRow Constraint(
            string entityType, string? constraintKey, string? constraintValue, Guid? baseId,
            ConflictRecordStatus status = ConflictRecordStatus.Unresolved) => new()
        {
            ConflictId = Guid.NewGuid(),
            ScopeKey = $"{entityType}|{constraintKey}={constraintValue}",
            Kind = ConflictKind.ConstraintConflict,
            EntityType = entityType,
            ConstraintKey = constraintKey,
            ConstraintValue = constraintValue,
            LocalEntityId = LocalId,
            BaseEntityId = baseId,
            RemoteEntityId = Guid.NewGuid(),
            RemoteSnapshotJson = "{}",
            RemoteFingerprint = "fp",
            Status = status,
        };

        // ---- supported tuples --------------------------------------------------------------

        [Theory]
        [InlineData(SyncEntityTypes.MonHoc, "MaHocKy")]
        [InlineData(SyncEntityTypes.StudyTask, "MaMonHoc")]
        public void ConcurrentReparent_LocalAndBasePresent_ClassifiesAsConcurrentReparent(string type, string field)
        {
            var row = Structural(type, field, StructuralReason.ConcurrentReparent, LocalId, BaseId);
            Assert.Equal(ConflictShape.ConcurrentReparent, ConflictShapeClassifier.Classify(row));
        }

        [Theory]
        [InlineData(SyncEntityTypes.MonHoc, "MaHocKy")]
        [InlineData(SyncEntityTypes.StudyTask, "MaMonHoc")]
        public void ParentTombstoned_LocalAndBasePresent_ClassifiesAsParentTombstoned(string type, string field)
        {
            var row = Structural(type, field, StructuralReason.ParentTombstoned, LocalId, BaseId);
            Assert.Equal(ConflictShape.ParentTombstoned, ConflictShapeClassifier.Classify(row));
        }

        [Theory]
        [InlineData(SyncEntityTypes.MonHoc, "MaHocKy")]
        [InlineData(SyncEntityTypes.StudyTask, "MaMonHoc")]
        public void ParentTombstoned_LocalAndBaseAbsent_ClassifiesAsAbsentLocalParentTombstoned(string type, string field)
        {
            var row = Structural(type, field, StructuralReason.ParentTombstoned, null, null);
            Assert.Equal(ConflictShape.AbsentLocalParentTombstoned, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void Constraint_TaskNote_MaTask_ValidGuid_BasePresent_ClassifiesAsConstraintOccupancy_S2()
        {
            var row = Constraint(SyncEntityTypes.TaskNote, "MaTask", EntityId.ToString("D"), BaseId);
            Assert.Equal(ConflictShape.ConstraintOccupancy, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void Constraint_TaskNote_MaTask_ValidGuid_BaseNull_ClassifiesAsConstraintOccupancy_S3()
        {
            var row = Constraint(SyncEntityTypes.TaskNote, "MaTask", EntityId.ToString("D"), baseId: null);
            Assert.Equal(ConflictShape.ConstraintOccupancy, ConflictShapeClassifier.Classify(row));
        }

        // ---- Unsupported: fail closed ------------------------------------------------------

        [Fact]
        public void NonUnresolvedStatus_IsUnsupported()
        {
            var row = Structural(SyncEntityTypes.StudyTask, "MaMonHoc", StructuralReason.ConcurrentReparent,
                LocalId, BaseId, status: ConflictRecordStatus.Resolved);
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void UnknownStructuralReasonEnumValue_IsUnsupported()
        {
            var row = Structural(SyncEntityTypes.StudyTask, "MaMonHoc", (StructuralReason)99, LocalId, BaseId);
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void UnknownKindEnumValue_IsUnsupported()
        {
            var row = Structural(SyncEntityTypes.StudyTask, "MaMonHoc", StructuralReason.ConcurrentReparent, LocalId, BaseId);
            row.Kind = (ConflictKind)99;
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void UnsupportedEntityType_IsUnsupported()
        {
            // HocKy has no D4 structural field; only MonHoc.MaHocKy and StudyTask.MaMonHoc are known.
            var row = Structural(SyncEntityTypes.HocKy, "MaHocKy", StructuralReason.ConcurrentReparent, LocalId, BaseId);
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void UnsupportedField_IsUnsupported()
        {
            var row = Structural(SyncEntityTypes.StudyTask, "TenTask", StructuralReason.ConcurrentReparent, LocalId, BaseId);
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void FieldConflictKind_IsUnsupported()
        {
            var row = Structural(SyncEntityTypes.StudyTask, "MaMonHoc", StructuralReason.ConcurrentReparent, LocalId, BaseId);
            row.Kind = ConflictKind.FieldConflict;
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void TombstoneConflictKind_IsUnsupported()
        {
            var row = Structural(SyncEntityTypes.StudyTask, "MaMonHoc", StructuralReason.ConcurrentReparent, LocalId, BaseId);
            row.Kind = ConflictKind.TombstoneConflict;
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void MalformedConstraintGuid_IsUnsupported()
        {
            var row = Constraint(SyncEntityTypes.TaskNote, "MaTask", "not-a-guid", BaseId);
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void ConstraintValueNull_IsUnsupported()
        {
            var row = Constraint(SyncEntityTypes.TaskNote, "MaTask", null, BaseId);
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void ConstraintWrongKey_IsUnsupported()
        {
            var row = Constraint(SyncEntityTypes.TaskNote, "SomeOtherKey", EntityId.ToString("D"), BaseId);
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void ConstraintWrongEntityType_IsUnsupported()
        {
            var row = Constraint(SyncEntityTypes.TaskReferenceLink, "MaTask", EntityId.ToString("D"), BaseId);
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void ConcurrentReparent_WithAbsentLocal_IsUnsupported()
        {
            // §4: "ConcurrentReparent with absent local" is explicitly Unsupported, never AL-PT.
            var row = Structural(SyncEntityTypes.StudyTask, "MaMonHoc", StructuralReason.ConcurrentReparent, null, null);
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void ParentTombstoned_LocalAbsentBasePresent_IsUnsupported()
        {
            // §4: "ParentTombstoned with Local absent/Base present" is explicitly Unsupported.
            var row = Structural(SyncEntityTypes.StudyTask, "MaMonHoc", StructuralReason.ParentTombstoned, null, BaseId);
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void ParentTombstoned_LocalPresentBaseAbsent_IsUnsupported()
        {
            var row = Structural(SyncEntityTypes.StudyTask, "MaMonHoc", StructuralReason.ParentTombstoned, LocalId, null);
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void StructuralConflictWithNullEntityId_IsUnsupported()
        {
            var row = Structural(SyncEntityTypes.StudyTask, "MaMonHoc", StructuralReason.ConcurrentReparent, LocalId, BaseId);
            row.EntityId = null;
            Assert.Equal(ConflictShape.Unsupported, ConflictShapeClassifier.Classify(row));
        }

        [Fact]
        public void NullRow_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ConflictShapeClassifier.Classify(null!));
        }
    }
}
