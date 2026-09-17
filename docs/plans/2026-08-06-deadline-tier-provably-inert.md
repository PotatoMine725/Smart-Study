# Deadline-filtered tier in `GenerateScheduleWithIdentity` is provably inert (output, not execution)

**Date:** 2026-08-06 · **Status:** Finding (Card F, T3.3, review round) · Canonical home for a proof
previously duplicated near-identically in three places — see "Where this used to live" below.

## Claim, precisely

In `WorkloadServiceImpl.GenerateScheduleWithIdentity`'s per-chunk placement loop, the day-selection
logic has two tiers:

```csharp
var targetDay = days.Where(d => d.TotalMinutes < capacityMinutes && d.Date <= hanChotDate)
                   .OrderBy(d => d.Date)
                   .FirstOrDefault()
               ?? days.Where(d => d.TotalMinutes < capacityMinutes)
                   .OrderBy(d => d.Date)
                   .FirstOrDefault();
```

Tier-1 (deadline-filtered) and tier-2 (deadline-agnostic fallback) **always produce the same
result**, on every input, given the allocator's current shape. This is a claim about *output*, not
about *execution*: tier-2's fallback branch runs whenever a task's deadline has already passed
relative to every day currently holding room — e.g. anything landing past day 5 in the flagship
fixtures, since `WorkloadServiceScheduleTests.NewTask` hardcodes `HanChot = FixedNow.AddDays(5)`.
The branch is live code, reached routinely. What never happens is tier-1 and tier-2 *disagreeing*
about which day to pick.

## Proof

Let `E` = the earliest day (by `Date`) in the current `days` list with room (`TotalMinutes <
capacityMinutes`), computed **ignoring** the deadline filter — i.e., exactly what tier-2 computes.

- **If `E <= HanChot`:** `E` itself satisfies tier-1's filter (`Date <= HanChot` and has room), and
  `E` is by definition the minimum `Date` among *all* days with room — so it is also the minimum
  among the smaller, deadline-filtered subset that contains it. Tier-1 returns `E`.
- **If `E > HanChot`:** every day with `Date <= HanChot` must be full — if any such day had room, it
  would contradict `E` being the *earliest* day with room. So tier-1's candidate set is empty, tier-1
  returns `null`, and the `??` falls through to tier-2, which returns `E`.

Either branch yields `E`. This holds for **any** deadline configuration — same-deadline fixtures,
varied deadlines, deadlines in the past, priority order scrambled against deadline order — because
the argument never assumes anything about how many distinct deadline values exist, only that both
tiers share the same room predicate and days are totally ordered by `Date`.

Since a day's capacity only grows (nothing in the current algorithm ever frees room on a previously
used day) and days are visited in `Date` order, `E` itself is non-decreasing across the whole
allocation run — the day-selection sequence is monotonic, for any input.

**Verified two ways**, not just derived: algebraically (above), and empirically against the real
allocator with two adversarial constructions — scrambled priority/deadline order, and a deadline in
the past — both producing strictly monotonic day sequences with zero mismatches. A follow-up
independent re-implementation (20,000 randomized trials) also found zero mismatches.

## Consequence: no mutation check is possible for this tier

Because tier-1 and tier-2 are provably output-equivalent on every input, **no mutation of the
deadline-filter clause can change any observable output of `GenerateScheduleWithIdentity` today.**
This is not "untested" or "no input has revealed it yet" — it is the strongest available form of
inertness. Consequently:

- No discriminating test belongs in `WorkloadServiceScheduleTests.cs` for this clause. Writing one
  would necessarily be vacuous (green regardless of whether the clause is present, absent, or
  mutated) — exactly the "signal that can't fail" anti-pattern this project guards against elsewhere.
- The clause is not dead code to delete, though: it becomes live (output-affecting) the moment
  something makes a day's capacity non-monotonic — e.g. the future `Optimize()` / T3.9 pass-loop
  seam reordering or rejecting placements, or `IConstraintValidator` rejecting a placement and
  forcing a retry. It was built ahead of that need per CP-3's ratification, not because today's
  allocator requires it.

## Where this used to live

The full derivation was originally written out near-identically in three places (drift risk — T3.9
would need to find and update all three): `WorkloadServiceImpl.cs`'s placement-loop comment,
`WorkloadServiceScheduleTests.cs`'s class doc comment, and `WorkloadServiceIdentityTests.cs`'s class
doc comment. All three now carry a 2-3 line summary and a pointer here instead.
