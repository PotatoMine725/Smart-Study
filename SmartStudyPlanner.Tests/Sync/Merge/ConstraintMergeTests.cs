using System;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Merge
{
    /// <summary>
    /// N-1..N-3 — D5 TaskNote MaTask scope. Detection is structural, never driven by a UNIQUE
    /// exception (D9 §19). No auto-winner; while unresolved the live row stays at Base (D9-T1).
    /// </summary>
    public class ConstraintMergeTests
    {
        private static readonly Guid Task = new("22222222-0000-0000-0000-000000000001");
        private static readonly Guid N1 = new("33333333-0000-0000-0000-000000000001");
        private static readonly Guid N2 = new("33333333-0000-0000-0000-000000000002");

        private static ConstraintScope Scope => new(SyncEntityTypes.TaskNote, "MaTask", Task.ToString("D"));

        private static EntitySnapshot Note(string content, Provenance p) =>
            MergeTestData.Snap(SyncEntityTypes.TaskNote, p,
                ("MaTask", new GuidValue(Task)), ("Content", new StringValue(content)));

        [Fact] // N-1 — two different note Ids compete for the same scope
        public void DifferentIdsInTheSameScope_IsAConstraintConflict_WithBothCandidatesPreserved()
        {
            var baseNote = Note("base", MergeTestData.Live(100, "d0"));
            var local = Note("local", MergeTestData.Live(200, "d1"));
            var remote = Note("remote", MergeTestData.Live(9_000, "d2"));   // later, still no auto-win

            var d = ConstraintMerge.Detect(new ConstraintScopeInput(
                Scope, (N1, baseNote), (N1, local), (N2, remote)));

            Assert.Equal(ConstraintOutcome.ConstraintConflict, d.Outcome);
            Assert.Equal(N1, d.LocalEntityId);
            Assert.Equal(N2, d.RemoteEntityId);

            var c = d.Candidate!;
            Assert.Equal(ConflictKind.ConstraintConflict, c.Kind);
            Assert.Equal(Scope, c.Scope);
            Assert.Null(c.AutoResolution);         // N-3: no auto winner
            Assert.Null(c.AutoResult);             // N-3: neither candidate becomes live
            Assert.Null(c.EntityId);               // scope-addressed, not entity-addressed
            Assert.Equal(baseNote, c.Base);
            Assert.Equal(N1, c.BaseEntityId);
            Assert.Equal(local, c.Local);
            Assert.Equal(N1, c.LocalEntityId);
            Assert.Equal(remote, c.Remote);
            Assert.Equal(N2, c.RemoteEntityId);
        }

        [Fact] // N-1b — null Base is allowed (D6-B): the scope simply had no baseline row
        public void ConstraintConflictWithNullBase_IsAllowed()
        {
            var d = ConstraintMerge.Detect(new ConstraintScopeInput(
                Scope, null,
                (N1, Note("local", MergeTestData.Live(200, "d1"))),
                (N2, Note("remote", MergeTestData.Live(300, "d2")))));

            Assert.Equal(ConstraintOutcome.ConstraintConflict, d.Outcome);
            Assert.Null(d.Candidate!.Base);
            Assert.Null(d.Candidate.BaseEntityId);
        }

        [Fact] // N-2 — same Id on both sides is an ordinary three-way merge, not a constraint conflict
        public void SameIdOnBothSides_IsAnOrdinaryMerge()
        {
            var d = ConstraintMerge.Detect(new ConstraintScopeInput(
                Scope, (N1, Note("base", MergeTestData.Live(100, "d0"))),
                (N1, Note("local", MergeTestData.Live(200, "d1"))),
                (N1, Note("remote", MergeTestData.Live(300, "d2")))));

            Assert.Equal(ConstraintOutcome.OrdinaryMerge, d.Outcome);
            Assert.Null(d.Candidate);
            Assert.Equal(N1, d.LocalEntityId);
            Assert.Equal(N1, d.RemoteEntityId);
        }

        [Fact] // remaining rows of DoR §7.2
        public void EmptyAndSingleSidedScopes_ResolveWithoutConflict()
        {
            Assert.Equal(ConstraintOutcome.NoAction,
                ConstraintMerge.Detect(new ConstraintScopeInput(Scope, null, null, null)).Outcome);

            Assert.Equal(ConstraintOutcome.CreateRemote,
                ConstraintMerge.Detect(new ConstraintScopeInput(
                    Scope, null, null, (N2, Note("r", MergeTestData.Live(300, "d2"))))).Outcome);

            Assert.Equal(ConstraintOutcome.LocalOnly,
                ConstraintMerge.Detect(new ConstraintScopeInput(
                    Scope, null, (N1, Note("l", MergeTestData.Live(200, "d1"))), null)).Outcome);
        }

        [Fact] // DoR §7.2 last row — fail closed, never a UNIQUE exception
        public void ScopeHeldByATombstonedNote_IsRejected_NotResurrected()
        {
            var d = ConstraintMerge.Detect(new ConstraintScopeInput(
                Scope, null,
                (N1, Note("dead", MergeTestData.Dead(200, "d1"))),
                (N2, Note("remote", MergeTestData.Live(300, "d2")))));

            Assert.Equal(ConstraintOutcome.ScopeOccupiedByTombstone, d.Outcome);
            Assert.Null(d.Candidate);
        }

        [Fact] // the ConflictKey of a constraint conflict is symmetric across peers (D5-G / D7-B)
        public void ConstraintConflictKey_IsSymmetric()
        {
            var b = (N1, Note("base", MergeTestData.Live(100, "d0")));
            var l = (N1, Note("local", MergeTestData.Live(200, "d1")));
            var r = (N2, Note("remote", MergeTestData.Live(300, "d2")));

            var forward = ConstraintMerge.Detect(new ConstraintScopeInput(Scope, b, l, r)).Candidate!;
            var backward = ConstraintMerge.Detect(new ConstraintScopeInput(Scope, b, r, l)).Candidate!;

            Assert.Equal(ConflictKeys.ConflictKey(forward), ConflictKeys.ConflictKey(backward));
            Assert.Equal(ConflictKeys.ScopeKeyOf(forward), ConflictKeys.ScopeKeyOf(backward));
        }

        [Fact] // E-4 — the same-Id path agrees with the entity merge entry point
        public void SameIdPath_AgreesWithThreeWayMerge()
        {
            var b = Note("base", MergeTestData.Live(100, "d0"));
            var l = Note("local", MergeTestData.Live(200, "d1"));
            var r = Note("remote", MergeTestData.Live(300, "d2"));

            Assert.Equal(ConstraintOutcome.OrdinaryMerge,
                ConstraintMerge.Detect(new ConstraintScopeInput(Scope, (N1, b), (N1, l), (N1, r))).Outcome);

            var m = ThreeWayMerge.Merge(MergeTestData.Ref(SyncEntityTypes.TaskNote, N1), b, l, r);
            Assert.Equal(new StringValue("remote"), m.Result!.Fields["Content"]);
        }
    }
}
