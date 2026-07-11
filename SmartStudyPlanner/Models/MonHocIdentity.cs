using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartStudyPlanner.Models
{
    // Epic 1 / M1.3: semantic identity for MonHoc. Alpha stopgap for the semantic-duplicate
    // class ("Toán" / "toán " / "Toán  " are the same subject) -- true cross-device
    // identity-merge is Epic 2. Bounded to MonHoc; every dedup/prevent-at-source site must
    // route through Normalize so no two places can drift on what "the same subject" means.
    public static class MonHocIdentity
    {
        // NFC-normalize -> trim -> collapse internal whitespace runs to a single space ->
        // invariant-culture lowercase. Diacritics are PRESERVED: "Toán" != "Toan" -- accent
        // makes it a different subject, only case/whitespace/composition variance folds away.
        public static string Normalize(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            var nfc = name.Normalize(NormalizationForm.FormC).Trim();
            var collapsed = Regex.Replace(nfc, @"\s+", " ");
            return collapsed.ToLowerInvariant();
        }

        public sealed class NameComparer : IEqualityComparer<string?>
        {
            public static readonly NameComparer Instance = new();

            public bool Equals(string? x, string? y) => Normalize(x) == Normalize(y);
            public int GetHashCode(string? obj) => Normalize(obj).GetHashCode();
        }
    }
}
