# ML Retrain Post-Reset — Pipeline Verification
## Spec · 2026-04-29

> **Context:** DB was reset to fix the semester end-date bug (NgayKetThuc editable, 150-day default).
> ML artifacts (`study_time.zip`, `meta.json`) were not fully cleaned — stale meta shows
> `SeedOnly=false, LogsUsedCount=180` from a pre-reset retrain. DB is now nearly empty.
>
> **Goal:** clean up stale artifacts, seed 180 synthetic StudyLogs via a reusable dev test,
> trigger a fresh retrain through the existing "Tối ưu AI" UI, and confirm the ML pipeline
> is healthy end-to-end after the reset.

---

## 1. Scope

### In scope
- Delete stale ML artifacts automatically as part of the seed script
- `DbSeedTests.cs` — a manually-run xUnit test that seeds realistic synthetic data into the real dev DB
- End-to-end verification: seed → app bootstrap → manual retrain via UI → inspect meta.json

### Out of scope
- Changes to production code (M7 ML pipeline is untouched)
- Schema migrations
- Deadline parser or any other feature module
- Automated CI execution of the seed test (it is a dev tool, not a regression test)

---

## 2. Cleanup

The seed script deletes the following files at the start of its execution if they exist:

```
%AppData%\SmartStudyPlanner\models\study_time.zip
%AppData%\SmartStudyPlanner\models\meta.json
```

This forces the app to re-bootstrap cleanly from the synthetic seed data on next startup
(`MLModelManager.InitializeAsync` → no zip found → `TrainOnSeedAsync`).

---

## 3. Seed Script Design

### Location
```
SmartStudyPlanner.Tests/DevTools/DbSeedTests.cs
```

### Tag
```csharp
[Trait("Category", "Seed")]
```

Run command:
```bash
dotnet test --filter "Category=Seed"
```

### Execution order inside the test

**Step 1 — Cleanup artifacts**
Delete `study_time.zip` and `meta.json` from AppData models directory.

**Step 2 — Ensure DB scaffolding**
Create one `HocKy` (if none exists), two `MonHoc`, and 10 `StudyTask` records via `AppDbContext`
directly. Tasks are required because `GetStudyLogsAsync(hocKy)` filters logs through task→subject→semester.

| Entity | Count | Key values |
|--------|-------|-----------|
| HocKy | 1 | NgayBatDau = 90 days ago, NgayKetThuc = 60 days from now |
| MonHoc | 2 | SoTinChi = 2 (nhẹ), SoTinChi = 4 (nặng) |
| StudyTask | 10 | 5 per MonHoc, DoKho spread 1–5, LoaiCongViec varied |

**Step 3 — 180 synthetic StudyLogs, 3 groups**

Fixed seed `rng = new Random(42)` for reproducibility. Gaussian noise ±15% applied to all labels.
`CreatedAtUtc` values spread evenly over the past 60 days so they appear as accumulated real usage.

| Group | Rows | SoPhutHoc range | Assigned to |
|-------|------|-----------------|-------------|
| Nhẹ (light) | 60 | 20–60 min | Tasks with DoKho 1–2 |
| Trung (medium) | 60 | 60–120 min | Tasks with DoKho 3 |
| Nặng (heavy) | 60 | 120–240 min | Tasks with DoKho 4–5 |

`DaHoanThanh = true`, `SoPhutDuKien` set to the group midpoint.

---

## 4. Verification Flow

After running the seed script:

```
dotnet test --filter "Category=Seed"
  → artifacts deleted
  → HocKy + MonHoc + 10 tasks + 180 StudyLogs in DB
```

Then:

1. Start the app
   - `InitializeAsync` finds no zip → bootstraps from `SeedDataGenerator.Generate(180)` → model ready
2. Navigate to Analytics page
   - "Tối ưu AI" button is **enabled** (≥20 logs present)
3. Click "Tối ưu AI"
   - Spinner appears → retrain runs with 180 real StudyLogs → label "Đã cập nhật model lúc HH:mm"
4. Inspect `%AppData%\SmartStudyPlanner\models\meta.json`:

```json
{
  "SeedOnly": false,
  "LogsUsedCount": 180,
  "ModelVersion": 2
}
```

---

## 5. Acceptance Criteria

- [ ] Running `dotnet test --filter "Category=Seed"` completes with 1 passed, 0 failed
- [ ] `study_time.zip` and `meta.json` are absent from AppData before the app starts
- [ ] 180 StudyLog records exist in the DB after the seed
- [ ] App starts without errors or schema mismatches
- [ ] "Tối ưu AI" button is enabled in Analytics
- [ ] After clicking retrain: `meta.json` shows `SeedOnly=false`, `LogsUsedCount=180`, `ModelVersion ≥ 2`
- [ ] Dashboard page loads normally — no regressions in task list or time suggestion column

---

## 6. File Map

| Action | Path |
|--------|------|
| **Create** | `SmartStudyPlanner.Tests/DevTools/DbSeedTests.cs` |

No production files are modified.

---

## 7. Non-goals

- This is not a migration script — it does not preserve or transform existing data
- The seed test is not tagged for CI — run manually only
- This does not replace organic data accumulation; it only verifies the pipeline is functional
- The seeded logs are dev data — they can be left in place or cleared before real usage begins

---

## 8. Forward compatibility

Once the pipeline is verified:
- Auto-retrain continues to work organically: ≥50 new StudyLogs since `LastRetrainedAt` → triggers on next app startup
- Manual retrain remains available via "Tối ưu AI" button (≥20 logs)
- The seed script can be re-run at any time after a future DB reset to restore a verified baseline
