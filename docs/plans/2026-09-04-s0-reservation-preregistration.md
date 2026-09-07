# S-0 — Reservation event, pre-registration

**Written and committed BEFORE selection runs.** The git history is the evidence of that order:
this file is committed in its own commit, and the snapshot artifact in the next one. S-2.5 requires the
composition of both partitions to be *pre-registered before any S-1 inspection*, and principle 2 of the
2026-09-04 Outcomes requires that **selection rules are fixed before the data is seen**. A rule written
after seeing the rows it selects is not a pre-registration, whatever it is called.

**Status: `pre-registered`.** Executed under the owner authorization of **2026-09-04** (rev 3 accepted
as the governing executable plan at commits `0265386`, `fc95385`, `ee4a969`, `7fb6ab2`).

## Authority

| Element | Ruling |
|---|---|
| Reserve before S-1 reads any row text | **S-1.2**, hardened into a binding precondition by **S-1.6** |
| One event, 40 rows, two pre-partitioned batches | **S-2.5** |
| Q-1's batch reserved in the same event | **S-1.2** |
| Q-1 draws from the contested backlog | **S-2.6** |
| One stratified batch; contested carry the TaskType axis, additional rows span Difficulty | **S-2.3** |
| Composition **and** scoring denominators pre-registered; per-stratum breakdown required | **S-2.4/G1, G2** |
| Identity keys on content hash + source file + line; the event **materialises a snapshot artifact, not a query** | **S-2.10 / H1, H3** |
| **Master contested pool = the 133 distinct production-relevant contested rows** | **Owner resolution, 2026-09-04**, of the S-2.6 pool parameter |

**The pool ruling, stated as the owner gave it.** The master contested pool is **133 distinct rows**.
The earlier `J-1` value of **36** remains the original cross-pass subset, **not** the current full
contested pool. **157 is a count of contested *appearances*, not a pool size, and is not used as one
here.** Appearances and rows are different dimensions and are never added or equated.

**This selection is not a taxonomy decision and not a Gold-A decision.** It resolves the S-2.6 pool
parameter that reservation required, and nothing else.

## Identity key (S-2.10/H1)

Each reserved row is identified by:

- `hash` — **SHA-256 of the input text**, UTF-8 encoded, hex. This is a **locator for the reserved
  snapshot, not long-lived identity** (H1); S-3 will introduce the stable `RowId` that retains it.
- `occurrences` — every `(source file, line number)` where that text appears in the three annotation
  passes. Line numbers are 1-based **including the header line**, so they match a text editor.

**Row text is not stored in the snapshot.** The hash is the locator; storing the text would make the
artifact a copy of the corpus rather than a reservation over it, and reading it back would be exactly
the S-1 inspection this event must precede.

## Derivation of the pool

Sources, all committed:

| Pass | File | Label column |
|---|---|---|
| 1 | `datasheets/normalized_dataset.csv` | `LoaiTask` |
| 2 | `datasheets/normalized_dataset_m8a.csv` | `TaskType` |
| 3 | `datasheets/normalized_dataset_m8a_uniform.csv` (v3 relabel in place, commit `9603c17`) | `TaskType` |

1. **Cross-pass**: join pass 1 × pass 2 on input text. Disagreements where the old label **survived**
   into the new taxonomy (i.e. excluding the retired `Khac` / `DuAn` forced moves) are the audit's
   genuine disagreement set.
2. **Production-relevant**: restrict to rows where **neither** label is a class dropped from the
   production enum (`NhacNho`, `OnTap`).
3. **Third pass**: join pass 2 × pass 3 over their shared window; relabelled rows, restricted the same
   way.
4. **Master pool** = the **union**, deduplicated by input text.

### Strata (S-2.4/G2)

The pool carries three provenance strata, which are the breakdown every quoted figure must travel with:

| Stratum | Meaning |
|---|---|
| `cross_only` | Contested by the cross-pass comparison only |
| `third_only` | Contested by the third pass only |
| `both` | Contested by both |

**Stratification is on provenance, not on boundary.** Selecting on which TaskType boundary a row sits
would pre-judge which boundaries matter, and that is **S-1.6's ruling to make, not this event's**.
Boundary coverage is therefore **reported as a diagnostic and never used as a selection constraint**.

