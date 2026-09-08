using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Merge
{
    /// <summary>
    /// G-1 (DoR §4.3) — the registry guard. EF lives on THIS side of the boundary: the pure core
    /// must never reference it, so the reconciliation between EF's model and the frozen registry
    /// happens here. A property added to a model without a registry entry fails this test.
    /// </summary>
    public class MergeSurfaceRegistryGuardTests
    {
        // Model building opens no connection; Clock/DeviceIdProvider are lazy and never invoked.
        private static IModel EfModel()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("Data Source=:memory:").Options;
            using var db = new AppDbContext(options);
            return db.Model;
        }

        private static readonly Dictionary<string, Type> ClrTypes = new(StringComparer.Ordinal)
        {
            [SyncEntityTypes.HocKy] = typeof(HocKy),
            [SyncEntityTypes.MonHoc] = typeof(MonHoc),
            [SyncEntityTypes.StudyTask] = typeof(StudyTask),
            [SyncEntityTypes.StudyLog] = typeof(StudyLog),
            [SyncEntityTypes.TaskNote] = typeof(TaskNote),
            [SyncEntityTypes.TaskReferenceLink] = typeof(TaskReferenceLink),
        };

        [Fact] // G-1a — every EF-mapped scalar property is classified exactly once
        public void EveryEfMappedScalarProperty_IsClassifiedInTheRegistry()
        {
            var model = EfModel();
            var unclassified = new List<string>();

            foreach (var spec in MergeSurfaceRegistry.All)
            {
                var et = model.FindEntityType(ClrTypes[spec.EntityType])
                         ?? throw new InvalidOperationException($"{spec.EntityType} is not in the EF model.");

                foreach (var p in et.GetProperties())          // scalars only; navigations excluded
                {
                    if (p.IsShadowProperty()) continue;
                    if (spec.Fields.Count(f => f.Name == p.Name) != 1)
                        unclassified.Add($"{spec.EntityType}.{p.Name}");
                }
            }

            Assert.Equal(Array.Empty<string>(), unclassified.ToArray());
        }

        [Fact] // G-1b — the registry invents nothing: every non-NotMapped entry exists in EF
        public void EveryRegistryEntry_ExceptNotMapped_ExistsInTheEfModel()
        {
            var model = EfModel();
            var phantom = new List<string>();

            foreach (var spec in MergeSurfaceRegistry.All)
            {
                var et = model.FindEntityType(ClrTypes[spec.EntityType])!;
                var efNames = et.GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

                foreach (var f in spec.Fields.Where(f => f.Class != FieldClass.NotMapped))
                    if (!efNames.Contains(f.Name)) phantom.Add($"{spec.EntityType}.{f.Name}");
            }

            Assert.Equal(Array.Empty<string>(), phantom.ToArray());
        }

        [Fact] // G-1c — no floating-point type on the merge surface (DoR F-o); this is what keeps
               // canonical serialization free of float formatting.
        public void NoMergeSurfaceField_HasAFloatingPointType()
        {
            var floats = MergeSurfaceRegistry.All
                .SelectMany(s => s.SnapshotFields.Select(f => (s.EntityType, f)))
                .Where(x => x.f.ValueType == typeof(double) || x.f.ValueType == typeof(float)
                         || x.f.ValueType == typeof(decimal))
                .Select(x => $"{x.EntityType}.{x.f.Name}")
                .ToArray();

            Assert.Equal(Array.Empty<string>(), floats);
        }

        [Fact] // G-1d — the D9-T3 exclusions are encoded, not merely documented
        public void D9T3Exclusions_AreEncodedInTheRegistry()
        {
            void AssertClass(string entityType, string field, FieldClass expected) =>
                Assert.Equal(expected, MergeSurfaceRegistry.Get(entityType).Fields.Single(f => f.Name == field).Class);

            AssertClass(SyncEntityTypes.HocKy, "IsSeeded", FieldClass.Derived);
            AssertClass(SyncEntityTypes.StudyTask, "DiemUuTien", FieldClass.Derived);
            AssertClass(SyncEntityTypes.StudyTask, "MucDoCanhBao", FieldClass.Derived);
            AssertClass(SyncEntityTypes.StudyLog, "DeviceId", FieldClass.CopyOnCreate);
            AssertClass(SyncEntityTypes.StudyLog, "CreatedAtUtc", FieldClass.CopyOnCreate);
            AssertClass(SyncEntityTypes.StudyLog, "MaTask", FieldClass.CopyOnCreate);
            AssertClass(SyncEntityTypes.TaskReferenceLink, "CreatedAtUtc", FieldClass.CopyOnCreate);
            AssertClass(SyncEntityTypes.TaskReferenceLink, "MaTask", FieldClass.CopyOnCreate);
            AssertClass(SyncEntityTypes.MonHoc, "MaHocKy", FieldClass.Structural);
            AssertClass(SyncEntityTypes.StudyTask, "MaMonHoc", FieldClass.Structural);
            AssertClass(SyncEntityTypes.TaskNote, "MaTask", FieldClass.ConstraintScope);

            // excluded classes never reach a snapshot
            foreach (var spec in MergeSurfaceRegistry.All)
                Assert.All(spec.SnapshotFields, f => Assert.True(
                    f.Class is FieldClass.Structural or FieldClass.ConstraintScope
                            or FieldClass.Merge or FieldClass.CopyOnCreate,
                    $"{spec.EntityType}.{f.Name} has excluded class {f.Class}"));
        }

        [Fact] // G-1e — exactly the six synced entity types, and only those
        public void RegistryCoversExactlyTheSixSyncedEntityTypes()
        {
            Assert.Equal(
                new[] { "HocKy", "MonHoc", "StudyLog", "StudyTask", "TaskNote", "TaskReferenceLink" },
                MergeSurfaceRegistry.All.Select(s => s.EntityType).OrderBy(n => n, StringComparer.Ordinal));

            Assert.False(MergeSurfaceRegistry.TryGet("SyncBaseSnapshotRow", out _));
            Assert.Throws<SnapshotContractException>(() => MergeSurfaceRegistry.Get("Nope"));
        }

        [Fact] // canonical order is the registry order and is stable
        public void SnapshotFieldOrder_MatchesTheRatifiedTable()
        {
            Assert.Equal(new[] { "Ten", "NgayBatDau" },
                MergeSurfaceRegistry.Get(SyncEntityTypes.HocKy).SnapshotFields.Select(f => f.Name));

            Assert.Equal(new[] { "MaHocKy", "TenMonHoc", "SoTinChi" },
                MergeSurfaceRegistry.Get(SyncEntityTypes.MonHoc).SnapshotFields.Select(f => f.Name));

            Assert.Equal(new[] { "MaMonHoc", "TenTask", "HanChot", "TrangThai", "LoaiTask",
                                 "DoKho", "ThoiGianDaHoc", "NgayHoanThanh" },
                MergeSurfaceRegistry.Get(SyncEntityTypes.StudyTask).SnapshotFields.Select(f => f.Name));

            Assert.Equal(new[] { "MaTask", "NgayHoc", "SoPhutHoc", "SoPhutDuKien", "DaHoanThanh",
                                 "GhiChu", "CreatedAtUtc", "DeviceId" },
                MergeSurfaceRegistry.Get(SyncEntityTypes.StudyLog).SnapshotFields.Select(f => f.Name));

            Assert.Equal(new[] { "MaTask", "Content" },
                MergeSurfaceRegistry.Get(SyncEntityTypes.TaskNote).SnapshotFields.Select(f => f.Name));

            Assert.Equal(new[] { "MaTask", "Title", "Url", "Category", "SortOrder", "CreatedAtUtc" },
                MergeSurfaceRegistry.Get(SyncEntityTypes.TaskReferenceLink).SnapshotFields.Select(f => f.Name));
        }
    }
}
