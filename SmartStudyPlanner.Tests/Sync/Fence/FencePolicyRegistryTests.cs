using System;
using SmartStudyPlanner.Sync.Fence;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 1 (plan §10). Exactly one policy per supported shape; no fallback for
    /// <see cref="ConflictShape.Unsupported"/>. The registry's static constructor asserts completeness
    /// at type-init time (N-5): removing a policy from the registry's backing array is the mutant for
    /// this file, and it turns every test that touches <c>FencePolicyRegistry</c> red via
    /// <c>TypeInitializationException</c>.
    /// </summary>
    public class FencePolicyRegistryTests
    {
        [Theory]
        [InlineData(ConflictShape.ConcurrentReparent)]
        [InlineData(ConflictShape.ParentTombstoned)]
        [InlineData(ConflictShape.AbsentLocalParentTombstoned)]
        [InlineData(ConflictShape.ConstraintOccupancy)]
        public void TryGet_ReturnsExactlyOnePolicy_WhoseShapeMatches(ConflictShape shape)
        {
            var policy = FencePolicyRegistry.TryGet(shape);

            Assert.NotNull(policy);
            Assert.Equal(shape, policy!.Shape);
        }

        [Fact]
        public void TryGet_Unsupported_ReturnsNull_NoFallbackPolicy()
        {
            Assert.Null(FencePolicyRegistry.TryGet(ConflictShape.Unsupported));
        }

        [Fact]
        public void EveryConflictShapeEnumValue_IsEitherUnsupportedOrRegistered()
        {
            foreach (var shape in Enum.GetValues<ConflictShape>())
            {
                if (shape == ConflictShape.Unsupported)
                {
                    Assert.Null(FencePolicyRegistry.TryGet(shape));
                }
                else
                {
                    Assert.NotNull(FencePolicyRegistry.TryGet(shape));
                }
            }
        }
    }
}
