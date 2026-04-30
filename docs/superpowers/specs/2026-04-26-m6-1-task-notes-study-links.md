# M6.1 — Task Notes & Study Links
## Code Spec · 2026-04-26

> **Scope:** implement the task notes + study links feature as a DB-split aggregate, with strong UX separation and parser isolation.
>
> **Non-goals:** quick-fill / SmartParser must not auto-populate notes or links. ML / analytics do not consume this feature in M6.1.
>
> **Primary UX requirement:** task editor should feel like three clear zones:
> 1. core task fields
> 2. notes area
> 3. study links area
>
> **Fallback UX rule:** if a clean split layout does not fit the available space, the notes area may use a rich-text document-style editor, but it must still present content in visually separated lines/blocks, not as a single dense blob.

---

## 1. Design goals

### 1.1 Functional goals
- Users can attach one private note set to a task.
- Users can attach multiple study links to a task.
- Notes and links persist independently from the quick-fill parser.
- Notes and links are loaded when editing an existing task.
- Notes and links are deleted automatically when the parent task is deleted.

### 1.2 UX goals
- The task editor must clearly distinguish between:
  - task metadata
  - freeform notes
  - study links
- Users should never lose note/link content when using parser quick-fill.
- Links should be easy to add, remove, copy, and open.
- Notes should be comfortable for short comments and medium-length study reminders.

### 1.3 Technical goals
- Use a DB-split model instead of embedding all data into `StudyTask`.
- Keep blast radius contained by using an aggregate-oriented repository API.
- Keep parser and note/link editing concerns isolated from each other.
- Preserve backward compatibility for existing task records.

---

## 2. Recommended data model

### 2.1 Core entity remains `StudyTask`
`StudyTask` continues to represent the task root aggregate and retains all existing core fields.

Keep:
- `MaTask`
- `MaMonHoc`
- `TenTask`
- `HanChot`
- `TrangThai`
- `LoaiTask`
- `DiemUuTien`
- `MucDoCanhBao`
- `DoKho`
- `ThoiGianDaHoc`
- `NgayHoanThanh`

Do not overload `StudyTask` with note/link collections that would make the entity noisy for M6.1.

### 2.2 New entity: `TaskNote`
One task has one primary note record.

Suggested shape:
- `Guid Id`
- `Guid MaTask`
- `string? Content`
- `DateTime UpdatedAtUtc`
- optional `string? Format` or `string? DocumentKind` if rich text fallback is used

Suggested relation:
- `StudyTask 1 ── 1 TaskNote`

### 2.3 New entity: `TaskReferenceLink`
One task can have many links.

Suggested shape:
- `Guid Id`
- `Guid MaTask`
- `string Title`
- `string Url`
- `string? Category`
- `int SortOrder`
- `DateTime CreatedAtUtc`

Suggested relation:
- `StudyTask 1 ── * TaskReferenceLink`

### 2.4 Optional helper DTO for editor loading
To reduce blast radius in the ViewModel layer, use a small aggregate DTO when loading/saving editor state.

Suggested DTO:
- `TaskEditorBundle`
  - `StudyTask Task`
  - `TaskNote? Note`
  - `List<TaskReferenceLink> Links`

This keeps the ViewModel from manually stitching multiple repository calls together.

---

## 3. EF Core / database mapping spec

### 3.1 `AppDbContext`
Add:
- `DbSet<TaskNote> TaskNotes`
- `DbSet<TaskReferenceLink> TaskReferenceLinks`

### 3.2 Relationship mapping
In `OnModelCreating`:
- configure `TaskNote` as required/optional depending on implementation choice
- configure `TaskReferenceLink` with FK to `StudyTask`
- set cascade delete from `StudyTask` to both child tables
- add unique constraint on `TaskNote.MaTask` if enforcing 1-1 at the DB level

### 3.3 Suggested cascade behavior
- deleting a task deletes its note
- deleting a task deletes all associated links
- removing a link does not affect the parent task

### 3.4 Backward compatibility
Because the project uses `EnsureCreated()` rather than migrations in current docs, the implementation should:
- preserve existing task data
- add the new tables without touching old task rows
- avoid making note/link fields required unless the UI guarantees initialization

If a future migration system is introduced, this feature can be normalized more strictly later.

---

## 4. Repository API spec

### 4.1 Principle
Use aggregate-style methods rather than many fine-grained calls wherever practical.

### 4.2 Recommended interface additions
In `IStudyRepository` add methods such as:

- `Task<TaskEditorBundle?> GetTaskEditorBundleAsync(Guid taskId)`
- `Task SaveTaskEditorBundleAsync(TaskEditorBundle bundle)`
- `Task UpsertTaskNoteAsync(Guid taskId, string? content)`
- `Task<List<TaskReferenceLink>> GetTaskReferenceLinksAsync(Guid taskId)`
- `Task AddTaskReferenceLinkAsync(Guid taskId, TaskReferenceLink link)`
- `Task UpdateTaskReferenceLinkAsync(TaskReferenceLink link)`
- `Task DeleteTaskReferenceLinkAsync(Guid linkId)`

### 4.3 Preferred repository behavior
When editing a task:
- load core task
- load note
- load links
- return them together in one bundle if possible

When saving a task editor session:
- save/update the task core fields
- save/update the note
- reconcile links by add/update/delete diff

### 4.4 Why aggregate-oriented APIs matter here
This feature crosses three concerns:
- task core
- notes
- links

A single bundle API reduces the risk of inconsistent partial updates and keeps ViewModel code simpler.

---

## 5. ViewModel spec

### 5.1 Target ViewModel
Primary target:
- `QuanLyTaskViewModel`

