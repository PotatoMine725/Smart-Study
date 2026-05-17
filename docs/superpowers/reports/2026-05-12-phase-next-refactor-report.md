# Phase Next Refactor Report
## Report · 2026-05-12

## What was implemented

I started the next refactor phase by separating the risk domain models from the legacy `Services.RiskAnalyzer` namespace and placing them under `Core/Risk/Models`.

### Changes made
- Added `SmartStudyPlanner/Core/Risk/Models/RiskLevel.cs`
- Added `SmartStudyPlanner/Core/Risk/Models/RiskAssessment.cs`
- Updated `SmartStudyPlanner/Core/Risk/Aggregators/RiskAggregator.cs` to use the new core models namespace
- Updated `SmartStudyPlanner/Core/Risk/RiskOrchestrator.cs` to reference the new core models namespace

## Why this phase

This is a low-risk boundary extraction step that helps:
- move risk output into the new Core layer
- reduce coupling to legacy service namespaces
- prepare for future scheduling/risk orchestrator splits
- keep the MVVM/offline-first shape intact

## Verification status

- I have not yet run the full build/test cycle after this phase.
- The next step should be a build/test pass to confirm the new Core model move does not break legacy references.

## Notes

- This refactor is intentionally incremental.
- I did not change the public behavior of the risk calculation pipeline.
- The next clean-up step will likely be adding legacy bridge types or adjusting remaining references from `Services.RiskAnalyzer` to `Core.Risk.Models`.
