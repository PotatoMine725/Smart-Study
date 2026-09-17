using System;
using System.Security.Cryptography;
using System.Text;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Merge
{
    /// <summary>
    /// K-7 (fingerprint stability) and K-8 (deterministic, symmetric ConflictKey) — DoR §5.3.
    /// </summary>
    public class FingerprintAndConflictKeyTests
    {
        private static readonly Guid Task = new("22222222-0000-0000-0000-000000000001");
        private static readonly Guid N1 = new("33333333-0000-0000-0000-000000000001");
        private static readonly Guid N2 = new("33333333-0000-0000-0000-000000000002");

        private static EntitySnapshot Note(string content, Provenance p) =>
            MergeTestData.Snap(SyncEntityTypes.TaskNote, p,
                ("MaTask", new GuidValue(Task)), ("Content", new StringValue(content)));

        // Independent channel: hash the canonical text with a separate code path, so the
        // fingerprint cannot be "whatever the implementation happens to produce".
        private static string Sha256Hex(string s) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

        [Fact] // K-7a
        public void Fingerprint_IsLowercaseSha256OfTheCanonicalUtf8Bytes()
        {
            var snap = Note("x", new Provenance(MergeTestData.Utc0, "dev-1", false, null));
            var canonical = CanonicalJson.Write(snap);

            Assert.Equal(Sha256Hex(canonical), CanonicalJson.Fingerprint(snap));
            Assert.Equal(64, CanonicalJson.Fingerprint(snap).Length);
        }

        [Fact] // K-7b — pinned value: stable across runtimes, processes and app updates
        public void Fingerprint_IsPinnedToAKnownHex()
        {
            var snap = Note("x", new Provenance(MergeTestData.Utc0, "dev-1", false, null));
            const string expectedCanonical =
                "{\"v\":1,\"entityType\":\"TaskNote\",\"provenance\":{" +
                "\"modifiedAtUtc\":\"2026-09-08T01:02:03.1234567Z\",\"modifiedByDeviceId\":\"dev-1\"," +
                "\"isDeleted\":false,\"deletedAtUtc\":null},\"fields\":{" +
                "\"MaTask\":\"22222222-0000-0000-0000-000000000001\",\"Content\":\"x\"}}";

            Assert.Equal(expectedCanonical, CanonicalJson.Write(snap));
            Assert.Equal(Sha256Hex(expectedCanonical), CanonicalJson.Fingerprint(snap));
        }

        [Fact] // K-7c — fp(null) is the sentinel "null", which cannot collide with 64 hex chars
        public void Fingerprint_OfNull_IsTheNullSentinel()
        {
            Assert.Equal("null", CanonicalJson.Fingerprint(null));
        }

        [Fact] // K-7d — the fingerprint is insensitive to dictionary insertion order
        public void Fingerprint_IsIndependentOfFieldInsertionOrder()
        {
            var snap = MergeTestData.Snap(SyncEntityTypes.StudyTask, MergeTestData.Live(7, "d"));
            Assert.Equal(CanonicalJson.Fingerprint(snap), CanonicalJson.Fingerprint(snap.Shuffled()));
        }

        [Fact] // K-7e — different content, different fingerprint
        public void Fingerprint_DistinguishesContentAndProvenance()
        {
            var a = Note("a", MergeTestData.Live(1, "d"));
            Assert.NotEqual(CanonicalJson.Fingerprint(a), CanonicalJson.Fingerprint(Note("b", MergeTestData.Live(1, "d"))));
            Assert.NotEqual(CanonicalJson.Fingerprint(a), CanonicalJson.Fingerprint(Note("a", MergeTestData.Live(2, "d"))));
            Assert.NotEqual(CanonicalJson.Fingerprint(a), CanonicalJson.Fingerprint(Note("a", MergeTestData.Live(1, "e"))));
        }

        // ---- K-8: ConflictKey -------------------------------------------------------------

        private static ConflictCandidate Constraint(EntitySnapshot? bas, Guid? baseId,
                                                    EntitySnapshot l, Guid lId,
                                                    EntitySnapshot r, Guid rId) =>
            new(ConflictKind.ConstraintConflict, SyncEntityTypes.TaskNote, null, null,
                new ConstraintScope(SyncEntityTypes.TaskNote, "MaTask", Task.ToString("D")),
                null, bas, baseId, l, lId, r, rId, null, null);

        [Fact] // K-8a — mirrored (Local, Remote) yields the same key on both peers
        public void ConflictKey_IsSymmetricUnderMirroring()
        {
            var b = Note("base", MergeTestData.Live(100, "d0"));
            var l = Note("local", MergeTestData.Live(200, "d1"));
            var r = Note("remote", MergeTestData.Live(300, "d2"));

            Assert.Equal(
                ConflictKeys.ConflictKey(Constraint(b, N1, l, N1, r, N2)),
                ConflictKeys.ConflictKey(Constraint(b, N1, r, N2, l, N1)));
        }

        [Fact] // K-8b — a different Base yields a different key
        public void ConflictKey_DependsOnBase()
        {
            var l = Note("local", MergeTestData.Live(200, "d1"));
            var r = Note("remote", MergeTestData.Live(300, "d2"));

            var withBase = ConflictKeys.ConflictKey(Constraint(Note("b1", MergeTestData.Live(100, "d0")), N1, l, N1, r, N2));
            var otherBase = ConflictKeys.ConflictKey(Constraint(Note("b2", MergeTestData.Live(100, "d0")), N1, l, N1, r, N2));
            var nullBase = ConflictKeys.ConflictKey(Constraint(null, null, l, N1, r, N2));

            Assert.NotEqual(withBase, otherBase);
            Assert.NotEqual(withBase, nullBase);
        }

        [Fact] // K-8c — the candidate Id is part of the candidate fingerprint
        public void ConflictKey_DistinguishesCandidatesByEntityId()
        {
            var b = Note("base", MergeTestData.Live(100, "d0"));
            var l = Note("same", MergeTestData.Live(200, "d1"));
            var r = Note("same", MergeTestData.Live(200, "d1"));

            Assert.NotEqual(
                ConflictKeys.ConflictKey(Constraint(b, N1, l, N1, r, N2)),
                ConflictKeys.ConflictKey(Constraint(b, N1, l, N1, r, N1)));
        }

        [Fact] // K-8d — deterministic and free of clock/GUID/Rev randomness
        public void ConflictKey_IsDeterministicAcrossRepeatedCalls()
        {
            var c = Constraint(Note("b", MergeTestData.Live(100, "d0")), N1,
                               Note("l", MergeTestData.Live(200, "d1")), N1,
                               Note("r", MergeTestData.Live(300, "d2")), N2);

            var first = ConflictKeys.ConflictKey(c);
            for (var i = 0; i < 5; i++) Assert.Equal(first, ConflictKeys.ConflictKey(c));
            Assert.Equal(64, first.Length);
        }

        [Fact] // scope keys follow DoR §5.3 exactly
        public void ScopeKey_FollowsTheRatifiedShape()
        {
            var id = MergeTestData.G3;

            Assert.Equal($"StudyTask|{id:D}|TenTask",
                ConflictKeys.ScopeKey(ConflictKind.FieldConflict, SyncEntityTypes.StudyTask, id, "TenTask", null));

            Assert.Equal($"StudyTask|{id:D}|*",
                ConflictKeys.ScopeKey(ConflictKind.TombstoneConflict, SyncEntityTypes.StudyTask, id, null, null));

            Assert.Equal($"MonHoc|{id:D}|MaHocKy",
                ConflictKeys.ScopeKey(ConflictKind.StructuralConflict, SyncEntityTypes.MonHoc, id, "MaHocKy", null));

            Assert.Equal($"TaskNote|MaTask={Task:D}",
                ConflictKeys.ScopeKey(ConflictKind.ConstraintConflict, SyncEntityTypes.TaskNote, null, null,
                    new ConstraintScope(SyncEntityTypes.TaskNote, "MaTask", Task.ToString("D"))));
        }

        [Fact] // CandidateFp = fp(snapshot) + "@" + Id
        public void CandidateFingerprint_CombinesSnapshotAndId()
        {
            var s = Note("x", MergeTestData.Live(1, "d"));
            Assert.Equal(CanonicalJson.Fingerprint(s) + "@" + N1.ToString("D"),
                         ConflictKeys.CandidateFingerprint(s, N1));
        }

        // ---- D4/D9-T4 amendment (2026-09-10): a StructuralConflict may have NO local candidate ----

        private static ConflictCandidate Structural(EntitySnapshot? local, Guid? localId,
                                                    EntitySnapshot remote, Guid remoteId,
                                                    ConflictKind kind = ConflictKind.StructuralConflict) =>
            new(kind, SyncEntityTypes.MonHoc, remoteId, "MaHocKy",
                kind == ConflictKind.ConstraintConflict
                    ? new ConstraintScope(SyncEntityTypes.TaskNote, "MaTask", Task.ToString("D"))
                    : null,
                StructuralReason.ParentTombstoned, null, null, local, localId, remote, remoteId, null, null);

        private static EntitySnapshot MonHoc(string device) =>
            MergeTestData.Snap(SyncEntityTypes.MonHoc, MergeTestData.Live(500, device));

        [Fact]
        public void CandidateFingerprint_OfAnAbsentCandidate_IsASentinelThatCannotCollide()
        {
            Assert.Equal(ConflictKeys.AbsentCandidate, ConflictKeys.CandidateFingerprint(null, null));

            // The reason a sentinel is used instead of fp(null)@<Guid.Empty>: that shape is
            // indistinguishable from a real candidate whose Id happens to be all zeroes.
            Assert.NotEqual(CanonicalJson.Fingerprint(null) + "@" + Guid.Empty.ToString("D"),
                            ConflictKeys.CandidateFingerprint(null, null));

            // A present candidate is always 64 hex + '@' + a D-format GUID, so no present candidate
            // can ever produce the sentinel.
            Assert.NotEqual(ConflictKeys.AbsentCandidate,
                            ConflictKeys.CandidateFingerprint(MonHoc("d1"), Guid.Empty));
        }

        [Fact]
        public void ConflictKey_WithNoLocalCandidate_IsDeterministicAndDistinctFromAPresentOne()
        {
            var remote = MonHoc("d2");
            var absent = Structural(null, null, remote, N2);

            var first = ConflictKeys.ConflictKey(absent);
            for (var i = 0; i < 5; i++) Assert.Equal(first, ConflictKeys.ConflictKey(absent));
            Assert.Equal(64, first.Length);

            // "no local candidate" must not hash to the same key as "a local candidate that happens to
            // equal the remote row" — otherwise a later real conflict in the same scope would be
            // swallowed as AlreadyStaged against evidence that says something different.
            Assert.NotEqual(first, ConflictKeys.ConflictKey(Structural(remote, N1, remote, N2)));
        }

        [Fact]
        public void ConflictKey_RejectsAnAbsentLocalCandidateForAnyKindButStructural()
        {
            var ex = Assert.Throws<MergeContractViolationException>(() =>
                ConflictKeys.ConflictKey(Structural(null, null, MonHoc("d2"), N2, ConflictKind.ConstraintConflict)));

            Assert.Contains("only a StructuralConflict may have none", ex.Message);
        }

        [Fact]
        public void ConflictKey_RejectsHalfAbsentLocalEvidence()
        {
            // Half-absent is the "fabricated placeholder" shape in disguise: an id with no snapshot
            // claims a local row exists, and a snapshot with no id cannot be traced back to one.
            Assert.Throws<MergeContractViolationException>(() =>
                ConflictKeys.ConflictKey(Structural(null, N1, MonHoc("d2"), N2)));

            Assert.Throws<MergeContractViolationException>(() =>
                ConflictKeys.ConflictKey(Structural(MonHoc("d1"), null, MonHoc("d2"), N2)));
        }
    }
}
