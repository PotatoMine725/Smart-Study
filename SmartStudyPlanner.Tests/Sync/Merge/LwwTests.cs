using System;
using System.Linq;
using System.Reflection;
using SmartStudyPlanner.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Merge
{
    /// <summary>
    /// B-1..B-7 — the D9-T2 total order: ModifiedAtUtc ticks -> Ordinal ModifiedByDeviceId ->
    /// UTF-8 bytes of the canonical field value. Rev is never consulted (D9 §21).
    /// </summary>
    public class LwwTests
    {
        private static readonly FieldValue A = new StringValue("A");
        private static readonly FieldValue B = new StringValue("B");

        [Fact] // B-1
        public void GreaterTimestampWins_RegardlessOfDeviceId()
        {
            Assert.Equal(LwwSide.Local,
                Lww.Decide(MergeTestData.Live(2000, "aaa"), A, MergeTestData.Live(1000, "zzz"), B));
            Assert.Equal(LwwSide.Remote,
                Lww.Decide(MergeTestData.Live(1000, "zzz"), A, MergeTestData.Live(2000, "aaa"), B));
        }

        [Fact] // B-2
        public void EqualTimestamps_GreaterOrdinalDeviceIdWins()
        {
            Assert.Equal(LwwSide.Local,
                Lww.Decide(MergeTestData.Live(1000, "dev-b"), A, MergeTestData.Live(1000, "dev-a"), B));
            Assert.Equal(LwwSide.Remote,
                Lww.Decide(MergeTestData.Live(1000, "dev-a"), A, MergeTestData.Live(1000, "dev-b"), B));
        }

        // B-3 — ORDINAL, not culture. Ordinal puts 'a' (0x61) above 'B' (0x42); every linguistic
        // comparer orders "a" < "B" and would flip this.
        [Fact]
        public void DeviceIdComparison_IsOrdinal_NotCultureSensitive()
        {
            Assert.Equal(LwwSide.Local,
                Lww.Decide(MergeTestData.Live(1000, "a"), A, MergeTestData.Live(1000, "B"), B));
            Assert.Equal(LwwSide.Remote,
                Lww.Decide(MergeTestData.Live(1000, "B"), A, MergeTestData.Live(1000, "a"), B));
        }

        [Fact] // B-3b — Vietnamese device ids stay on code-point order ('đ' > 'd')
        public void DeviceIdComparison_HandlesVietnameseOrdinally()
        {
            Assert.Equal(LwwSide.Local,
                Lww.Decide(MergeTestData.Live(1000, "đev"), A, MergeTestData.Live(1000, "dev"), B));
        }

        [Fact] // B-4a — Rev cannot influence the order because it is not on the type at all.
        public void ProvenanceAndSnapshot_ExposeNoRevMember()
        {
            foreach (var t in new[] { typeof(Provenance), typeof(EntitySnapshot) })
            {
                var names = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Select(p => p.Name).ToList();
                Assert.DoesNotContain(names, n => n.Contains("Rev", StringComparison.OrdinalIgnoreCase));
            }

            var mergeParams = typeof(ThreeWayMerge).GetMethod(nameof(ThreeWayMerge.Merge))!.GetParameters();
            Assert.DoesNotContain(mergeParams, p => p.ParameterType == typeof(long));
        }

        [Fact] // B-4b — Rev-like bookkeeping can never reach the decision.
        public void RevLikeBookkeeping_CannotChangeTheDecision()
        {
            foreach (var spec in MergeSurfaceRegistry.All)
            {
                Assert.DoesNotContain(spec.SnapshotFields, f => f.Name == "Rev");
                Assert.Equal(FieldClass.SyncMetadata, spec.Fields.Single(f => f.Name == "Rev").Class);
            }

            // Two sides differing ONLY in a hypothetical Rev are provenance-identical here, so the
            // decision falls to the canonical value and mirrors cleanly.
            var pl = MergeTestData.Live(1000, "same");
            var pr = MergeTestData.Live(1000, "same");
            Assert.Equal(LwwSide.Remote, Lww.Decide(pl, A, pr, B));
            Assert.Equal(LwwSide.Local, Lww.Decide(pl, B, pr, A));
        }

        // B-7 (D9-T2) — full provenance tie decided by UTF-8 bytes of the canonical value.
        // UTF-16 ordinal orders U+FFFD (0xFFFD) above U+1F600 (lead unit 0xD83D) and would flip
        // this; UTF-8 orders F0.9F.98.80 above EF.BF.BD.
        [Fact]
        public void FullProvenanceTie_IsDecidedByUtf8BytesOfCanonicalValue()
        {
            var p = MergeTestData.Live(4242, "same-device");
            FieldValue emoji = new StringValue("\U0001F600");
            FieldValue replacement = new StringValue("�");

            Assert.Equal(LwwSide.Local, Lww.Decide(p, emoji, p, replacement));
            Assert.Equal(LwwSide.Remote, Lww.Decide(p, replacement, p, emoji));
        }

        // B-7b — DoR §6.1: null needs no special case. Its canonical bytes are "null", and
        // 'n' (0x6E) is above the opening quote (0x22), so null sorts ABOVE any string literal.
        [Fact]
        public void FullProvenanceTie_OrdersNullAndScalarsByCanonicalBytes()
        {
            var p = MergeTestData.Live(1, "d");
            Assert.Equal(LwwSide.Remote, Lww.Decide(p, new StringValue("a"), p, new StringValue(null)));
            Assert.Equal(LwwSide.Local, Lww.Decide(p, new StringValue(null), p, new StringValue("a")));
            Assert.Equal(LwwSide.Local, Lww.Decide(p, new Int32Value(9), p, new Int32Value(8)));
            Assert.Equal(LwwSide.Local, Lww.Decide(p, new BoolValue(true), p, new BoolValue(false)));
        }

        [Fact] // an all-three-component tie means the values were equal: case 4, not case 5.
        public void CompleteTie_IsAContractViolation_NotASilentWinner()
        {
            var p = MergeTestData.Live(1, "d");
            Assert.Throws<MergeContractViolationException>(() => Lww.Decide(p, A, p, A));
        }

        [Fact] // B-5/B-6 — argument-order independence over 200 generated triples
        public void Decide_IsArgumentOrderIndependent_Over200Triples()
        {
            var rnd = new Random(20260908);
            var devices = new[] { "a", "B", "dev-1", "đev", "Z", " x", "desktop-1a2b3c4d" };

            for (var i = 0; i < 200; i++)
            {
                var pl = MergeTestData.Live(rnd.Next(1, 5), devices[rnd.Next(devices.Length)]);
                var pr = MergeTestData.Live(rnd.Next(1, 5), devices[rnd.Next(devices.Length)]);
                FieldValue vl = new Int32Value(rnd.Next(0, 4));
                FieldValue vr = new Int32Value(rnd.Next(0, 4));

                if (pl.ModifiedAtUtc == pr.ModifiedAtUtc
                    && StringComparer.Ordinal.Equals(pl.ModifiedByDeviceId, pr.ModifiedByDeviceId)
                    && CanonicalJson.WriteFieldValue(vl) == CanonicalJson.WriteFieldValue(vr))
                {
                    continue; // case 4 (common value) never reaches LWW
                }

                var forward = Lww.Decide(pl, vl, pr, vr);
                var backward = Lww.Decide(pr, vr, pl, vl);

                // The same physical side must win whichever way the arguments are passed.
                var forwardValue = forward == LwwSide.Local ? vl : vr;
                var backwardValue = backward == LwwSide.Local ? vr : vl;
                Assert.Equal(CanonicalJson.WriteFieldValue(forwardValue),
                             CanonicalJson.WriteFieldValue(backwardValue));
                Assert.NotEqual(forward, backward);
            }
        }

        [Fact] // provenance order is ticks-then-ordinal; Kind never participates
        public void CompareProvenance_UsesTicksThenOrdinalDeviceId()
        {
            var earlier = MergeTestData.Live(1000, "z");
            var later = MergeTestData.Live(2000, "a");
            Assert.True(Lww.CompareProvenance(later, earlier) > 0);
            Assert.True(Lww.CompareProvenance(earlier, later) < 0);
            Assert.Equal(0, Lww.CompareProvenance(earlier, MergeTestData.Live(1000, "z")));
            Assert.True(Lww.CompareProvenance(MergeTestData.Live(5, "a"), MergeTestData.Live(5, "B")) > 0);
        }
    }
}
