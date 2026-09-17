namespace SmartStudyPlanner.Sync
{
    // Shared entity-type discriminator constants, used by both SyncBaseSnapshotStore and
    // SyncChangeEnumerator so the two can never drift into mismatched strings (Epic 2 / M2.1,
    // T1.4 + T2.2).
    public static class SyncEntityTypes
    {
        public const string HocKy = "HocKy";
        public const string MonHoc = "MonHoc";
        public const string StudyTask = "StudyTask";
        public const string StudyLog = "StudyLog";
        public const string TaskNote = "TaskNote";
        public const string TaskReferenceLink = "TaskReferenceLink";
    }
}
