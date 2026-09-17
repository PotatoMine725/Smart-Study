using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 2 (mission §11, plan §7.2). Reconciles <see cref="StructuralDependencyRegistry"/>
    /// against the three independent sources of truth it must never drift from, following the
    /// <c>MergeSurfaceRegistryGuardTests</c> precedent (EF lives on this side of the boundary).
    /// </summary>
    public class StructuralDependencyRegistryGuardTests : IDisposable
    {
        private readonly FenceScenarioFixture _fx = new();

        public void Dispose() => _fx.Dispose();

        private static readonly Dictionary<string, Type> ClrTypes = new(StringComparer.Ordinal)
        {
            [SyncEntityTypes.HocKy] = typeof(HocKy),
            [SyncEntityTypes.MonHoc] = typeof(MonHoc),
            [SyncEntityTypes.StudyTask] = typeof(StudyTask),
            [SyncEntityTypes.StudyLog] = typeof(StudyLog),
            [SyncEntityTypes.TaskNote] = typeof(TaskNote),
            [SyncEntityTypes.TaskReferenceLink] = typeof(TaskReferenceLink),
        };

        private static IModel EfModel()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options;
            using var db = new AppDbContext(options);
            return db.Model;
        }

        private sealed record EfCascadeEdge(string ParentType, string ChildType, string ChildField);

        private static IReadOnlyList<EfCascadeEdge> EfCascadeEdges()
        {
            var model = EfModel();
            var reverseClrTypes = ClrTypes.ToDictionary(kv => kv.Value, kv => kv.Key);
            var edges = new List<EfCascadeEdge>();

            foreach (var et in model.GetEntityTypes())
            {
                if (!reverseClrTypes.TryGetValue(et.ClrType, out var childType)) continue;

                foreach (var fk in et.GetForeignKeys())
                {
                    if (fk.DeleteBehavior != DeleteBehavior.Cascade) continue;
                    if (!reverseClrTypes.TryGetValue(fk.PrincipalEntityType.ClrType, out var parentType)) continue;

                    edges.Add(new EfCascadeEdge(parentType, childType, fk.Properties.Single().Name));
                }
            }

            return edges;
        }

        [Fact] // 1a — every EF cascade FK has a matching registry edge with CascadesOnTombstone = true
        public void EveryEfCascadeForeignKey_HasARegistryEdge()
        {
            var missing = EfCascadeEdges()
                .Where(e => !StructuralDependencyRegistry.All.Any(r =>
                    r.ParentType == e.ParentType && r.ChildType == e.ChildType &&
                    r.ChildField == e.ChildField && r.CascadesOnTombstone))
                .ToArray();

            Assert.Equal(Array.Empty<EfCascadeEdge>(), missing);
        }

        [Fact] // 1b — no registry edge claims a cascade the EF model does not have
        public void NoRegistryCascadeEdge_IsPhantom()
        {
            var efEdges = EfCascadeEdges();
            var phantom = StructuralDependencyRegistry.All
                .Where(r => r.CascadesOnTombstone)
                .Where(r => !efEdges.Any(e => e.ParentType == r.ParentType && e.ChildType == r.ChildType && e.ChildField == r.ChildField))
                .ToArray();

            Assert.Equal(Array.Empty<StructuralDependencyRegistry.StructuralEdge>(), phantom);
        }

        [Fact] // 1c — StudyLog is registered but is NOT a cascade edge: no FK exists for it at all
        public void StudyLog_HasNoEfForeignKey_AndDoesNotCascade()
        {
            var model = EfModel();
            var studyLogType = model.FindEntityType(typeof(StudyLog))!;
            Assert.Empty(studyLogType.GetForeignKeys());

            var edge = StructuralDependencyRegistry.All.Single(e => e.ChildType == SyncEntityTypes.StudyLog);
            Assert.False(edge.CascadesOnTombstone);
        }

        [Fact] // 2 — the D4 structural fields agree with MergeSurfaceRegistry's FieldClass.Structural
        public void StructuralFieldOf_MatchesMergeSurfaceRegistry_Structural()
        {
            Assert.Equal(FieldClass.Structural,
                MergeSurfaceRegistry.Get(SyncEntityTypes.MonHoc).Fields.Single(f => f.Name == "MaHocKy").Class);
            Assert.Equal("MaHocKy", StructuralDependencyRegistry.StructuralFieldOf(SyncEntityTypes.MonHoc));

            Assert.Equal(FieldClass.Structural,
                MergeSurfaceRegistry.Get(SyncEntityTypes.StudyTask).Fields.Single(f => f.Name == "MaMonHoc").Class);
            Assert.Equal("MaMonHoc", StructuralDependencyRegistry.StructuralFieldOf(SyncEntityTypes.StudyTask));

            // No other registered type has a structural field.
            foreach (var type in new[] { SyncEntityTypes.HocKy, SyncEntityTypes.TaskNote, SyncEntityTypes.TaskReferenceLink, SyncEntityTypes.StudyLog })
                Assert.Null(StructuralDependencyRegistry.StructuralFieldOf(type));
        }

        [Fact] // 2 — the D5 constraint-scope field agrees with MergeSurfaceRegistry's FieldClass.ConstraintScope
        public void TaskNoteConstraintScopeField_MatchesMergeSurfaceRegistry()
        {
            var edge = StructuralDependencyRegistry.All.Single(e => e.ChildType == SyncEntityTypes.TaskNote);
            Assert.Equal("MaTask", edge.ChildField);
            Assert.Equal(FieldClass.ConstraintScope,
                MergeSurfaceRegistry.Get(SyncEntityTypes.TaskNote).Fields.Single(f => f.Name == "MaTask").Class);
        }

        [Fact] // 3 — behavioural agreement with SyncApplySession's own (private) StructuralFieldOf: the
               // ScopeKey field it stages for a real S1-CR/S1-PT conflict must equal the registry's answer.
        public async System.Threading.Tasks.Task StructuralFieldOf_AgreesWithSyncApplySessionStagedScopeKeys()
        {
            var (crRecord, _, _, _, _, _) = await _fx.StageS1CrAsync();
            Assert.Equal(StructuralDependencyRegistry.StructuralFieldOf(crRecord.EntityType), crRecord.FieldName);

            var (ptRecord, _, _) = await _fx.StageS1PtAsync();
            Assert.Equal(StructuralDependencyRegistry.StructuralFieldOf(ptRecord.EntityType), ptRecord.FieldName);
        }

        [Fact] // registry itself: exactly the six synced entity types are known
        public void IsKnownEntityType_CoversExactlyTheSixSyncedTypes()
        {
            foreach (var type in ClrTypes.Keys)
                Assert.True(StructuralDependencyRegistry.IsKnownEntityType(type));

            Assert.False(StructuralDependencyRegistry.IsKnownEntityType("Nope"));
            Assert.False(StructuralDependencyRegistry.IsKnownEntityType(""));
        }
    }
}
