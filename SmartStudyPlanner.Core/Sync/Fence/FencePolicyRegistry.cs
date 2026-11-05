using System;
using System.Collections.Generic;
using SmartStudyPlanner.Sync.Fence.Policies;

namespace SmartStudyPlanner.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 1 (plan §10). Exactly one policy per non-<see cref="ConflictShape.Unsupported"/>
    /// shape. The static constructor asserts completeness so a new shape without a registered policy
    /// fails loudly at load time rather than silently falling back (INV-5). <see cref="ConflictShape.Unsupported"/>
    /// never reaches a policy -- callers emit <see cref="FenceOutcome.Unsupported"/> themselves.
    /// </summary>
    internal static class FencePolicyRegistry
    {
        private static readonly IReadOnlyDictionary<ConflictShape, IConflictFencePolicy> Policies;

        static FencePolicyRegistry()
        {
            var policies = new IConflictFencePolicy[]
            {
                new ConcurrentReparentFencePolicy(),
                new ParentTombstoneFencePolicy(),
                new AbsentLocalParentTombstonePolicy(),
                new ConstraintOccupancyFencePolicy(),
            };

            var map = new Dictionary<ConflictShape, IConflictFencePolicy>();
            foreach (var policy in policies)
            {
                if (policy.Shape == ConflictShape.Unsupported)
                {
                    throw new InvalidOperationException(
                        $"{policy.GetType().Name} must not register for ConflictShape.Unsupported.");
                }

                if (!map.TryAdd(policy.Shape, policy))
                {
                    throw new InvalidOperationException(
                        $"Duplicate IConflictFencePolicy registered for shape {policy.Shape}.");
                }
            }

            foreach (var shape in Enum.GetValues<ConflictShape>())
            {
                if (shape == ConflictShape.Unsupported) continue;
                if (!map.ContainsKey(shape))
                {
                    throw new InvalidOperationException(
                        $"No IConflictFencePolicy registered for ConflictShape.{shape}.");
                }
            }

            Policies = map;
        }

        /// <summary>Null for <see cref="ConflictShape.Unsupported"/> or any shape not in the registry.</summary>
        public static IConflictFencePolicy? TryGet(ConflictShape shape) =>
            Policies.TryGetValue(shape, out var policy) ? policy : null;
    }
}
