# S-2 — owner decisions, recorded 2026-09-05

**These continue the owner-decision series** opened in
[`2026-09-04-s1-limited-taxonomy-review.md`](2026-09-04-s1-limited-taxonomy-review.md) §6, which
holds `D-1`–`D-5` dated 2026-09-04 and is the series frozen `v1` cites — §10 cites `D-2`, §13 cites
`D-4`.

> **Numbering correction.** Working records written earlier on 2026-09-05 cited **`D-5`** for the
> *unresolved-scoring* constraint. That is wrong: **`D-5` in this series is "the sealed residual."**
> The constraint is renumbered **`D-6`** below and the series continues at `D-7`. The stale citation
> has been corrected in the reader-package manifest, the validator and the coverage-expansion plan.
> **No decision changed — only its label.**

**Nothing here is a test result.** The §10 reproducibility test remains **not performed**, no reader
is recruited, nothing has been annotated, adjudicated or scored, and **no figure from §10 exists.**

---

## The finding that prompted `D-7` and `D-9`

**Four of the twenty reserved rows are template-identical to entries in the frozen `v1` §6
catalogue**, differing only in the course name.

| Item | Frozen `v1` line | Similarity |
|---|---|---|
| `R-01` | 332 | 0.87 |
| `R-09` | 265 | 0.85 |
| `R-16` | 256 | 0.84 |
| `R-14` | 396 | 0.76 |

Measured against all 38 quoted catalogue examples, the twenty rows split cleanly: **four at
0.76–0.87, the remaining sixteen below 0.60, and nothing in between.** The affected set is therefore
unambiguous and needs no judgement call about where to draw a line.

**`v1` §6 states the invariant this violates**, in its own words:

> The reserved 60 rows are absent from this catalogue by construction — an example drawn from the
> scored batch would train a reader on a row they are later measured against.

**The exclusion was implemented at row granularity while the corpus is template-generated** — §7
records one boundary with *"6 rows but 3 templates"* — and the catalogue's own pre-registered
selection rule deduplicates **by template**. So template siblings passed straight through. There are
**zero verbatim matches**: the letter of the invariant holds and its spirit does not. This is a
defect in the exclusion rule's granularity, not in anyone's conduct.

**The second half of the finding.** §6 is not only worked examples. Across its 19 entries it prints
**19 historical pass-label progressions, 19 Difficulty values, 19 row `sha256` (15 distinct) and 57
source `file:line` locators** — four categories that are all on the reader-package exclusion list,
reaching the reader inside the frozen guideline. Each of the four twins sits beside an entry
printing a full three-pass progression.

**The derived labels are recorded nowhere** — not here, not in the manifest, not in any report. The
claim made is the template match and the disclosure counts, nothing further. The owner performs the
Gold pass, so quoting those values into a working record would prime the very pass they anchor.

---

## `D-6` — the scoring treatment of `unresolved` remains unauthorised

**The owner has not authorised how an `unresolved` response scores.** The reader keeps the ability
to mark it, **no scoring semantics were added to `v1`** or to its Vietnamese rendering, and **no
reader-facing text claims any treatment for it** — not that it counts, not that it does not count,
not that it is safe or costless.

**Why it is a standing constraint and not a detail.** A reader told that `unresolved` is harmless
will reach for it; a reader told it is penalised will guess instead. Either statement changes the
measurement, so the honest instrument makes neither. **Enforced by validator check `C11`**, in
English and Vietnamese, and demonstrated firing.

## `D-7` — the 20-row gate stands; a 16-row reading is pre-registered beside it

**The pre-registered gate is unchanged**: `TaskType` **≥ 17/20**, `Difficulty` **≥ 18/20**, full-batch
scoring, exact match. **S-2.7's invariant is untouched — no threshold was altered.**

**Additionally, the sixteen rows that are not template twins are scored and reported as the primary
evidence**, at **≥ 14/16 `TaskType`** and **≥ 15/16 `Difficulty`**. Those follow from the frozen
85% / 90% applied to a denominator of 16 — 13.6 and 14.4 — **rounded up**, which makes the secondary
reading marginally stricter (87.5% / 93.75%) rather than looser. The four excluded items are `R-01`,
`R-09`, `R-14` and `R-16`.

