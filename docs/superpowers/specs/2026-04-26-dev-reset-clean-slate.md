# Dev Reset — Clean Slate Strategy
## Spec · 2026-04-26

> **Scope:** this spec defines the cleanest reset strategy for the current dev-only environment. There is no production user data to preserve, so the preferred approach is a full local reset of database and ML artifacts, followed by deterministic reseed + retrain.
>
> **Goal:** remove schema drift and stale ML artifacts so the codebase can be re-initialized from a single source of truth.

---

## 1. Why a clean reset is the right move

The current workspace is dev-only and contains test data only. A clean reset is preferable because:

- it eliminates schema drift caused by incremental field additions
- it removes compatibility patches that are only needed for old local DB files
- it guarantees that M7 / M8 can be re-run from a clean baseline
- it reduces debugging noise from legacy SQLite state

This is **not** a user-data-preserving migration plan. It is a deliberate rebuild strategy for development.

---

## 2. Reset boundary

### 2.1 What gets reset
- local SQLite database file
- any generated model artifacts from M7 / ML bootstrap
- cached metadata for model lifecycle if stored on disk
- any temporary `.tmp` / stale export files related to ML training

### 2.2 What stays in source control
- code files
- docs files
- test fixtures / seed generators
- ML schema and service contracts

### 2.3 What is regenerated after reset
- SQLite schema
- seed semester / subject / task test data
- study logs used for analytics and ML bootstrap
- ML model zip / meta files

---

## 3. Data reset policy

### 3.1 Database reset
Preferred strategy:
- delete the local dev database file
- let the app recreate schema from code
- re-seed deterministic test data

This avoids trying to reconcile old schema with new models.

### 3.2 ML artifact reset
Delete the following local artifacts if they exist:
- trained model zip files
- model metadata JSON
- any intermediate model temp files

After deletion:
- bootstrap training runs again
- seed data generator supplies the initial training corpus
- predictions/fallback behavior are re-validated

### 3.3 Test data reset
Because the workspace is dev-only, test data may be regenerated from seed routines instead of preserved.

Recommended seed targets:
- one semester
- a few subjects
- a handful of representative tasks
- enough logs to satisfy M7 retrain bootstrap behavior

---

## 4. Risk analysis

### 4.1 Blast radius
This reset has a broad blast radius by design, but in dev-only context that is acceptable.

High-impact surfaces:
- `AppDbContext`
- repository initialization
- `SetupViewModel` / setup flow
- analytics bootstrap
- ML bootstrap and retrain

### 4.2 Why this is still safe
- no real user data exists
- deterministic seed data can recreate the baseline
- ML can simply retrain from the regenerated dataset

### 4.3 What could go wrong
- a seed routine may not fully repopulate required records
- model bootstrap may assume old files exist
- UI startup may rely on stale data that is now absent

Mitigation: write a clear initialization order and tests for startup.

---

## 5. Acceptance criteria

The reset is considered successful when:
- app starts without schema mismatch errors
- SQLite DB is recreated from scratch
- seed data appears as expected
- analytics pages load without missing-column failures
- M7 ML bootstrap retrains successfully
- M8 scaffolds remain compatible with the reset baseline
- no legacy model artifact is reused accidentally

---

## 6. Non-goals
- preserving historical user data
- writing a migration chain for production
- making old DB files backward compatible
- supporting multiple schema versions in dev reset mode

---

## 7. Decision summary

**Chosen strategy:** full clean reset for dev environment.

**Reason:** the workspace is dev-only, so the cost of preserving stale schema is higher than the cost of rebuilding from seed data.

**Policy:**
> When dev-only data is disposable, schema correctness and repeatable bootstrap matter more than compatibility patches.