## Selection rule

**Deterministic, and unpredictable from content.** Rows are ordered by `hash` ascending and taken in
that order. Hash order cannot be steered toward or away from any row, so the rule cannot be tuned to
the data even in principle — that, and not the file's timestamp, is what makes this pre-registration
load-bearing.

**Allocation across strata is proportional to stratum size, by largest remainder.** Ties in the
remainder break toward the larger stratum, then by stratum name ascending.

**Draw order is fixed**: scored contested → Q-1 → retest contested, each taking the next unused rows in
hash order within its stratum. Every partition is disjoint from every other by construction.

### Difficulty-spread rows (S-2.3)

- **Universe**: the cross-pass common window **minus the master pool** — rows that are not contested,
  so the spread measures the Difficulty axis rather than re-testing the TaskType axis.
- **Level**: read from pass 2's `Difficulty`.
- **Allocation**: one row per level 1–5, then the surplus assigned to levels **3, 4 and 5**.
- **The level-1/level-2 gap is declared, not solved** (S-2.3). The v1 Difficulty threshold governs
  **levels 3–5 only**; levels 1–2 defer to authored examples in S-4. Levels 1 and 2 are still
  represented here so that the batch genuinely spans the range, but **the v1 threshold does not
  govern them** and no v1 figure may be quoted as if it did.

## Composition to be materialised

| Partition | Size | Composition |
|---|---|---|
| **Scored batch** | **20** | **12 contested** (`cross_only` 1 · `third_only` 9 · `both` 2) + **8 Difficulty-spread** (L1 1 · L2 1 · L3 2 · L4 2 · L5 2) |
| **Q-1 timed adjudication batch** | **20** | **20 contested** (`cross_only` 2 · `third_only` 15 · `both` 3) |
| **Clean retest batch** | **20** | **12 contested** (`cross_only` 1 · `third_only` 9 · `both` 2) + **8 Difficulty-spread** (L1 1 · L2 1 · L3 2 · L4 2 · L5 2) |
| **Total reserved** | **60** | 44 contested of 133 · 16 Difficulty-spread |

**Why 60 and not 40.** S-2.5's *"reserve 40 once"* covers S-2's two partitions; **S-1.2 separately
reserves "the S-2 reproducibility batch **and** Q-1's batch"** in the same event. 40 + 20 = 60 is a
**reading of the two rulings together**, recorded here as a reading and not as ratified text.

**Why the retest batch mirrors the scored batch.** S-2.5 holds it for a `v2` re-test after a threshold
failure. A re-test whose composition differs from the test it replaces is not a re-test of the same
thing. Mirroring is likewise a **reading**, flagged as one.

## Scoring denominators, pre-registered (S-2.4/G1)

- **TaskType**: 20 — the whole scored batch, **full-batch scoring** (S-2.4/B).
- **Difficulty**: 20 — the whole scored batch, with the **levels 1–2 caveat above travelling with every
  quoted figure** (S-2.4/G4).
- Thresholds are **TaskType ≥ 17/20** and **Difficulty ≥ 18/20**, exact match (S-2.7). **They must not
  be changed after observing results.** Within-one may be reported as a diagnostic; it is never the gate.
- **No bare percentage anywhere**: batch composition travels with every quoted figure (S-2.4/G4), and
  headline rates must not be described as corpus-wide agreement (S-2.4/G3).

## Pre-commitment on the sealed residual

Deriving row identities is **not** the re-measurement that the 119-vs-121 residual (§9.6 of rev 3) is
sealed against. But the derivation returns counts. **Any count it returns is recorded as an observation
of what the join returned, and nothing in this event closes, reconciles or amends that residual.** If an
observation bears on it, it is reported to the owner as an observation and left open.

## Exit criteria (rev 3, §S-0)

A recorded reservation event carrying: the 60 row identities, the pre-registered composition of every
partition, and the timestamp — **materialised as a snapshot artifact, not a query** (S-2.10/H3).

Artifact: `datasheets/reservations/2026-09-04-s0-reservation-snapshot.json`.
