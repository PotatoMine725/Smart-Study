using System.Collections.Generic;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Sync
{
    public sealed record PeerChangeSet(
        List<ChangedEntity<HocKy>> HocKys,
        List<ChangedEntity<MonHoc>> MonHocs,
        List<ChangedEntity<StudyTask>> StudyTasks,
        List<ChangedEntity<StudyLog>> StudyLogs,
        List<ChangedEntity<TaskNote>> TaskNotes,
        List<ChangedEntity<TaskReferenceLink>> TaskReferenceLinks);
}
