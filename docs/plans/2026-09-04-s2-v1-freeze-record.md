# S-2 — `GuidelineVersion: v1` freeze record

**The owner ratified `v1` on 2026-09-04 as the frozen specification the §10 reproducibility test runs
against.** This file records the anchor so that **"frozen" is verifiable rather than asserted.**

**Ratified for testing, not validated.** No figure from the test exists, and **S-2 has not passed.**

## Anchor

| | |
|---|---|
| **Artifact** | [`../specs/annotation-guideline.md`](../specs/annotation-guideline.md) |
| **Frozen at commit** | `da98e73ff64565729e7fe9f2f8afca6c4bdbf306` |
| **`sha256` of the file at that commit** | `dd4fc2736d83c18c373161fc371070a967e082bbbf7987a142a434103684a433` |
| **Size** | 37024 bytes |
| **Timestamp** | `2026-09-04T20:23:14+07:00` |

**To verify** — the frozen text is whatever that commit holds, and the hash is over the file's bytes as
committed:

```bash
git show da98e73ff64565729e7fe9f2f8afca6c4bdbf306:docs/specs/annotation-guideline.md | sha256sum
# dd4fc2736d83c18c373161fc371070a967e082bbbf7987a142a434103684a433
```

A later edit to the working file does **not** disturb this anchor — it makes the divergence visible,
which is the point. If the file's current content no longer hashes to the value above, **the working
copy is not the frozen `v1`**, and any test scored against it is scored against something else.

## What the freeze covers

Per §14 of the specification: the decision procedure (§3), the six boundary rules `B-1`–`B-6` (§4), the
Difficulty anchors and their three rules (§5), the catalogue exactly as evidence-derived (§6), the 5-way
adjudication procedure (§8), the thresholds — **TaskType ≥ 17/20, Difficulty ≥ 18/20, exact match** —
and every reporting rule (§10).

**None of it may be changed after test results are seen.** S-2.7 already carried that invariant for the
thresholds; the freeze extends the same discipline to the rest, because a specification revised after
seeing its own score measures nothing.

**Legitimate revision** — after a **recorded** failure — is a `GuidelineVersion` bump under §11, run
against the pre-reserved **v2 retest batch**. That path is open. Editing `v1` in place is not.

## What is *not* frozen, and stays open

| | |
|---|---|
| The three **zero-evidence boundaries** | Routed to **S-4 authored examples**. Not filled here, not filled by authoring into `v1` |
| The **short boundaries** | Stand as catalogue-coverage findings (§7). **Not re-drawn** |
| The **`kiểm tra` defect rows** in the seed | Preserved as observed material (D-4). Seed unmodified |
| **FU-1**, **S-6**, **Gold-A adjudication** | Out of S-2 by their own rulings |

## Gates that remain shut

| Gate | State |
|---|---|
| **DFD-2** — the labelled-data bar | **NOT satisfied.** Ruled 2026-09-04: for this execution DFD-2 is satisfied **only after the §10 test passes**, unless a later explicit owner decision changes the interpretation. Neither the spec's existence nor its ratification lifts it |
| **§10 reproducibility test** | **NOT performed.** Blocked on reader recruitment |
| **Independent reader** | **NOT recruited.** Owner action. Q-2 establishes that a network *exists*, not that anyone is assigned. **No AI substitute** (S-2.2) |

## The sealed reservation is untouched

The 20-row scored batch stands exactly as sealed by S-0, and is **not** re-reserved on the strength of
its shared-label vs third-pass-forced composition. That composition is a **required diagnostic attached
to every S-2 figure — never a selection or exclusion criterion.**

**The 119-vs-121 / 155-vs-157 residual remains sealed and unresolved.** Nothing in S-2 touches it.