### 5.2 ViewModel responsibilities
The ViewModel must own:
- current task core input fields
- current note content
- current study link collection
- add/remove link commands
- save/reset/edit state synchronization

The ViewModel must **not**:
- auto-fill notes or links from parser quick-fill
- contain DB query logic directly
- mix link management into the parser flow

### 5.3 Suggested state
Add fields/properties such as:
- `string? NoteContent`
- `ObservableCollection<TaskReferenceLinkItemVm> StudyLinks`
- `string NewLinkTitle`
- `string NewLinkUrl`
- `bool IsEditingNotes`
- `bool IsEditingLinks`

If using a richer editor model, define a dedicated child VM:
- `TaskNoteEditorViewModel`
- `TaskLinksEditorViewModel`

### 5.4 Commands
Recommended commands:
- `AddLinkCommand`
- `RemoveLinkCommand`
- `OpenLinkCommand`
- `CopyLinkCommand`
- `ClearNoteCommand`
- `ResetLinksCommand`

### 5.5 Parser isolation rule
The parser quick-fill flow may update only:
- task title
- task type
- deadline
- difficulty
- any core fields already supported by parser logic

It must not touch:
- note content
- link collection

This should be treated as a hard invariant, not a UI preference.

---

## 6. UI/UX spec

### 6.1 Layout requirement
The task editor should be divided into 3 visible zones:

1. **Task core section**
2. **Notes section**
3. **Study links section**

### 6.2 Preferred layout hierarchy
Recommended container structure:
- `Grid` or `ScrollViewer`
- `GroupBox` / `Expander` / clearly separated `Border` panels
- consistent section headers

### 6.3 Notes area
Preferred behavior:
- independent notes panel
- support plain text first
- if UX requires richer formatting, use a document-style editor
- if rich text is used, keep the visual content broken into lines/blocks, not a single dense paragraph

### 6.4 Study links area
Recommended link item fields:
- Title
- URL
- optional category/tag

Recommended affordances:
- add button
- remove button
- open button
- copy button
- URL validation hint

### 6.5 Parser visibility rule
The quick-fill parser should never place content into the note or link zones. This is important so the user clearly sees those zones as self-authored content.

### 6.6 UX fallback rule
If the screen becomes too crowded:
- collapse notes/links into separate expandable sections
- or move them into tabs
- preserve visibility and editability without requiring horizontal scrolling

---

## 7. Parser / quick-fill spec

### 7.1 Required behavior
The parser must only map the input text to core task properties.

### 7.2 Explicitly excluded fields
Do not derive or auto-fill:
- notes
- study links

### 7.3 Why this matters
Notes and links are user-authored metadata. Auto-filling them would make the feature feel unpredictable and would blur the separation between inferred metadata and user content.

### 7.4 Acceptance rule
If a user types a quick-fill sentence and then opens the task editor, the notes and links should remain untouched unless the user edits them manually.

---

## 8. Test spec

### 8.1 Repository tests
Add tests for:
- saving task with note
- saving task with multiple links
- deleting task cascades to note/links
- loading editor bundle returns full data

### 8.2 ViewModel tests
Add tests for:
- add/remove link commands
- note persistence through save/edit cycle
- parser quick-fill does not modify note/link state
- invalid link handling does not crash form

### 8.3 UI behavior tests if available
If the project has UI automation or snapshot-style coverage, verify:
- note area visible
- study links area visible
- sections remain separated after quick-fill

### 8.4 Edge cases
- empty note
- one task with zero links
- one task with many links
- duplicate URLs
- invalid URL strings
- edit existing task with pre-existing note/link data

---

## 9. Blast radius and implementation order

### 9.1 Highest-risk files
- `Models/StudyTask.cs`
- `ViewModels/QuanLyTaskViewModel.cs`
- `Views/QuanLyTaskPage.xaml`

### 9.2 Medium-risk files
- `Data/AppDbContext.cs`
- `Data/IStudyRepository.cs`
- `Data/StudyRepository.cs`

### 9.3 Lowest-risk files
- tests
- supporting DTOs
- small helper view models

### 9.4 Recommended order
1. define entities
2. map EF relations
3. extend repository APIs
4. wire ViewModel state and commands
5. build UI sections
6. lock parser isolation
7. add tests
8. polish UX

This order minimizes partial wiring and avoids building the UI on top of an incomplete data contract.

---

## 10. File map

### Create
- `Models/TaskNote.cs`
- `Models/TaskReferenceLink.cs`
- `ViewModels/TaskNoteEditorViewModel.cs` or equivalent helper VM if needed
- `Tests/TaskNotesTests.cs`

### Modify
- `Models/StudyTask.cs`
- `Data/AppDbContext.cs`
- `Data/IStudyRepository.cs`
- `Data/StudyRepository.cs`
- `ViewModels/QuanLyTaskViewModel.cs`
- `Views/QuanLyTaskPage.xaml`
- `Views/QuanLyTaskPage.xaml.cs` if UI event wiring is needed

---

## 11. Done criteria

M6.1 is complete when:
- tasks can store note content separately
- tasks can store multiple study links separately
- quick-fill parser does not populate notes/links
- UI clearly separates core task data, notes, and links
- optional rich-text fallback still keeps content visually structured
- existing task CRUD continues to work
- tests pass for the new behavior

---

## 12. Final decision summary

**Chosen architecture:**
- DB split with `TaskNote` and `TaskReferenceLink`
- `StudyTask` remains the root aggregate
- parser is strictly core-field-only
- UI must prefer separate note/link panels
- rich-text document layout is a fallback, not the default

**Design principle:**
> Notes and study links are user-owned content, not parser-inferred content.

> **Skip note:** this spec is complete and can be skipped unless a future follow-up scope is opened.