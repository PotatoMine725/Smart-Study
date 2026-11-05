using System;

namespace SmartStudyPlanner.Sync
{
    public sealed record ChangedEntity<T>(Guid EntityId, T Entity, long Rev, bool IsDeleted);
}
