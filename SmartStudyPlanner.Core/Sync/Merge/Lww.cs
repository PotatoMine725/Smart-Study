using System;
using System.Text;

namespace SmartStudyPlanner.Sync.Merge
{
    public enum LwwSide { Local, Remote }

    /// <summary>
    /// The D9-T2 total order (DoR §6):
    ///   1. <c>ModifiedAtUtc</c> ticks
    ///   2. <c>ModifiedByDeviceId</c> under <see cref="StringComparer.Ordinal"/>
    ///   3. UTF-8 bytes of the canonical field value
    /// Greater wins. <c>Rev</c> is never consulted, and the argument order never matters.
    /// </summary>
    public static class Lww
    {
        public static LwwSide Decide(Provenance local, FieldValue localValue,
                                     Provenance remote, FieldValue remoteValue)
        {
            var byProvenance = CompareProvenance(local, remote);
            if (byProvenance != 0) return byProvenance > 0 ? LwwSide.Local : LwwSide.Remote;

            // Component 3 compares BYTES, not UTF-16 code units: string.CompareOrdinal orders
            // supplementary characters by their surrogate lead unit, which differs from code-point
            // order and would make two peers disagree.
            var left = Encoding.UTF8.GetBytes(CanonicalJson.WriteFieldValue(localValue));
            var right = Encoding.UTF8.GetBytes(CanonicalJson.WriteFieldValue(remoteValue));
            var byValue = ((ReadOnlySpan<byte>)left).SequenceCompareTo(right);

            if (byValue == 0)
            {
                // All three components equal means the canonical values are equal, i.e. the caller
                // is in D9 case 4 (common value) and must not have reached LWW at all.
                throw new MergeContractViolationException(
                    "LWW reached with identical provenance and identical canonical value; that is case 4, not a conflict.");
            }

            return byValue > 0 ? LwwSide.Local : LwwSide.Remote;
        }

        /// <summary>
        /// Row-level provenance order (ticks, then Ordinal device id). Kind never participates:
        /// the value has already been re-kinded at the boundary (DoR §5.4).
        /// </summary>
        public static int CompareProvenance(Provenance a, Provenance b)
        {
            var byTicks = a.ModifiedAtUtc.Ticks.CompareTo(b.ModifiedAtUtc.Ticks);
            return byTicks != 0
                ? byTicks
                : StringComparer.Ordinal.Compare(a.ModifiedByDeviceId, b.ModifiedByDeviceId);
        }
    }
}
