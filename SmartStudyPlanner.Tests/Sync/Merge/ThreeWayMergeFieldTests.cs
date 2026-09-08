using System;
using System.Linq;
using System.Reflection;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Merge
{
    /// <summary>
    /// A-1..A-6 (D9 §12.1 five-case table + per-field independence) and C-1..C-3 (result
    /// provenance, DoR §6.5).
    /// </summary>
    public class ThreeWayMergeFieldTests
    {
        private const string Type = SyncEntityTypes.StudyTask;
        private static readonly Guid Id = MergeTestData.G3;
        private static EntityRef Ref => MergeTestData.Ref(Type, Id);

        private static FieldOutcome Outcome(MergedEntity m, string field) =>
            m.Outcomes.Single(o => o.Field == field);

        // ---- A-1..A-5: the five cases on an ordinary Merge field ----------------------------

        [Fact] // A-1 — neither side changed => Base
        public void Case1_NeitherChanged_TakesBase()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var l = b.WithProv(MergeTestData.Live(200, "d1"));
            var r = b.WithProv(MergeTestData.Live(300, "d2"));

            var m = ThreeWayMerge.Merge(Ref, b, l, r);

            Assert.Equal(FieldDecision.Base, Outcome(m, "TenTask").Decision);
            Assert.Equal(b.Fields["TenTask"], m.Result!.Fields["TenTask"]);
            Assert.Empty(m.Candidates);
        }

        [Fact] // A-2 — only Local changed => Local
        public void Case2_OnlyLocalChanged_TakesLocal()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var l = b.With("TenTask", new StringValue("local")).WithProv(MergeTestData.Live(200, "d1"));
            var r = b.WithProv(MergeTestData.Live(300, "d2"));

            var m = ThreeWayMerge.Merge(Ref, b, l, r);

            Assert.Equal(FieldDecision.Local, Outcome(m, "TenTask").Decision);
            Assert.Equal(new StringValue("local"), m.Result!.Fields["TenTask"]);
            Assert.Empty(m.Candidates);
        }

        [Fact] // A-3 — only Remote changed => Remote
        public void Case3_OnlyRemoteChanged_TakesRemote()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var l = b.WithProv(MergeTestData.Live(300, "d1"));
            var r = b.With("TenTask", new StringValue("remote")).WithProv(MergeTestData.Live(200, "d2"));

            var m = ThreeWayMerge.Merge(Ref, b, l, r);

            Assert.Equal(FieldDecision.Remote, Outcome(m, "TenTask").Decision);
            Assert.Equal(new StringValue("remote"), m.Result!.Fields["TenTask"]);
            Assert.Empty(m.Candidates);
        }

        [Fact] // A-4 — both changed to the SAME value => common, not a conflict
        public void Case4_BothChangedToSameValue_IsCommonAndEmitsNoEvidence()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var v = new StringValue("agreed");
            var l = b.With("TenTask", v).WithProv(MergeTestData.Live(200, "d1"));
            var r = b.With("TenTask", v).WithProv(MergeTestData.Live(300, "d2"));

            var m = ThreeWayMerge.Merge(Ref, b, l, r);

            Assert.Equal(FieldDecision.Common, Outcome(m, "TenTask").Decision);
            Assert.Equal(v, m.Result!.Fields["TenTask"]);
            Assert.Empty(m.Candidates);
        }

        [Fact] // A-5 — both changed differently => LWW
        public void Case5_BothChangedDifferently_UsesLww()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var l = b.With("TenTask", new StringValue("L")).WithProv(MergeTestData.Live(200, "d1"));
            var r = b.With("TenTask", new StringValue("R")).WithProv(MergeTestData.Live(300, "d2"));

            var m = ThreeWayMerge.Merge(Ref, b, l, r);

            Assert.Equal(FieldDecision.LwwRemote, Outcome(m, "TenTask").Decision);
            Assert.Equal(new StringValue("R"), m.Result!.Fields["TenTask"]);

            // mirror: the earlier remote loses
            var m2 = ThreeWayMerge.Merge(Ref, b,
                b.With("TenTask", new StringValue("L")).WithProv(MergeTestData.Live(400, "d1")),
                b.With("TenTask", new StringValue("R")).WithProv(MergeTestData.Live(300, "d2")));
            Assert.Equal(FieldDecision.LwwLocal, Outcome(m2, "TenTask").Decision);
            Assert.Equal(new StringValue("L"), m2.Result!.Fields["TenTask"]);
        }

        // ---- A-6: per-field independence (D9 §13) -------------------------------------------

        [Fact] // A-6 — one row resolving Local / Remote / LWW / unchanged simultaneously
        public void PerFieldIndependence_ResolvesEachFieldOnItsOwn()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));

            var l = b.With("TenTask", new StringValue("L-only"))       // -> Local
                     .With("DoKho", new Int32Value(1))                  // unchanged on remote side below
                     .With("TrangThai", new StringValue("L"))           // -> LWW
                     .WithProv(MergeTestData.Live(200, "d1"));
            l = l.With("DoKho", b.Fields["DoKho"]);                     // keep DoKho == Base on local

            var r = b.With("ThoiGianDaHoc", new Int32Value(42))         // -> Remote
                     .With("TrangThai", new StringValue("R"))           // -> LWW (remote is later)
                     .WithProv(MergeTestData.Live(300, "d2"));

            var m = ThreeWayMerge.Merge(Ref, b, l, r);

            Assert.Equal(FieldDecision.Local, Outcome(m, "TenTask").Decision);
            Assert.Equal(FieldDecision.Remote, Outcome(m, "ThoiGianDaHoc").Decision);
            Assert.Equal(FieldDecision.LwwRemote, Outcome(m, "TrangThai").Decision);
            Assert.Equal(FieldDecision.Base, Outcome(m, "DoKho").Decision);

            Assert.Equal(new StringValue("L-only"), m.Result!.Fields["TenTask"]);
            Assert.Equal(new Int32Value(42), m.Result.Fields["ThoiGianDaHoc"]);
            Assert.Equal(new StringValue("R"), m.Result.Fields["TrangThai"]);
            Assert.Equal(b.Fields["DoKho"], m.Result.Fields["DoKho"]);
        }

        [Theory] // A-6b — cases 2 and 3 hold on every entity spec
        [MemberData(nameof(MergeTestData.AllEntityTypes), MemberType = typeof(MergeTestData))]
        public void Cases2And3_HoldForEveryEntitySpec(string entityType)
        {
            var field = MergeTestData.FirstMergeField(entityType);
            var id = MergeTestData.G3;
            var b = MergeTestData.Snap(entityType, MergeTestData.Live(100, "d0"));
            var changed = MergeTestData.Bump(b.Fields[field]);

            var localChanged = ThreeWayMerge.Merge(MergeTestData.Ref(entityType, id), b,
                b.With(field, changed).WithProv(MergeTestData.Live(200, "d1")),
                b.WithProv(MergeTestData.Live(300, "d2")));
            Assert.Equal(FieldDecision.Local, localChanged.Outcomes.Single(o => o.Field == field).Decision);
            Assert.Equal(changed, localChanged.Result!.Fields[field]);

            var remoteChanged = ThreeWayMerge.Merge(MergeTestData.Ref(entityType, id), b,
                b.WithProv(MergeTestData.Live(300, "d1")),
                b.With(field, changed).WithProv(MergeTestData.Live(200, "d2")));
            Assert.Equal(FieldDecision.Remote, remoteChanged.Outcomes.Single(o => o.Field == field).Decision);
            Assert.Equal(changed, remoteChanged.Result!.Fields[field]);
        }

        // ---- Null Base (D9-T1 reading; see report) ------------------------------------------

        [Fact]
        public void NullBase_TreatsEveryFieldAsBothSidesChanged()
        {
            var l = MergeTestData.Snap(Type, MergeTestData.Live(200, "d1"))
                                 .With("TenTask", new StringValue("L"));
            var r = MergeTestData.Snap(Type, MergeTestData.Live(300, "d2"))
                                 .With("TenTask", new StringValue("R"));

            var m = ThreeWayMerge.Merge(Ref, null, l, r);

            Assert.Equal(FieldDecision.LwwRemote, Outcome(m, "TenTask").Decision);
            // fields that agree collapse to Common
            Assert.Equal(FieldDecision.Common, Outcome(m, "DoKho").Decision);
            Assert.NotNull(m.Result);
        }

        // ---- C-1..C-3: result provenance (DoR §6.5) -----------------------------------------

        [Fact] // C-1 — only one side changed => that side's provenance
        public void ResultProvenance_IsTheChangingSides()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var lp = MergeTestData.Live(200, "d1");
            var rp = MergeTestData.Live(900, "d2");

            var localOnly = ThreeWayMerge.Merge(Ref, b,
                b.With("TenTask", new StringValue("L")).WithProv(lp), b.WithProv(rp));
            Assert.Equal(lp, localOnly.ResultProvenance);

            var remoteOnly = ThreeWayMerge.Merge(Ref, b,
                b.WithProv(rp), b.With("TenTask", new StringValue("R")).WithProv(lp));
            Assert.Equal(lp, remoteOnly.ResultProvenance);
        }

        [Fact] // C-2 — mixed row => the greater (Ticks, DeviceId) side, symmetric
        public void ResultProvenance_OnMixedRow_IsTheGreaterSide()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var lp = MergeTestData.Live(200, "d1");
            var rp = MergeTestData.Live(300, "d2");

            var m = ThreeWayMerge.Merge(Ref, b,
                b.With("TenTask", new StringValue("L")).WithProv(lp),
                b.With("ThoiGianDaHoc", new Int32Value(9)).WithProv(rp));

            Assert.Equal(rp, m.ResultProvenance);

            var mirrored = ThreeWayMerge.Merge(Ref, b,
                b.With("ThoiGianDaHoc", new Int32Value(9)).WithProv(rp),
                b.With("TenTask", new StringValue("L")).WithProv(lp));
            Assert.Equal(rp, mirrored.ResultProvenance);
        }

        [Fact] // C-2b — nothing changed anywhere => Base provenance, IsNoOp
        public void NoChangeOnEitherSide_KeepsBaseProvenanceAndIsANoOp()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var m = ThreeWayMerge.Merge(Ref, b,
                b.WithProv(MergeTestData.Live(200, "d1")), b.WithProv(MergeTestData.Live(300, "d2")));

            Assert.Equal(b.Provenance, m.ResultProvenance);
            Assert.True(m.IsNoOp);   // DoR §6.5 row 1: row untouched
        }

        [Fact] // IsNoOp — result identical to Local (fields + provenance) must not be written
        public void IsNoOp_IsTrueOnlyWhenResultEqualsLocalIncludingProvenance()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));

            // local already carries the merged outcome
            var localOnly = ThreeWayMerge.Merge(Ref, b,
                b.With("TenTask", new StringValue("L")).WithProv(MergeTestData.Live(200, "d1")),
                b.WithProv(MergeTestData.Live(150, "d2")));
            Assert.True(localOnly.IsNoOp);

            var remoteWins = ThreeWayMerge.Merge(Ref, b,
                b.WithProv(MergeTestData.Live(150, "d1")),
                b.With("TenTask", new StringValue("R")).WithProv(MergeTestData.Live(200, "d2")));
            Assert.False(remoteWins.IsNoOp);
        }

        [Fact] // C-3 — the provenance type carries no Rev (reflection)
        public void ProvenanceType_HasExactlyTheFourRatifiedMembers()
        {
            var names = typeof(Provenance).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                          .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
            Assert.Equal(new[] { "DeletedAtUtc", "IsDeleted", "ModifiedAtUtc", "ModifiedByDeviceId" }, names);
        }

        // ---- contract violations are never conflicts (DoR §4.1) -----------------------------

        [Fact]
        public void DifferingCopyOnCreateValue_IsAContractViolation()
        {
            var b = MergeTestData.Snap(SyncEntityTypes.StudyLog, MergeTestData.Live(100, "d0"));
            var l = b.With("CreatedAtUtc", new UtcValue(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)))
                     .WithProv(MergeTestData.Live(200, "d1"));
            var r = b.WithProv(MergeTestData.Live(300, "d2"));

            Assert.Throws<MergeContractViolationException>(
                () => ThreeWayMerge.Merge(MergeTestData.Ref(SyncEntityTypes.StudyLog, Id), b, l, r));
        }

        [Fact]
        public void MismatchedFieldSetOrEntityType_IsAContractViolation()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var otherType = MergeTestData.Snap(SyncEntityTypes.MonHoc, MergeTestData.Live(100, "d0"));

            Assert.Throws<MergeContractViolationException>(
                () => ThreeWayMerge.Merge(Ref, b, otherType, b));

            var missingField = new EntitySnapshot(Type, b.Provenance,
                b.Fields.Where(kv => kv.Key != "DoKho").ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal));
            Assert.Throws<MergeContractViolationException>(
                () => ThreeWayMerge.Merge(Ref, b, missingField, b));
        }

        [Fact]
        public void NonUtcProvenance_IsAContractViolation()
        {
            var b = MergeTestData.Snap(Type, MergeTestData.Live(100, "d0"));
            var bad = b.WithProv(new Provenance(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local), "d1", false, null));

            Assert.Throws<MergeContractViolationException>(() => ThreeWayMerge.Merge(Ref, b, bad, b));
        }
    }
}