**Both figures travel together.** Neither may be quoted alone, under the same discipline `G4` imposes
on batch composition.

**Why this and not a re-draw.** §14 holds the sealed batch unchanged and forbids re-drawing the
catalogue; swapping rows would also spend reserve held for the `v2` retest and would need this same
check re-run on the replacements. The gate is left exactly as frozen, and what is added is a
*reading*, not a threshold change.

**The timing is the point.** This was ruled **before any annotation**, so it is pre-registration in
the ordinary sense. Once the reader starts, S-2.7 closes this door permanently — an adjustment made
after results exist would be exactly what the invariant forbids.

## `D-8` — the Vietnamese rendering is the instrument, and both passes use it

**Both the owner's Gold pass and the reader's blind pass run on the Vietnamese rendering.** The
readers meet the Q-2 conditions but do not study IT or any technical subject, and `v1` is written in
English; an instrument the reader cannot read measures reading comprehension, not reproducibility.

**Running both passes on the same text is what removes the confound.** Had the Gold pass used
English `v1` while the reader used the rendering, every disagreement would be ambiguous between a
guideline defect and a translation artifact — in the one measurement that must not carry an
uncontrolled variable.

**Scope that travels with every figure:** the result measures **reproducibility of `v1` as rendered
in Vietnamese.**

**`v1` itself is unchanged** — byte-exact at `dd4fc273…684a433`, still shipped, still the text with
authority where the two are read against each other.

**Discharged 2026-09-05.** The owner **read the rendering through and ratified it as pass.** Its
anchor — `sha256` `3b1dbd249f3dce248b4ae0a73d5ea5ae45bada33b614d233e01c8ec6a07ca61c` — is recorded in the
[`v1` freeze record](2026-09-04-s2-v1-freeze-record.md), so the ratification is verifiable rather
than asserted.

## `D-9` — the catalogue's provenance metadata is stripped from the rendering

**Removed, per entry:** the row `sha256`, the source `file:line` locators, and the historical
pass-label / Difficulty line.

**Kept:** **every example row verbatim, and every word of commentary.** All 19 entries are present.

**Why this costs the reader nothing.** Those three are provenance metadata, not decision support —
and `v1` §6 itself rules that **no entry may be cited as its row's label.** Stripping them removes
nothing the reader is meant to reason from, while taking 19 label progressions and 57 locators out
of the hands of an annotator who is instructed not to look anything up.

**Divergence from `v1` is metadata-only**, it is recorded here and in the manifest, and frozen `v1`
still ships beside the rendering byte-exact. **Enforced by validator check `C18`.**

---

## What remains open

| Open | Owner action |
|---|---|
| ~~Ratification of the rendering (`D-8`)~~ | **Done 2026-09-05** — read through, ratified as pass, anchor recorded |
| **Reader recruitment** | Q-2 establishes that a network exists; it assigns nobody (`v1` §14). **No AI substitute** (S-2.2) |
| **The §10 test** | Not performed, and not to be started before a reader is recruited |

**Reader recruitment is now the only thing between here and the test.**

## Decisions made in producing this record

**`R-1` — recorded the D-series continuation in a new dated file rather than appending to the S-1
review.** That review's §6 is titled *"Owner decisions, recorded 2026-09-04"*; adding 2026-09-05
rulings under a 2026-09-04 heading would date them wrongly, and retro-editing a dated governance
section is the failure mode the freeze discipline exists to prevent.

**`R-2` — corrected the `D-5` mis-numbering rather than leaving it and adding a note.** A citation
that resolves to the wrong ruling is worse than no citation: a reader chasing "D-5 — unresolved
scoring" lands on "the sealed residual" and concludes the record is unreliable. The label was
changed; no decision was.

**`R-3` — reported the catalogue overlap with ids, line numbers and similarity, and withheld the
labels.** Enough for the owner to verify the finding independently and act on it; not enough to
prime the Gold pass. The same restraint applies to this document, the manifest and every report.

**`R-4` — quantified the disclosure before proposing a remedy.** The first report of this finding
described §6 as worked examples that could be pattern-matched. Opening it fully showed it also
carries labels, hashes and locators, which is a materially worse and differently-shaped problem.
The remedy was re-proposed against the measured contents rather than against the first impression.
