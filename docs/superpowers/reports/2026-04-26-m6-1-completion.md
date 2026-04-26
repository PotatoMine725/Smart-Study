# M6.1 Completion Report — Task Notes & Study Links
**Date:** 2026-04-26  
**Branch:** `feat/m6-1-task-notes`  
**Tests:** 141/141 pass (13 new tests added)

---

## What Was Built

### Data Layer
- **`TaskNote`** — 1-1 per task, stores freeform note content + `UpdatedAtUtc`
- **`TaskReferenceLink`** — 1-N per task, stores `Title`, `Url`, `Category`, `SortOrder`
- **`TaskEditorBundle`** — aggregate DTO (task + note + links) for atomic load/save
- **`AppDbContext`** — added `TaskNotes` / `TaskReferenceLinks` DbSets; Fluent API cascade delete from `StudyTask` to both child tables; added `DbContextOptions` constructor for testability
- **`IStudyRepository`** + **`StudyRepository`** — 7 new methods: `GetTaskEditorBundleAsync`, `UpsertTaskNoteAsync`, `GetTaskReferenceLinksAsync`, `AddTaskReferenceLinkAsync`, `UpdateTaskReferenceLinkAsync`, `DeleteTaskReferenceLinkAsync`, `SaveTaskEditorBundleAsync`

### ViewModel Layer
- **`TaskReferenceLinkItemVm`** — observable item VM with `ToModel()` / `FromModel()` round-trip
- **`QuanLyTaskViewModel`** — added `NoteContent`, `NewLinkTitle`, `NewLinkUrl`, `StudyLinks` collection; 5 new commands: `AddLink`, `RemoveLink`, `OpenLink`, `CopyLink`, `ClearNote`; `SuaTask` made async to load bundle on edit; `ThemTask` saves notes + links after core save
- **Parser isolation preserved** — `PhanTichNhapNhanh` still only fills core task fields; `NoteContent` and `StudyLinks` are never touched by parser

### UI Layer
- **`QuanLyTaskPage.xaml`** — wrapped in `ScrollViewer`; added 2 new `GroupBox` zones:
  - **Zone 2 — Ghi Chú**: multiline `TextBox` + "Xóa ghi chú" button
  - **Zone 3 — Liên Kết Học Tập**: URL input row (title + URL + Add button) + `ItemsControl` with per-item Open / Copy / Remove buttons

### Tests
- **`TaskNotesTests.cs`** — 13 new tests covering:
  - Upsert note then load → persists
  - Update existing note → content changes
  - Add 3 links → returns all, ordered by SortOrder
  - Delete task → cascade removes note + links
  - SaveBundle diff → removed link deleted from DB
  - Null note content → no exception
  - Zero links → empty list
  - VM `AddLink` with empty URL → no-op
  - VM `AddLink` valid URL → item added, fields cleared
  - VM `RemoveLink` → item removed from collection
  - VM `ClearNote` → `NoteContent` becomes null
  - VM parser isolation → note/links unchanged after quick-fill

---

## Architecture Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| DB layout | Split tables (`TaskNote`, `TaskReferenceLink`) | Keeps `StudyTask` clean; blast radius contained |
| Cascade delete | Fluent API FK, no nav props on `StudyTask` | Avoids polluting the root aggregate |
| Note/link save path | New per-task methods, never via `LuuHocKyAsync` | `LuuHocKyAsync` does delete-reinsert; routing notes through it would wipe them |
| EnsureCreated compat | Accept DB recreation for dev | No migration system yet; new tables only appear on fresh DB |
| Parser isolation | Hard invariant — `PhanTichNhapNhanh` untouched | Notes/links are user-authored content, not parser-inferred |

---

## Files Changed

| Action | File |
|--------|------|
| Created | `SmartStudyPlanner/Models/TaskNote.cs` |
| Created | `SmartStudyPlanner/Models/TaskReferenceLink.cs` |
| Created | `SmartStudyPlanner/Models/TaskEditorBundle.cs` |
| Created | `SmartStudyPlanner/ViewModels/TaskReferenceLinkItemVm.cs` |
| Modified | `SmartStudyPlanner/Data/AppDbContext.cs` |
| Modified | `SmartStudyPlanner/Data/IStudyRepository.cs` |
| Modified | `SmartStudyPlanner/Data/StudyRepository.cs` |
| Modified | `SmartStudyPlanner/ViewModels/QuanLyTaskViewModel.cs` |
| Modified | `SmartStudyPlanner/Views/QuanLyTaskPage.xaml` |
| Modified | `SmartStudyPlanner.Tests/Helpers/FakeStudyRepository.cs` |
| Created | `SmartStudyPlanner.Tests/TaskNotesTests.cs` |

---

## Done Criteria Checklist

- [x] Tasks can store note content separately (`TaskNote` table)
- [x] Tasks can store multiple study links separately (`TaskReferenceLink` table)
- [x] Quick-fill parser does not populate notes/links
- [x] UI clearly separates core task data (zone 1), notes (zone 2), links (zone 3)
- [x] Existing task CRUD continues to work
- [x] Cascade delete: deleting a task removes its note and all links
- [x] 141/141 tests pass

---

## Known Limitations / Future Work

- `EnsureCreated()` — dev must delete `SmartStudyData.db` to pick up new tables (no migration system yet)
- Notes/links for newly-created tasks (not yet persisted) are saved on first `ThemTask` call; the `_editingTaskId` pattern handles this
- No URL validation feedback in the UI (deferred — not in M6.1 scope)
- `Category` field on `TaskReferenceLink` is available in the model but not yet exposed in the UI
