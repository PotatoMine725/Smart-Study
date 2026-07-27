using SmartStudyPlanner.Models;
using Xunit;

namespace SmartStudyPlanner.Tests.Models
{
    public class MonHocIdentityTests
    {
        [Theory]
        [InlineData("Toán", "Toán")]
        [InlineData("Toán", "toán")]
        [InlineData("Toán", " Toán ")]
        [InlineData("Toán", "Toán  ")]
        [InlineData("Toán", "  toán")]
        [InlineData("Vật Lý", "vật    lý")]
        [InlineData("Vật Lý", "Vật\tLý")]
        public void Normalize_EquivalentNames_ProduceSameKey(string a, string b)
        {
            Assert.Equal(MonHocIdentity.Normalize(a), MonHocIdentity.Normalize(b));
        }

        [Fact]
        public void Normalize_NfcPrecomposedAndDecomposed_ProduceSameKey()
        {
            // "Toán" via precomposed 'á' (U+00E1) vs decomposed 'a' + combining acute (U+0301).
            var precomposed = "Toán";
            var decomposed = "Toán";

            Assert.NotEqual(precomposed, decomposed); // distinct as raw strings pre-normalization
            Assert.Equal(MonHocIdentity.Normalize(precomposed), MonHocIdentity.Normalize(decomposed));
        }

        [Theory]
        [InlineData("Toán", "Toan")]
        [InlineData("Toán", "Táon")]
        [InlineData("Hóa", "Hoa")]
        public void Normalize_DiacriticDifference_ProducesDifferentKey(string a, string b)
        {
            Assert.NotEqual(MonHocIdentity.Normalize(a), MonHocIdentity.Normalize(b));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Normalize_NullOrWhitespace_ReturnsEmpty(string? name)
        {
            Assert.Equal(string.Empty, MonHocIdentity.Normalize(name));
        }

        [Fact]
        public void NameComparer_TreatsNormalizeEquivalentNamesAsEqual()
        {
            var comparer = MonHocIdentity.NameComparer.Instance;

            Assert.True(comparer.Equals("Toán", " toán "));
            Assert.False(comparer.Equals("Toán", "Toan"));
            Assert.Equal(comparer.GetHashCode("Toán"), comparer.GetHashCode(" toán "));
        }
    }
}
