using System;
using System.Linq;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Merge
{
    /// <summary>
    /// D-1..D-5 — tombstone-wins (DoR §7.3) and the D9-T5 auto-resolution evidence kinds.
    /// The pure core only PRODUCES this evidence; persisting it is T2.4.
    /// </summary>
    public class TombstoneAndEvidenceTests
    {
        private const string Type = SyncEntityTypes.StudyTask;
        private static readonly Guid Id = MergeTestData.G3;
        private static EntityRef Ref => MergeTestData.Ref(Type, Id);

        [Fact] // D-1 — both sides deleted: tombstone, deterministic provenance, NO evidence
        public void BothSidesTombstoned_ProducesTombstoneWithGreaterProvenance_AndNoEvidence()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var l = b.WithProv(MergeTestData.Dead(200, "d1"));
            var r = b.WithProv(MergeTestData.Dead(300, "d2"));

            var m = ThreeWayMerge.Merge(Ref, b, l, r);

            Assert.True(m.Result!.Provenance.IsDeleted);
            Assert.Equal(r.Provenance, m.ResultProvenance);
            Assert.Empty(m.Candidates);

            // symmetric
            var mirrored = ThreeWayMerge.Merge(Ref, b, r, l);
            Assert.Equal(r.Provenance, mirrored.ResultProvenance);
        }

        [Fact] // D-2 — delete-vs-edit: the tombstone wins even though the editor is later
        public void LocalTombstoneBeatsLaterRemoteEdit_AndEmitsAutoTombstoneEvidence()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var l = b.WithProv(MergeTestData.Dead(200, "d1"));
            var r = b.With("TenTask", new StringValue("still editing"))
                     .WithProv(MergeTestData.Live(9_000, "d2"));   // much later, still loses

            var m = ThreeWayMerge.Merge(Ref, b, l, r);

            Assert.True(m.Result!.Provenance.IsDeleted);
            Assert.Equal(l.Provenance, m.ResultProvenance);

            var c = Assert.Single(m.Candidates);
            Assert.Equal(ConflictKind.TombstoneConflict, c.Kind);
            Assert.Equal(ResolutionKind.AutoTombstone, c.AutoResolution);
            Assert.Equal(l, c.AutoResult);
            Assert.Equal(b, c.Base);
            Assert.Equal(l, c.Local);
            Assert.Equal(r, c.Remote);
            Assert.Equal(Id, c.EntityId);
        }

        [Fact] // D-4 — mirror: remote tombstone wins over a later local edit
        public void RemoteTombstoneBeatsLaterLocalEdit_AndEmitsAutoTombstoneEvidence()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var l = b.With("TenTask", new StringValue("still editing"))
                     .WithProv(MergeTestData.Live(9_000, "d1"));
            var r = b.WithProv(MergeTestData.Dead(200, "d2"));

            var m = ThreeWayMerge.Merge(Ref, b, l, r);

            Assert.True(m.Result!.Provenance.IsDeleted);
            Assert.Equal(r.Provenance, m.ResultProvenance);

            var c = Assert.Single(m.Candidates);
            Assert.Equal(ConflictKind.TombstoneConflict, c.Kind);
            Assert.Equal(ResolutionKind.AutoTombstone, c.AutoResolution);
            Assert.Equal(r, c.AutoResult);
        }

        [Fact] // D-3 — delete vs an unchanged live side: no competing claim, so NO evidence
        public void TombstoneAgainstUnchangedLiveSide_EmitsNoEvidence()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var l = b.WithProv(MergeTestData.Dead(200, "d1"));
            var r = b.WithProv(MergeTestData.Live(300, "d2"));   // provenance moved, fields did not

            var m = ThreeWayMerge.Merge(Ref, b, l, r);

            Assert.True(m.Result!.Provenance.IsDeleted);
            Assert.Empty(m.Candidates);
        }

        [Fact] // D-5 — AutoLww evidence: one FieldConflict candidate per case-5 field
        public void Case5Fields_EmitAutoLwwEvidence_WithoutChangingTheResult()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var l = b.With("TenTask", new StringValue("L"))
                     .With("TrangThai", new StringValue("LS"))
                     .WithProv(MergeTestData.Live(200, "d1"));
            var r = b.With("TenTask", new StringValue("R"))
                     .With("TrangThai", new StringValue("RS"))
                     .With("DoKho", new Int32Value(5))          // case 3, no evidence
                     .WithProv(MergeTestData.Live(300, "d2"));

            var m = ThreeWayMerge.Merge(Ref, b, l, r);

            var lww = m.Candidates.Where(c => c.Kind == ConflictKind.FieldConflict).ToList();
            Assert.Equal(2, lww.Count);
            Assert.All(lww, c => Assert.Equal(ResolutionKind.AutoLww, c.AutoResolution));
            Assert.Equal(new[] { "TenTask", "TrangThai" },
                         lww.Select(c => c.FieldName).OrderBy(n => n, StringComparer.Ordinal));
            Assert.All(lww, c => Assert.Equal(r, c.AutoResult));   // remote is the later writer

            // evidence never changes the merged result
            Assert.Equal(new StringValue("R"), m.Result!.Fields["TenTask"]);
            Assert.Equal(new Int32Value(5), m.Result.Fields["DoKho"]);
            Assert.Equal(FieldDecision.Remote, m.Outcomes.Single(o => o.Field == "DoKho").Decision);
        }

        [Fact] // resurrection does not exist in the model — fail closed, never a conflict
        public void BaseTombstonedWithALiveSide_IsAContractViolation()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Dead(100, "d0"));
            var live = b.WithProv(MergeTestData.Live(200, "d1"));
            var dead = b.WithProv(MergeTestData.Dead(300, "d2"));

            Assert.Throws<MergeContractViolationException>(() => ThreeWayMerge.Merge(Ref, b, live, dead));
            Assert.Throws<MergeContractViolationException>(() => ThreeWayMerge.Merge(Ref, b, dead, live));
        }

        [Fact] // B dead, L dead, R dead => no-op
        public void AllThreeTombstoned_IsANoOp()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Dead(100, "d0"));
            var m = ThreeWayMerge.Merge(Ref, b,
                b.WithProv(MergeTestData.Dead(200, "d1")), b.WithProv(MergeTestData.Dead(300, "d2")));

            Assert.True(m.Result!.Provenance.IsDeleted);
            Assert.Empty(m.Candidates);
            Assert.True(m.IsNoOp);
        }

        [Fact] // tombstone-wins is evaluated BEFORE field merge: no field outcomes leak through
        public void TombstoneShortCircuitsFieldMerge()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var l = b.WithProv(MergeTestData.Dead(200, "d1"));
            var r = b.With("TenTask", new StringValue("R")).WithProv(MergeTestData.Live(300, "d2"));

            var m = ThreeWayMerge.Merge(Ref, b, l, r);

            Assert.Empty(m.Outcomes);
            Assert.Equal(b.Fields["TenTask"], m.Result!.Fields["TenTask"]);
        }
    }
}
