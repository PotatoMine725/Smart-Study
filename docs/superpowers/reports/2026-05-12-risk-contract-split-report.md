# Risk Contract Split Report
## Report · 2026-05-12

## What I attempted

I split the risk analyzer contract boundary one step further so the codebase can gradually move toward `Core/Risk` without breaking the legacy namespace immediately.

## What happened

The first attempt introduced a namespace conflict because `SmartStudyPlanner.Services.RiskAnalyzer` already contains a `RiskAssessment` type.

I corrected the direction by keeping the legacy `Services.RiskAnalyzer.RiskAssessment` type and making it inherit the Core model, while preserving the legacy `IRiskAnalyzer` interface shape for now.

A later attempt hit enum interop problems between legacy and Core risk levels, so I simplified the adapter mapping back to an explicit cast path, and ensured the legacy enum values mirror the Core enum values.

## Current status

- The contract split is still in progress.
- The legacy adapter path is now aligned to the Core enum values.
- I have not yet rerun build/test after this final correction in this message.
- The next step is to rerun build/test/lint and only continue once everything is green.

## Notes

- This is intentionally a one-step-at-a-time refactor.
- I am keeping the old service contract alive until all callers can move safely.
- Any remaining namespace/type mismatches should be resolved before touching the next evaluator/service.
