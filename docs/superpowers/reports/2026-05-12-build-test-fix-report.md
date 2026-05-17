# Build/Test Fix Report
## Report · 2026-05-12

## What was fixed

I fixed the failing URL assertion in `TaskNotesViewModelTests` by aligning the production code with the test expectation for link storage.

### Code change
- In `SmartStudyPlanner/ViewModels/QuanLyTaskViewModel.cs`, `AddLink()` now stores `uri.OriginalString` instead of `uri.ToString()`.
- This preserves the exact user-entered URL shape and avoids normalizing `https://example.com` into `https://example.com/`.

## Verification

### Build
- `dotnet build SmartStudyPlanner.slnx`
- Result: passed

### Test
- `dotnet test SmartStudyPlanner.slnx`
- Result: passed
- Summary: `146` passed, `0` failed

## Notes

- Build/test still emits existing warnings about nullable reference types and the vulnerable `System.Drawing.Common` package.
- Those warnings were not introduced by this fix.
- The repository is still solution-based on `SmartStudyPlanner.slnx`, not `.sln`.

## Outcome

The codebase is green again for the current build/test run, and the URL handling behavior now matches the existing test contract.
