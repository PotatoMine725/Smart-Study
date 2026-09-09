using System;
using System.Linq;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Merge
{
    /// <summary>
    /// S-1..S-4 — D4 structural fields (MonHoc.MaHocKy, StudyTask.MaMonHoc). Never LWW; a
    /// concurrent reparent has no auto-winner and leaves live state at Base (D9-T1).
    /// Parent existence is an apply-layer concern (D4 / D9-T4) and is not examined here.
    /// </summary>
    public class StructuralMergeTests
    {
        public static TheoryData<string, string> StructuralFields => new()
        {
            { SyncEntityTypes.MonHoc, "MaHocKy" },
            { SyncEntityTypes.StudyTask, "MaMonHoc" },
        };

        private static readonly Guid P0 = new("11111111-0000-0000-0000-00000000000a");
        private static readonly Guid P1 = new("11111111-0000-0000-0000-00000000000b");
        private static readonly Guid P2 = new("11111111-0000-0000-0000-00000000000c");

        [Theory, MemberData(nameof(StructuralFields))] // S-1
        public void NeitherSideReparented_TakesBase(string type, string field)
        {
            var b = MergeTestData.Snap(type, MergeTestData.Live(100, "d0")).With(field, new GuidValue(P0));
            var m = ThreeWayMerge.Merge(MergeTestData.Ref(type, MergeTestData.G3), b,
                b.WithProv(MergeTestData.Live(200, "d1")), b.WithProv(MergeTestData.Live(300, "d2")));

            Assert.Equal(FieldDecision.Base, m.Outcomes.Single(o => o.Field == field).Decision);
            Assert.Equal(new GuidValue(P0), m.Result!.Fields[field]);
            Assert.Empty(m.Candidates);
        }

        [Theory, MemberData(nameof(StructuralFields))] // S-2
        public void OneSideReparented_IsAccepted(string type, string field)
        {
            var b = MergeTestData.Snap(type, MergeTestData.Live(100, "d0")).With(field, new GuidValue(P0));

            var localMoved = ThreeWayMerge.Merge(MergeTestData.Ref(type, MergeTestData.G3), b,
                b.With(field, new GuidValue(P1)).WithProv(MergeTestData.Live(200, "d1")),
                b.WithProv(MergeTestData.Live(9_000, "d2")));   // remote is later and still loses
            Assert.Equal(FieldDecision.Local, localMoved.Outcomes.Single(o => o.Field == field).Decision);
            Assert.Equal(new GuidValue(P1), localMoved.Result!.Fields[field]);
            Assert.Empty(localMoved.Candidates);

            var remoteMoved = ThreeWayMerge.Merge(MergeTestData.Ref(type, MergeTestData.G3), b,
                b.WithProv(MergeTestData.Live(9_000, "d1")),
                b.With(field, new GuidValue(P2)).WithProv(MergeTestData.Live(200, "d2")));
            Assert.Equal(FieldDecision.Remote, remoteMoved.Outcomes.Single(o => o.Field == field).Decision);
            Assert.Equal(new GuidValue(P2), remoteMoved.Result!.Fields[field]);
            Assert.Empty(remoteMoved.Candidates);
        }

        [Theory, MemberData(nameof(StructuralFields))] // S-3
        public void BothReparentedToTheSameTarget_IsCommon(string type, string field)
        {
            var b = MergeTestData.Snap(type, MergeTestData.Live(100, "d0")).With(field, new GuidValue(P0));
            var m = ThreeWayMerge.Merge(MergeTestData.Ref(type, MergeTestData.G3), b,
                b.With(field, new GuidValue(P1)).WithProv(MergeTestData.Live(200, "d1")),
                b.With(field, new GuidValue(P1)).WithProv(MergeTestData.Live(300, "d2")));

            Assert.Equal(FieldDecision.Common, m.Outcomes.Single(o => o.Field == field).Decision);
            Assert.Equal(new GuidValue(P1), m.Result!.Fields[field]);
            Assert.Empty(m.Candidates);
        }

        [Theory, MemberData(nameof(StructuralFields))] // S-4
        public void ConcurrentReparentToDifferentTargets_IsAStructuralConflict_AndLiveStaysAtBase(
            string type, string field)
        {
            var id = MergeTestData.G3;
            var b = MergeTestData.Snap(type, MergeTestData.Live(100, "d0")).With(field, new GuidValue(P0));
            var l = b.With(field, new GuidValue(P1)).WithProv(MergeTestData.Live(200, "d1"));
            var r = b.With(field, new GuidValue(P2)).WithProv(MergeTestData.Live(300, "d2"));

            var m = ThreeWayMerge.Merge(MergeTestData.Ref(type, id), b, l, r);

            Assert.Equal(FieldDecision.StructuralConflict, m.Outcomes.Single(o => o.Field == field).Decision);

            // D9-T1: the WHOLE live row stays at Base while unresolved.
            Assert.Equal(b, m.Result);
            Assert.Equal(b.Provenance, m.ResultProvenance);

            var c = Assert.Single(m.Candidates);
            Assert.Equal(ConflictKind.StructuralConflict, c.Kind);
            Assert.Equal(StructuralReason.ConcurrentReparent, c.Reason);
            Assert.Null(c.AutoResolution);      // no auto-winner
            Assert.Null(c.AutoResult);
            Assert.Equal(field, c.FieldName);
            Assert.Equal(id, c.EntityId);

            // full-row evidence, so a later KeepLocal/KeepRemote can restore the other fields too
            Assert.Equal(b, c.Base);
            Assert.Equal(l, c.Local);
            Assert.Equal(r, c.Remote);
        }

        [Theory, MemberData(nameof(StructuralFields))] // S-4b
        public void StructuralConflict_SuppressesPartialApplicationOfOtherFields(string type, string field)
        {
            var other = MergeSurfaceRegistry.Get(type).SnapshotFields
                                            .First(f => f.Class == FieldClass.Merge).Name;
            var b = MergeTestData.Snap(type, MergeTestData.Live(100, "d0")).With(field, new GuidValue(P0));
            var l = b.With(field, new GuidValue(P1))
                     .With(other, MergeTestData.Bump(b.Fields[other]))
                     .WithProv(MergeTestData.Live(200, "d1"));
            var r = b.With(field, new GuidValue(P2)).WithProv(MergeTestData.Live(300, "d2"));

            var m = ThreeWayMerge.Merge(MergeTestData.Ref(type, MergeTestData.G3), b, l, r);

            Assert.Equal(b.Fields[other], m.Result!.Fields[other]);   // NOT the local edit
            Assert.False(m.IsNoOp);                                   // local differs from Base
        }

        [Theory, MemberData(nameof(StructuralFields))] // S-4c — never LWW, in either argument order
        public void StructuralConflict_IsSymmetric(string type, string field)
        {
            var b = MergeTestData.Snap(type, MergeTestData.Live(100, "d0")).With(field, new GuidValue(P0));
            var l = b.With(field, new GuidValue(P1)).WithProv(MergeTestData.Live(200, "d1"));
            var r = b.With(field, new GuidValue(P2)).WithProv(MergeTestData.Live(300, "d2"));
            var reference = MergeTestData.Ref(type, MergeTestData.G3);

            var forward = ThreeWayMerge.Merge(reference, b, l, r);
            var backward = ThreeWayMerge.Merge(reference, b, r, l);

            Assert.Equal(forward.Result, backward.Result);
            Assert.Equal(ConflictKeys.ConflictKey(forward.Candidates.Single()),
                         ConflictKeys.ConflictKey(backward.Candidates.Single()));
        }

        [Fact] // null Base + divergent structural targets => D9-T1: the scope has no live row
        public void NullBaseWithDivergentStructuralTargets_LeavesNoLiveRow()
        {
            var l = MergeTestData.Snap(SyncEntityTypes.MonHoc, MergeTestData.Live(200, "d1"))
                                 .With("MaHocKy", new GuidValue(P1));
            var r = MergeTestData.Snap(SyncEntityTypes.MonHoc, MergeTestData.Live(300, "d2"))
                                 .With("MaHocKy", new GuidValue(P2));

            var m = ThreeWayMerge.Merge(MergeTestData.Ref(SyncEntityTypes.MonHoc, MergeTestData.G3), null, l, r);

            Assert.Null(m.Result);
            var c = Assert.Single(m.Candidates);
            Assert.Equal(ConflictKind.StructuralConflict, c.Kind);
            Assert.Null(c.Base);
            Assert.Null(c.BaseEntityId);
        }
    }
}
