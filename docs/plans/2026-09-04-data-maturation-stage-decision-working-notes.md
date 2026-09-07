# Data Maturation — Stage Decision Working Notes (agent-authored)

> **Filed 2026-09-04. These are AGENT WORKING NOTES, not a ruling.**
>
> They are the raw record kept while the S-1 … S-8 + S-T decision pass ran: 23 `NEW EVIDENCE` blocks,
> the option analysis behind each decision, and the per-decision *Consequences recorded* detail that
> the outcomes record deliberately compresses.
>
> **The ruling lives in
> [`2026-09-04-data-maturation-stage-decision-outcomes.md`](2026-09-04-data-maturation-stage-decision-outcomes.md).**
> Where these notes and that record differ, **the outcomes record governs** — it was written last and
> was checked against the proposal text. Nothing here is owner policy; passages beginning
> *"Owner ruling (verbatim)"* quote the owner, everything around them is the agent's own analysis.
>
> **Why this file exists in the repository.** It was written to a session-scoped temporary directory
> and would otherwise be lost. The rev 3 rewrite needs the evidence blocks and consequence chains it
> holds — e.g. S-3.4's hazard about a canonical stamp inside export bytes, S-5.3's role-partition
> collision, and the chance-baseline arithmetic behind S-6.1/S-6.2 — none of which survives in
> compressed form.
>
> **Reading order is chronological, not structural.** Decisions appear in the order they were taken,
> interleaved with the evidence that forced them. Two headings use an older `## DECISION …` form and
> some carry a `PRESENTED … AWAITING OWNER` marker immediately above the ruling that answered them.

---

# S-1 owner-review working state (session 2026-08-28)

Target: docs/plans/2026-08-26-data-maturation-coverage-expansion.md (draft, rev 2)
Status: owner-review dialogue in progress. NOT yet written into the proposal.

## Verified premise correction (agent, 2026-08-28) — reproduced from CSVs

- Audit §E.1 arithmetic is CORRECT and reproduces exactly:
  495 identical / 325 forced / 208 genuine; 29.6%; transitions 105/29/26/17/16.
- Three label spaces exist, not one:
  - legacy  `normalized_dataset.csv`      : BaiTap, KiemTraThuongXuyen, ThiCuoiKy, OnTap, NhacNho, Khac, DuAn (7)
  - interim `normalized_dataset_m8a.csv`  : BaiTap, KiemTraThuongXuyen, ThiCuoiKy, OnTap, NhacNho (5)
  - PRODUCTION `LoaiCongViec` + seed_intents.csv + splits
                                          : BaiTapVeNha, KiemTraThuongXuyen, ThiGiuaKy, DoAnCuoiKy, ThiCuoiKy (5)
- TWO retirements at two steps (proposal collapses them):
  - Retirement A (Khac, DuAn), legacy->interim: 325 rows RELABELLED and kept.
  - Retirement B (OnTap, NhacNho), interim->production: 402 rows DELETED, zero remapped.
    BaiTap -> BaiTapVeNha was a pure rename (123/124 survived).
- Proposal S-1 table calling `OnTap->NhacNho` a "retired-class transition" is FALSE as placed:
  both are live inside §E.1's comparison (that is why it sits in the 208, not the 325).
  Audit §J-2 carries the identical error. FLAGGED, NOT FIXED this session.
- Consequence: of the 208 genuine disagreements, only 36 have both endpoints surviving into
  production (ThiCuoiKy->BaiTapVeNha 26, ThiCuoiKy->KiemTraThuongXuyen 8,
  KiemTraThuongXuyen->BaiTapVeNha 2). 172 involve OnTap or NhacNho.
  ThiGiuaKy and DoAnCuoiKy appear in NEITHER file §E.1 compared.
- App model fact: no reminder/revision domain entity. StudyTask carries LoaiCongViec, deadline,
  difficulty only. MainWindow.xaml.cs:173 toast is a deadline-urgency alert derived from tasks.

## Decisions taken

### S-1.0 — which taxonomy is S-1 reviewing?  RULED: **Option B** (owner, 2026-08-28)
Production taxonomy is the subject, AND retirement B (OnTap/NhacNho deletion) is reopened as one
explicit review question rather than inherited silently. Default stays "out" unless owner rules
otherwise. Scope stays inside P-3 (limited review) via P-3's explicit-decision escape hatch.

Affects: proposal §3 S-1 scope table, §4.1 "Gold-A adjudication scope = 208 rows"
(measured against the interim space, not production's), §8; audit §E.1 characterization, §J-1, §J-2.

### S-1.1 — should the taxonomy model reminders/revision?  RULED: **Option C** (owner, 2026-08-28)
Retirement B CONFIRMED for the taxonomy: OnTap/NhacNho do not return as production classes.
Reminder-ness is relocated, not deleted — it stays a DERIVED attribute/surface (as the deadline
toast already treats it), and the S-1 ruling must say so explicitly so S-2 does not re-litigate it
as a missing class. Consequence: the 402 rows are legacy debris; 172 of the 208 leave J-1 and join
the <=136 untraceable rows as an S-4 disposal question. Live cross-pass backlog ~36 rows.

## NEW EVIDENCE (agent, 2026-08-28) — a THIRD annotation pass, in the production space

normalized_dataset_m8a.csv -> normalized_dataset_m8a_uniform.csv:
- 810 texts in both; **121 relabelled (14.9%)** — a third pass §E.1 never measured.
- Transitions are almost entirely the CARVING OUT of the two production-only classes:
    BaiTap    -> DoAnCuoiKy  93
    ThiCuoiKy -> ThiGiuaKy   23
    NhacNho   -> DoAnCuoiKy   2 | KiemTraThuongXuyen -> ThiGiuaKy 2 | ThiCuoiKy -> DoAnCuoiKy 1
- So ThiGiuaKy and DoAnCuoiKy were CREATED by subdividing ThiCuoiKy and BaiTap. They are the
  residue of re-cutting the very boundary S-1 asks about.
- **All 121 relabelled texts reached seed_intents.csv.** The re-cut is baked into the shipped model.
- 290 rows are new in m8a_uniform (in neither earlier file); **136 of them reached the seed** —
  independently confirming the <=136 untraceable rows of audit §E.5 / J-3. Corroborates P-2/DFD-1;
  does not reopen them.
- Therefore §8's claim that S-1 "needs no infrastructure, no tooling and no data" is FALSE.

### S-1.2 — S-1's evidence basis + reservation timing?  RULED: **Option B** (owner, 2026-08-28)
RESERVE FIRST, THEN SAMPLE. The S-2 reproducibility batch and Q-1's batch are reserved BEFORE S-1
reads any row text; S-1 then reads from the remainder. Independence is established by sequence.
Fixes §S-2, which currently puts the reservation before S-2 — too late, since S-1 touches the same
pool first. Also invalidates §S-2's "forty rows out of 208 is affordable": the live pool is 36
(cross-pass) + 121 (third pass) = 157, in two pools with different meanings.
NOT EXECUTED THIS SESSION — S-1 is not authorized (§8). Reservation is part of S-1 execution.

## NEW EVIDENCE 2 — the ThiCuoiKy/BaiTap boundary was cut THREE different ways

- 22 rows were contested in BOTH passes; **all 22 start from ThiCuoiKy**.
- **20 of the 26 ThiCuoiKy->BaiTap rows were re-cut AGAIN by pass 3 into DoAnCuoiKy.**
  Trajectory: pass1 "final exam" -> pass2 "homework" -> pass3 "final project", same rows.
- These rows are in the shipped seed_intents.csv.
- `[inference]` A boundary three independent passes each cut differently reads as TAXONOMY
  SEMANTICS rather than annotation inconsistency — which is exactly the cause fork S-1 must rule.
  Marked as inference; S-1 must settle it, not inherit it from this note.

### S-1.3 — is the third pass in S-1's scope?  RULED: **Option B** (owner, 2026-08-28)
IN SCOPE AS EVIDENCE, NOT AS A NEW QUESTION. The 121 third-pass rows inform the existing
ThiCuoiKy/BaiTapVeNha boundary question; S-1 gains no new review item and makes no ruling on
whether the ThiGiuaKy/DoAnCuoiKy subdivision was correct. S-1's evidence pool = 157 rows
(36 cross-pass + 121 third-pass), covering all five production classes instead of three.
Reservation must draw from two pools with different meanings, and record which is which.

## NEW EVIDENCE 3 — "Difficulty is never trained on" is FALSE

Proposal §3 S-1 says Difficulty "is currently **never trained on**". True ONLY of the text
classifier. Difficulty (DoKho) has at least four live consumers:
1. **Study-time model FEATURE** — MLModelManager.cs:94 concatenates "Difficulty" into Features
   for a FastTree regressor. Trains on StudyTimeOutcomeLogs (real telemetry, MinRows=50).
2. **Formula fallback** — StudyTimePredictorService.cs:58: `difficultyBonus = (DoKho/5.0)*60.0`.
   An explicit LINEAR, EQUALLY-SPACED ordinal commitment, already in shipped code. Active
   whenever the model is not ready or confidence < 0.6.
3. **Risk evaluation** — PerformanceDropRiskEvaluator (tests: DoKho1->0, DoKho5->1).
4. **DifficultyLabelLogs** — QuanLyTaskViewModel.cs:327-341 records SuggestedDoKho / FinalDoKho /
   WasOverride. DFD-9b's designated real-data source; a suggestion mechanism already exists.
Text classifier genuinely does NOT feed it — TextClassifierInput.cs:20-23 says so explicitly
("difficulty prediction is a separate (deferred) model").

Measured: Difficulty cross-pass disagreement 167/1028 = 16.2% (reproduces audit exactly).
  transitions 5->4 (50), 3->4 (41), 1->3 (27), 4->2 (23), 5->3 (18), 2->3 (8)
  **magnitude: 99 one-step, 68 TWO-step** — 41% move two levels on a five-point scale.
Low-end coverage collapse across the lineage:
  legacy   1:8.6%  2:19.0%
  interim  1:4.0%  2:20.8%
  seed     1:0.2% (2 rows of 903)  2:5.6%
So shipped training data has essentially NO low-difficulty examples, while the fallback formula
assumes the scale is linear across all five levels.

ADJACENT, NOT REOPENED: the M8-A confidence-gate calibration concern (band [0.6,0.7) accuracy
0.000) is DEFERRED and stays out of scope. Noting only that StudyTimePredictorService uses a 0.6
gate too; no ruling sought here.

### S-1.4 — Difficulty semantics in S-1 scope?  RULED: **Option B, ratify** (owner, 2026-08-28)
S-1 DEFINES Difficulty as an explicit 1-5 semantic/ordinal scale with WRITTEN ANCHORS, so S-2 can
govern future labels. S-1 does NOT audit or change current consumers.
OWNER ADDITION: any `(DoKho/5)*60` or downstream consistency check is a SEPARATE FOLLOW-UP,
raised only AFTER the semantics are frozen. -> tracked below as FU-1.

## Follow-ups created (not S-1 scope)

- **FU-1** — Consumer consistency check for Difficulty. After the 1-5 anchors are frozen by S-1/S-2,
  check the shipped consumers against them: StudyTimePredictorService.cs:58 `(DoKho/5.0)*60.0`
  linear assumption, MLModelManager.cs:94 feature use, PerformanceDropRiskEvaluator, and the
  DifficultyLabelLogs SuggestedDoKho mechanism. Owner-raised 2026-08-28. NOT scheduled.

## NEW EVIDENCE 4 — the zero-coverage question is mis-framed

Proposal S-1 asks of KiemTraThuongXuyen / ThiCuoiKy: "Do they earn their place in a 5-class
taxonomy, or are they aspirational?" They are NOT aspirational — they are the two LARGEST
training classes:
    class                 train   test
    KiemTraThuongXuyen      188      0
    ThiCuoiKy               170      0
    DoAnCuoiKy              131     50
    BaiTapVeNha             124     56
    ThiGiuaKy                85     99
Together 358/698 = 51.3% of training. ThiGiuaKy is inverted: least trained, most evaluated.

CAUSE (measured): the train/test split is BY SOURCE FILE, not random.
  train = m8a_uniform (597) + synthetic_v3 (101);  test = collected_v4 (205), entire file.
  test texts are byte-identical to collected_v4; train/test text overlap = 0 (no leakage).
collected_v4 is AI-generated (P-1, ratified). So the ENTIRE evaluation set is AI-generated and
covers only 3 of 5 production classes.
=> This is an EVALUATION-DESIGN defect (S-6 territory), not a taxonomy question (S-1).

### S-1.5 — where does the zero-coverage item belong?  RULED: **Option B** (owner, 2026-08-28)
S-1 KEEPS a class-justification question, reframed onto product-intent grounds (same basis as
S-1.1), premise corrected — the two classes are 51.3% of training, not aspirational.
The EVALUATION DEFECT moves to **S-6**: file-level split, entire test set = AI-generated
collected_v4, 3-of-5 class coverage, no leakage (overlap 0). Not a taxonomy finding.

## NEW EVIDENCE 5 — the contested boundaries are concentrated on ONE axis

All contested rows across BOTH passes, production classes only (155 rows = 36 cross-pass + 119
third-pass; 2 of the 121 excluded for a non-production source, NhacNho->DoAnCuoiKy):
    BaiTapVeNha        <-> DoAnCuoiKy           93  (60%)
    BaiTapVeNha        <-> ThiCuoiKy            26  (17%)
    ThiCuoiKy          <-> ThiGiuaKy            23  (15%)
    KiemTraThuongXuyen <-> ThiCuoiKy             8  (5%)
    BaiTapVeNha <-> KiemTraThuongXuyen 2 | KiemTraThuongXuyen <-> ThiGiuaKy 2 | DoAnCuoiKy <-> ThiCuoiKy 1

  WITHIN a kind (test<->test, work<->work): 126 (81%)
  ACROSS kinds (test<->work)              :  29 (19%)

`[inference — NOT a ruling, S-1 must be free to reject it]` Annotators agree on test-vs-work 81%
of the time and disagree on scale/occasion. The five production classes read as an irregular
cross of two axes:
    kind:            test | work product
    scale/occasion:  routine | midterm | final
    BaiTapVeNha = work/routine | DoAnCuoiKy = work/final | KiemTraThuongXuyen = test/routine
    ThiGiuaKy = test/midterm   | ThiCuoiKy = test/final
  The cell work x midterm has no class. That may be why BaiTapVeNha<->DoAnCuoiKy carries 60% of
  all contested rows. If so the cause fork lands on TAXONOMY SEMANTICS, not inconsistency — but
  that is S-1's ruling to make on evidence, not mine to assert.

### S-1.6 — how is the cause fork restated?  RULED: **Option B, ratify** (owner, 2026-08-28)
S-1 issues a SEPARATE cause/ruling for EACH production-relevant contested boundary. Do NOT collapse
into one global verdict. Do NOT assume the two-axis (kind x occasion) reading above — it stays an
agent inference S-1 is free to discard; row-level evidence determines the cause per boundary.
Boundaries carrying volume: BaiTapVeNha<->DoAnCuoiKy 93 | BaiTapVeNha<->ThiCuoiKy 26 |
ThiCuoiKy<->ThiGiuaKy 23 | KiemTraThuongXuyen<->ThiCuoiKy 8.
OWNER ADDITION: any hold-out / reservation conflict is a SEQUENCING CONSTRAINT — reservation
executes BEFORE S-1 reads disputed rows. This hardens S-1.2 from a ruling into a binding
precondition on S-1 execution, and it governs the per-boundary reading S-1.6 requires.

**S-1 BLOCK CLOSED.** SEVEN decisions ruled: S-1.0 B / S-1.1 C / S-1.2 B / S-1.3 B / S-1.4 B+addition
/ S-1.5 B / S-1.6 B+addition. Follow-up created: FU-1.

## NEW EVIDENCE 6 — TaskType and Difficulty fail almost INDEPENDENTLY; pass 3 never re-annotated Difficulty

Pass 1->2 (legacy -> interim), genuine pool n=703 (1028 shared texts minus the 325 forced
Khac/DuAn relabels). **This fixes the denominator: the audit's 29.6% is 208/703, not 208/1028.**

    TaskType disagree            208  (29.6%)
    Difficulty disagree           96  (13.7%)   [the audit's 167/1028 = 16.2% is over the FULL
                                                 1028 incl. forced rows; both true, different scope]
    BOTH disagree                 11   <-- expected 28.4 under independence
    only TaskType                197
    only Difficulty               85
    agree on BOTH fields         410  (58.3%)

=> The two fields are NOT positively coupled; they are slightly NEGATIVELY associated. 85 rows fail
   only on Difficulty — invisible to any TaskType-only agreement test. Adding Difficulty to the
   reproducibility test is close to ADDITIVE, not redundant.

Pass 2->3 (interim -> uniform), 810 shared texts:
    TaskType disagree            121  (14.9%)
    Difficulty disagree            0  (0.0%)

INSTRUMENT VERIFIED: per-text Difficulty is single-valued inside BOTH files (0 texts carry varying
Difficulty within a file), so the join is well-defined. A naive multiset compare reports 134
"differences" — that is purely interim's 168 duplicate texts vs uniform's 0, NOT a value change.
The zero is real: pass 3 re-cut the TYPE taxonomy on 121 rows and carried Difficulty over
mechanically, without re-annotating it.

=> CONSEQUENCE for S-2: there is exactly ONE cross-pass measurement of Difficulty reproducibility
   (pass 1->2). The 121 third-pass rows S-1.3 admitted as evidence carry ZERO Difficulty signal.
   Difficulty's evidence base is thinner than TaskType's, which bears on any threshold set for it.

### S-2.1 — what does the reproducibility test measure?  RULED: **Option B, ratify** (owner, 2026-08-28)
TaskType and Difficulty are evaluated INDEPENDENTLY. Conditions attached by the owner:
  C1. the two dimensions are evaluated independently of each other;
  C2. BOTH must pass their OWN pre-registered threshold (no averaging, no trade-off between them);
  C3. the SAME pre-reserved 20-row holdout serves both dimensions — one batch, two measurements,
      NOT two batches;
  C4. the report must expose PER-DIMENSION agreement and per-boundary detail, not only a combined
      score. A single headline number is not an acceptable output.
Fixes §3 S-2 exit criteria, which say "agreeing at a rate" without saying agreeing on WHAT.
Consequence still open (goes to the threshold decision): whether Difficulty agreement is exact-match
or within-one-level. 68 of 167 cross-pass Difficulty moves were TWO-step on a 5-point scale.
C4 also lands on S-1.6: per-boundary detail is now required of the S-2 test report too, not only of
S-1's cause rulings.

### S-2.2 — R-1, who performs the two passes?  RULED: **Option B, ratify** (owner, 2026-08-28)
R-1 IS NOW CLOSED. Shape: **owner = Gold/reference pass; one independent human reader from the Q-2
network = BLIND reproducibility probe.** Both annotate the SAME pre-reserved 20 rows independently.
Report TaskType and Difficulty agreement separately, each vs its own pre-registered threshold.
AI may be added LATER as a SUPPLEMENTARY probe — never a substitute for the human transfer test.
Standing under DFD-8: owner's pass is the label; the reader's pass is a measurement, never Gold.

Consequences to carry into the reservation/threshold decisions:
- "BLIND" needs a delivery mechanism. The reserved rows carry historical labels from 2-3 prior
  passes. The batch handed to the reader must be STRIPPED of all prior labels and of the owner's
  pass. A raw CSV slice would leak them.
- The owner is NOT blind and cannot be (has seen the corpus). That asymmetry is acceptable because
  the owner's pass is the label, not the transfer measurement — but it must be stated, not hidden.
- A Q-2 human hour is now ON THE CRITICAL PATH for S-2 exit. Q-1 (which prices one adjudication)
  runs AFTER S-2, so the ask is currently open-ended. Recruitment + briefing is now a real task.

## NEW EVIDENCE 7 — chance-agreement baselines and the resolution of an n=20 test

Chance agreement = the rate two annotators would hit by drawing independently from the observed
marginal. It is the floor a threshold must clear to mean anything.

  seed_intents.csv (903 rows), TaskType   : 5 classes, near-uniform -> chance = **0.200**
  seed_intents.csv,            Difficulty : 3:431 4:287 5:132 2:51 1:2 -> chance = **0.353**
                               Difficulty, WITHIN-ONE-LEVEL           -> chance = **0.804**

=> The same numeric threshold means very different things per dimension. And a WITHIN-ONE-LEVEL
   Difficulty rule is nearly untestable corpus-wide: two annotators guessing hit 80.4%.

Resolution of an n=20 test (binomial):
  threshold >=17/20 (85%): a spec whose TRUE agreement is 0.85 passes only 65% of the time;
                           one whose true agreement is 0.75 still passes 23% of the time.
  95% Wilson CI at observed 17/20 = [64%, 95%] — 31 points wide.
=> n=20 CANNOT distinguish 0.85 from 0.75. The threshold is a DECISION GATE, not an estimate.
   This must be stated in the spec, or the number will later be read as a measurement.

BATCH-SIZE PROVENANCE (checked): the 2026-08-27 outcomes doc ratifies "20 held-back rows" for **Q-1
only**. It says NOTHING about the S-2 reproducibility test's size. The S-2 "20" is the PROPOSAL'S
OWN DRAFT NUMBER, not an owner ratification — so it is legitimately open, and rev 2 should stop
implying otherwise.

## NEW EVIDENCE 8 — the contested pool is UNFIT to test Difficulty

Scope correction: the contested production-relevant set is **155 contested EVENTS across 133
DISTINCT rows** (22 rows are contested in both passes). Earlier notes said 155; 133 is the number
of rows available to reserve from.

Difficulty marginal INSIDE that 133-row pool:  3:49  4:70  5:14  — **no level 1, no level 2 at all.**
  exact-match chance agreement on this pool      = **0.424**
  within-one-level chance agreement on this pool = **0.922**
Only 10 of the 133 carry any historical Difficulty-disagreement signal (and 0 of the 119 third-pass
rows can, because pass 3 never re-annotated Difficulty).

=> A 20-row batch drawn from the contested pool would contain ZERO low-difficulty rows, so the
   level-1 and level-2 anchors S-1.4 requires would NEVER BE EXERCISED. A within-one-level rule on
   this pool is passed by random labelling (92.2%). This collides with S-2.1's condition C3 (one
   batch serves both dimensions): the pool is enriched for the TaskType axis and degenerate on the
   Difficulty axis.
=> Deeper constraint: the shipped seed itself has 2 rows at level 1 and 51 at level 2. Low-difficulty
   anchors may not be testable on ANY existing data — that would push authored examples into S-4.

### S-2.3 — what pool is the batch drawn from?  RULED: **Option B** (owner, 2026-08-28)
ONE STRATIFIED BATCH: contested rows carry the TaskType axis; additional rows are drawn to span the
Difficulty range. Satisfies S-2.1/C3 (one batch, two measurements). The level-1/level-2 gap is
DECLARED, not solved: the Difficulty threshold at v1 governs levels 3-5 only; levels 1-2 are
deferred to authored examples in S-4.

## NEW EVIDENCE 9 — the low end of the Difficulty scale is SYNTHETIC-ONLY

seed_intents.csv Difficulty by Source:
  L1: total   2  -> synthetic_v3 1, collected_v4 1        <-- BOTH are synthetic/AI-generated
  L2: total  51  -> m8a_uniform 17, synthetic_v3 13, collected_v4 21   (34/51 = 67% synthetic/AI)
  L3: 431 | L4: 287 | L5: 132
132 of the 133 contested rows are present in seed_intents.csv (the pool is almost entirely shipped).
Seed rows NOT in the contested pool, by Difficulty: L1=2 L2=51 L3=382 L4=217 L5=119.

=> The level-1/2 gap is worse than scarcity. Under DFD-6 (synthetic = Silver/augmentation only) and
   DFD-8 (owner is Gold authority), reserving those rows would put synthetic data underneath a
   Gold-authority test. Human-origin low-difficulty rows are effectively L1=0, L2=17.
   Reinforces S-2.3's declared gap: levels 1-2 need AUTHORED examples (S-4), not a draw.

## NEW EVIDENCE 10 — the cost of splitting the batch between dimensions

Separation = P(pass | true agreement 0.90) - P(pass | true agreement 0.70). Higher = the test can
actually tell a good spec from a bad one.
    n=20, >=17/20 (85%) -> 0.87 vs 0.11, separation **0.76**
    n=12, >=10/12 (83%) -> 0.89 vs 0.25, separation **0.64**
    n=8,  >=7/8  (88%)  -> 0.81 vs 0.26, separation **0.56**
    n=8,  >=6/8  (75%)  -> 0.96 vs 0.55, separation **0.41**
=> Scoring each dimension only on its own stratum roughly halves the test's discriminating power.

### S-2.4 — composition + scoring scope?  RULED: **Option B, ratify** (owner, 2026-08-28)
FULL-BATCH SCORING: the whole pre-reserved 20-row batch scores BOTH TaskType and Difficulty.
Owner guardrails:
  G1. batch composition AND scoring denominators are PRE-REGISTERED.
  G2. the report includes the per-stratum breakdown (on top of S-2.1/C4's per-dimension detail).
  G3. headline rates MUST NOT be described as corpus-wide agreement.
  G4. the batch composition TRAVELS WITH EVERY QUOTED FIGURE — no bare percentage anywhere.
G3/G4 are a presentation invariant, not advice; they belong in the spec and in §4 of the proposal.

CORRECTION to my S-2.3 framing (does not change the ruling): I said reserving L1/L2 rows would put
"synthetic data underneath a Gold-authority test". Too strong — the test STRIPS prior labels, so a
row's synthetic origin taints its TEXT, not its label. The real level-1/2 problem is (a) scarcity
(2 and 51 rows) and (b) whether generated text exhibits genuine low-difficulty characteristics at
all. Also note DFD-1: NO part of this corpus is verified real/user-authored, so there is no strictly
"human-origin" stratum to prefer. The declared gap stands on scarcity + representativeness.

## NEW EVIDENCE 11 — the 133-row pool, by boundary and by claim

Distinct contested rows: 133  (cross-pass only 14 | third-pass only 97 | contested in BOTH 22)
DISTINCT ROWS per boundary (a dual-contested row counts in both):
    BaiTapVeNha        <-> DoAnCuoiKy          93
    BaiTapVeNha        <-> ThiCuoiKy           26
    ThiCuoiKy          <-> ThiGiuaKy           23
    KiemTraThuongXuyen <-> ThiCuoiKy            8
    BaiTapVeNha <-> KiemTraThuongXuyen 2 | KiemTraThuongXuyen <-> ThiGiuaKy 2 | DoAnCuoiKy <-> ThiCuoiKy 1
  111 rows contested on exactly one boundary; 22 on two.

COMPETING CLAIMS on the same 133 rows — the reservation must PARTITION them:
  1. S-2's contested stratum (reproducibility test)
  2. Q-1's 20 timed adjudication rows (ruled 2026-08-27; drawn from the live J-1 backlog)
  3. S-1's own per-boundary reading (S-1.6) — needs rows at EVERY boundary it must rule on
  4. the ambiguous-example catalogue's raw material (§3 S-2 design constraint 1)
Claims 1 and 2 must be disjoint from each other AND from what claim 4 sees during spec authoring.

CONSEQUENCE for S-2.1/C4 + S-2.4/G2: a 12-row contested stratum spread over 4 main boundaries is
~3 rows per boundary. Per-boundary detail is then QUALITATIVE (which rows disagreed and why), NOT a
per-boundary rate. The spec must say which, or 3/3 will be quoted as "100% on this boundary".

### S-2.5 — reserved size / scored size / split?  RULED: **Option C** (owner, 2026-08-28)
RESERVE 40 ONCE, in a single reservation event, PRE-PARTITIONED into:
  - a current 20-row scored batch (12 contested / 8 Difficulty-spread), and
  - a clean 20-row RETEST batch, held for a v2 re-test if v1 fails.
Composition of BOTH partitions is pre-registered BEFORE any S-1 inspection of row text.
Reader cost is unchanged from a 20-row test (only the scored batch is labelled).
Closes a hole §3 S-2 never addressed: a v2 re-test on already-labelled rows is not independent.
Consumes ~24 contested rows; with Q-1's 20 that is 44 of 133 (33%) — see S-2.6, which the owner
opened by asking whether Q-1 must draw from this pool at all.

## NEW EVIDENCE 12 — Q-1's pool is NOT owner-ratified, and J-1 has SHRUNK

Ratified text (outcomes doc 2026-08-27, mirrored in §4.2): "adjudicate 20 held-back rows; measure
total time and per-row time; record ambiguous cases and guideline gaps discovered. Estimate Gold-A
workload from the result rather than guessing." It says **nothing about which pool**.
The line "Rows drawn from the real J-1 backlog (the 208), so the timing generalizes to the work it
is estimating" is the PROPOSAL'S OWN TEXT (§3 S-2, two-exercise table) — agent-drafted, NOT ratified.
=> The owner's question is well-founded: Q-1's pool is genuinely open.

What DOES constrain it: S-4 defines Gold-A workload as J-1 (adjudicate disagreeing rows) + J-3
(dispose of untraceable rows) + J-7 (residue). So the estimate is tied to ADJUDICATION cost, and a
representative authored pool would measure ordinary labelling cost instead — a different number.

SIZING, post-S-1.1 — this changes the calculus:
  - J-1 as originally defined (audit §E.1 cross-pass, production-surviving): **36 rows**, not 208.
    (172 of the 208 left J-1 under S-1.1; §4.1's "208" and §4.2's "remaining ~188" are both obsolete.)
  - Newly discovered this session, third-pass contested, never in J-1: **97 rows**.
  - Whether those 97 JOIN J-1 as WORK is UNRULED. S-1.3 admitted them as EVIDENCE for S-1 only and
    explicitly gave S-1 no new review item; adjudicating them is S-4 territory. FLAG, do not rule.
=> If J-1 stays 36, a 20-row Q-1 batch times 56% of the entire backlog — that is not an estimate,
   it is most of the job. If J-1 becomes 133, 20 rows estimating 113 is a real extrapolation.

### S-2.6 — Q-1's pool?  RULED: **Option A, with a scoping condition** (owner, 2026-08-28)
Q-1 draws from the CONTESTED backlog. Its result estimates **contested adjudication time ONLY** —
explicitly NOT total Gold-A workload. J-3 disposal and any J-7 residue must be estimated or measured
SEPARATELY. **No invented weighting factor** may fold them together.
This SCOPES the ratified Q-1 ruling ("estimate Gold-A workload from the result"); the owner holds
that authority. It is a narrowing, not a reopening.
Rewrites required: §4.2 Q-1 row, §S-4 "the remaining ~188 are estimated from that", §4.1's
"208 + <=136 is the measured Gold-A adjudication scope".
NEW OPEN ITEM (S-4 territory, NOT ruled): how J-3 disposal effort and J-7 residue get estimated.

## NEW EVIDENCE 13 — J-1 and the third-pass rows are BOUNDARY-DISJOINT

J-1 as defined (36 cross-pass rows):
    BaiTapVeNha <-> ThiCuoiKy 26 | KiemTraThuongXuyen <-> ThiCuoiKy 8 | BaiTapVeNha <-> KiemTraThuongXuyen 2
  -> involves ONLY the three classes that existed in the interim space. ZERO DoAnCuoiKy, ZERO ThiGiuaKy.
Third-pass ONLY (97 rows, never in J-1):
    BaiTapVeNha <-> DoAnCuoiKy 73 | ThiCuoiKy <-> ThiGiuaKy 23 | DoAnCuoiKy <-> ThiCuoiKy 1
  -> involves ONLY the two classes pass 3 created.
The 22 dual-contested rows are the bridge (third-pass boundary: BaiTapVeNha<->DoAnCuoiKy 20,
KiemTraThuongXuyen<->ThiGiuaKy 2).

=> CONSEQUENCE of S-2.6/A: Q-1 prices adjudication on boundaries that EXCLUDE DoAnCuoiKy and
   ThiGiuaKy. The per-row time it yields does not automatically transfer to the 97, which is a
   second reason not to treat Q-1's number as a total-workload estimate. Consistent with the
   owner's scoping condition.
=> FEASIBILITY CHECKED, no conflict: Q-1 takes 20 of J-1's 36, leaving 16; S-2.5 needs 24 contested
   rows and has 16 + 97 = 113 available. Reservations remain disjoint.
=> UNRULED, surfaced by this: does "adjudicate" mean pick between the TWO disputed labels, or assign
   the correct label from all FIVE? A dual-contested row has three candidate labels. This changes
   the per-row time Q-1 measures AND is required content of the S-2 spec ("adjudication procedure").
   Queued for S-2, after the thresholds.

## NEW EVIDENCE 14 — what a headline threshold implies for the contested rows

Batch = 12 contested + 8 Difficulty-spread. The spread rows are not contested, so they agree at a
high rate and pull the headline up. Translation (this is the G3/G4 dilution, made explicit):

  headline    implied bar on the 12 contested rows (spread rows at 100% / 95%)
  >= 19/20 (95%)   ->   92% / 95%
  >= 18/20 (90%)   ->   83% / 87%
  >= 17/20 (85%)   ->   75% / 78%
  >= 16/20 (80%)   ->   67% / 70%
  >= 15/20 (75%)   ->   58% / 62%
  >= 14/20 (70%)   ->   50% / 53%

Chance floors a threshold must clear to mean anything:
  TaskType                              20%
  Difficulty exact-match                35% corpus-wide | 42% on contested rows
  Difficulty WITHIN-ONE-LEVEL           80% corpus-wide | 92% on contested rows  <-- unusable
NOTE: there is NO historical anchor for agreement UNDER A SHARED SPEC. Every prior measurement
came from passes with no common guideline. The thresholds are a judgement call, not an
extrapolation, and the spec must say so.

CORRECTION to the line above (same session): a NO-SPEC BASELINE does exist for Difficulty and I
initially overlooked it. Pass 1->2 Difficulty agreement on the 703-row genuine pool = 1 - 96/703 =
**86.3%**, achieved with NO shared guideline. Because TaskType and Difficulty fail near-
INDEPENDENTLY (EVIDENCE 6: 11 co-failures vs 28.4 expected), a batch enriched for TaskType conflict
is roughly UNENRICHED for Difficulty — so 86.3% is broadly comparable to what the 20-row batch would
show. A Difficulty threshold materially below ~86% would let a spec pass while proving nothing.
  CAVEAT, and it is load-bearing: the INDEPENDENCE of that baseline is `[unknown]`. Pass 3 copied
  Difficulty with ZERO changes across 810 rows — proof that at least one pass in this lineage was a
  carry-over/edit pass, not a re-annotation. If pass 2 was likewise an edit pass starting from pass
  1's values, 86.3% is inflated and the true independent baseline is lower and unmeasured.
  TaskType has no comparable baseline: the batch is enriched for TaskType conflict by construction,
  so the 70.4% pool figure does not transfer.

### S-2.7 — the two pre-registered thresholds?  RULED: **Bundle B, ratify** (owner, 2026-08-28)
  **TaskType  >= 17/20 (85%)** — no clean comparable baseline exists; 17/20 respects n=20's coarseness.
  **Difficulty >= 18/20 (90%)** — justified by the observed 86.3% no-spec historical agreement.
  **EXACT MATCH.** Within-one-level may be REPORTED as a diagnostic; it is never the gate
  (80% chance corpus-wide / 92% on contested rows — it cannot fail).
  Failure => spec revision + the pre-reserved v2 retest batch.
  **PRE-REGISTRATION LOCK: thresholds must NOT be changed after observing results.** This is an
  invariant of the test, not a preference. It belongs in the spec next to the numbers.
Implied bar on the 12 contested rows: TaskType 75%, Difficulty 83% (spread rows at 100%).

### S-2.8 — what does "adjudicate" mean?  RULED by the owner unprompted (2026-08-28)
**Assign the CORRECT label from the FULL current production taxonomy (all 5 classes)** — NOT a
2-way choice between the originally disputed labels.
Consequences:
- An adjudication may land on a class in NEITHER prior pass (e.g. a J-1 row disputed
  BaiTapVeNha vs ThiCuoiKy may be ruled DoAnCuoiKy).
- REFINES EVIDENCE 13: under 5-way adjudication the TASK is uniform across J-1 and the 97 — the
  adjudicator can reach DoAnCuoiKy/ThiGiuaKy from any row. What still differs between the two pools
  is the DIFFICULTY MIX of their disputes, not the shape of the work. Q-1's number therefore
  transfers better than EVIDENCE 13 implied, but still not automatically — consistent with S-2.6's
  scoping condition, which does not depend on this.
- Adjudication and annotation become the SAME operation (assign from 5 classes, ignoring prior
  labels), plus a recorded ruling. The spec's "adjudication procedure" is that operation + the
  record, not a separate method.
- Q-1's timing is for 5-way assignment, which is slower than a 2-way tiebreak. The measured number
  is therefore the honest one for the real work.

### S-2.9 — catalogue form?  RULED: **Option B, ratify** (owner, 2026-08-28)
BOUNDARY-INDEXED catalogue, 3-5 representative examples per production-relevant boundary.
Owner conditions:
  K1. S-2 does NOT adjudicate the remaining rows. J-1 stays S-4 workload, priced by Q-1.
  K2. PRE-REGISTER an example-selection rule, so the catalogue cannot become self-confirming.
      (This answers my own stated objection to B, and more cheaply than option C's filing pass.)
  K3. RETAIN ROW IDs AND PROVENANCE for every example.
Working pool after reservations: 133 - 24 (S-2.5) - 20 (Q-1) = **89 rows**, shared by S-1's
per-boundary reading and the catalogue. Sequence: reserve 44 -> S-1 reads 89 and rules per boundary
-> S-2 writes the spec + catalogue from those rows and rulings.

## NEW EVIDENCE 15 — condition K3 cannot be met today: NO ROW IDENTIFIER EXISTS

Checked all 7 corpus files (seed_intents, the 3 datasheets, collected_v4, train, test):
**ZERO id-like columns. Not one.** Row identity today IS the text.

Is text a usable key?
    seed_intents.csv                    903 rows / 903 unique texts   -> YES, text is a key
    normalized_dataset_m8a_uniform.csv 1100 / 1100                    -> YES
    collected_v4.csv / train.csv / test.csv                           -> YES
    normalized_dataset.csv             1365 / 1028, **337 dup rows**  -> NO
    normalized_dataset_m8a.csv         1365 / 1028, **337 dup rows**  -> NO
Whitespace+case normalisation introduces 0 collisions in the seed (903 -> 903).
=> Text keys the SHIPPED corpus (where 132 of the 133 contested rows live) but NOT the two
   historical datasheets, which is exactly where the disagreement evidence comes from.

BLOCKING: S-2.5 requires PRE-REGISTERING a 40-row reservation BEFORE S-1 reads anything. That is the
first executable act of this whole programme and it cannot name its rows.

GAP FOUND IN S-3's OWN SCOPING (flag, do not rule — S-3 territory): §S-3 lists DFD-5's 7 properties
and scopes "~5 new columns". **None of them is a row identifier.** As scoped, S-3 would add
provenance columns to rows that still have no stable identity.

### S-2.10 — row identity?  RULED: **Option C, ratify** (owner, 2026-08-28)
S-2 reservation + catalogue references key on **content hash + source file + line**.
S-3 introduces a stable surrogate **RowId**, retaining the originating hash **permanently**.
Owner refinements, and they change what the hash IS:
  H1. the hash is a **content/provenance LOCATOR for the reserved snapshot** — NOT the long-lived
      row identity. Long-lived identity is the S-3 RowId.
  H2. if source text is later corrected, **old hash references are PRESERVED**, not overwritten.
      => hash history / superseded-by chain, so a reservation record never silently points at
         nothing. This answers my stated objection to C (hash rot) directly.
  H3. Implied and recorded: the reservation event MATERIALISES A SNAPSHOT — the 40 rows with their
      hashes as of reservation time. "The reserved snapshot" is an artifact, not a query.

## NEW EVIDENCE 16 — `LabelVersion` provably fails to version the guideline

seed_intents.csv by (Source, LabelVersion):  m8a_uniform/v3 597 | synthetic_v3/v3 101 | collected_v4/v4 205
The 597 m8a_uniform rows carrying the SINGLE value `LabelVersion=v3` decompose as:
     340  pass-2 label CARRIED OVER unchanged
     136  NEW in uniform, no pass-1/2 ancestor (the untraceable set)
     121  pass-3 RELABELLED (label changed at pass 3)
Plus 101 synthetic_v3 rows also carrying `v3`.
=> **One version string, `v3`, spans FOUR distinct label origins across 698 rows.**
§3 S-2's design constraint 2 asserts this qualitatively ("versions the file, not the guideline").
It is now quantified: 340 / 136 / 121 / 101.

### S-2.11 — guideline versioning?  RULED: **Option A, ratify** (owner, 2026-08-28)
S-2 produces ONLY the versioned annotation specification and defines its version/bump semantics.
**S-3 owns the row-level `GuidelineVersion` field.**
The retrospective 903-row era map (EVIDENCE 16: 340 / 136 / 121 / 101) is **NOT embedded in S-2** —
it belongs with S-3 PROVENANCE EVIDENCE. Recorded here as an S-3 INPUT, not deleted.
This is the owner's third consecutive ruling drawing the same S-2/S-3 boundary (K1, H1, now A).

**BUMP RULE — ratified as proposed, plus one owner guardrail:**
  BUMP when: a class definition changes | a class-boundary rule changes | a Difficulty anchor changes
  NO BUMP for: typos, formatting, non-semantic example additions
  GUARDRAIL (owner): **an example addition that changes the EFFECTIVE CLASSIFICATION RULE is a
  semantic change and REQUIRES a bump.** Closes the loophole where spec behaviour drifts through
  examples while the version string stays fixed.
  Interaction to note: the S-2.9 catalogue IS examples, so catalogue growth can trigger bumps.

### S-2.12 — label provenance in the spec?  RULED: **Option B, ratify** (owner, 2026-08-28)
S-2 contains the semantic contract PLUS a concrete **annotation-record template** for the
reproducibility test and Q-1 — kept **explicitly as a WORKING ANNOTATION ARTIFACT**, not an S-3
storage/schema contract. S-3 remains free to design storage.
**AI-DRAFTING DISCLOSURE — CONFIRMED** (was agent-drafted rev-2 text, never ratified; now ratified):
the canonical spec must identify which sections were AI-drafted or AI-assisted, per DFD-8.
Owner qualification: this is strictly **provenance / transparency metadata — NOT a quality or trust
score.** An AI-drafted section is not thereby suspect; it is thereby labelled.

**S-2 BLOCK CLOSED.** Twelve decisions: S-2.1 B+C1-C4 / S-2.2 B / S-2.3 B / S-2.4 B+G1-G4 /
S-2.5 C / S-2.6 A+scoping / S-2.7 B+lock / S-2.8 5-way / S-2.9 B+K1-K3 / S-2.10 C+H1-H3 /
S-2.11 A+bump rule+guardrail / S-2.12 B+AI-disclosure.

## NEW EVIDENCE 17 — the seed is an EMBEDDED RESOURCE, and its BYTES gate model retraining

`SmartStudyPlanner.csproj:28`  <EmbeddedResource Include="Services\ML\TextClassifier\seed_intents.csv" />
  -> the governed corpus SHIPS INSIDE THE APPLICATION BINARY; trained at first run when no model
     exists on disk.

Parse contract (TextClassifierDatasetImporter.cs:35-54) — checked, and it is BETTER than feared:
  - column lookup is BY HEADER NAME, not by position;
  - RequiredColumns = InputText, TaskType, Difficulty, DeadlineHint (4 of 7). TaskName / Source /
    LabelVersion are OPTIONAL;
  - unknown extra columns are IGNORED.
  => ADDING provenance columns does NOT break the shipped parser. Renaming/removing the 4 required
     ones does.

BUT (TextClassifierModelManager.cs:73, 170, 227 + ModelMeta.SeedHash):
  `ComputeSeedHash()` = SHA-256 of the RAW EMBEDDED SEED BYTES. A mismatch against the cached
  model's stored SeedHash marks the cache STALE and forces a retrain.
  => Any byte change to seed_intents.csv — including a pure metadata column the model never reads —
     **invalidates every installation's cached model and forces a retrain on next launch.**
  => §S-3's "widening the schema is low-risk for the model path — the shipped classifier reads one
     of seven columns" is true about PARSING and silent about this. Data-governance metadata
     currently sits inside the model-cache staleness signal.

COLUMN-COUNT ARITHMETIC, corrected for the S-2 rulings:
  DFD-5 properties absent today: origin/collection event, provenance type, generator identity+version,
  label source, annotation-guideline version, licence metadata = **6**.
  `LabelVersion` does NOT satisfy "dataset version" — EVIDENCE 16 shows one value spanning four
  label origins. Plus S-2.10's **RowId** and the permanently-retained **originating hash** = 2 more.
  => ~**8** new columns, 7 -> ~**15**. §S-3's "~5 new columns (7 -> ~12)" is understated.

## DECISION S-3.1 — RULED (owner, 2026-08-28): Option B — governed corpus + generated export

**Ruling:** The governed corpus lives OUTSIDE the application. `seed_intents.csv` becomes a
GENERATED EXPORT that projects only the columns the shipped app requires.

Consequences recorded:
- The seed is formally what it already was in fact: a derived artifact. `datasheets/_merge_seed.py`
  produced it once, off the record. B makes the generation step a governed part of the pipeline.
- **Retrain decoupling.** Because the export carries only the app-required columns, provenance edits
  do not change the export's bytes, so `ComputeSeedHash()` (TextClassifierModelManager.cs:227) does
  not fire and NO retrain is forced on any installation. Governance metadata leaves the model-cache
  staleness signal. This was the decisive property (NEW EVIDENCE 17).
- Governance metadata no longer ships inside the application binary.
- **New obligation created:** corpus->export drift. A generated file that must be regenerated is a
  file that will one day be hand-edited instead. B is not free; it buys retrain-decoupling at the
  price of a consistency guarantee that must now be designed. QUEUED as an S-3 decision below.
- The ratified §S-3 exit criterion ("attempt to ingest a row with a missing field and confirm it is
  rejected") now lands on the corpus ingest path, not on the export.

## DECISION S-3.2 — PRESENTED 2026-08-30, AWAITING OWNER
Retrospective boundary: do the 903 existing seed rows enter the governed corpus with per-row
provenance, or as an exempt legacy block? Forced because the S-3 exit criterion is unsatisfiable
for rows whose provenance fields cannot be filled. Bears on S-2.11's ruling that the era map is
S-3 provenance evidence.

## DECISION S-3.2 — RULED (owner, 2026-08-30): Option B + derived/recorded marker MANDATORY ON EXPORT

**Ruling:** Backfill per-row provenance for the 903 existing rows wherever it is mechanically
derivable by join (340 pass-2-carried / 121 pass-3-relabelled / 101 synthetic_v3 / 205 collected_v4);
the 136 with no antecedent are marked **untraceable**. A second field distinguishes
**derived-by-join** from **recorded-at-creation**.

**Owner condition (binding):** the derived/recorded marker is **mandatory on export**. It may not be
dropped when provenance values leave the corpus. This closes the exact failure the agent argued
against its own recommendation: 802 inferred provenance values becoming indistinguishable from
recorded fact in a copy, with the failure silent.

Scope clarification recorded (NOT a change to the ruling):
- Under S-3.1=B the app-facing seed export projects only the app-required columns and therefore
  carries NO provenance at all — there is nothing there to mislabel.
- The condition binds every export that DOES carry provenance values: analysis extracts, training/
  test splits, dataset shares, any file handed to a reader or a downstream tool.
- Practical consequence: provenance columns travel as a PAIR. Any export path that emits a provenance
  value without its marker is non-conforming. This is a schema-level invariant, not a convention.

Pattern noted: this is the same shape as S-2.4's G4 ("composition travels with every quoted figure").
The owner is consistently ruling that a qualifier must travel with the datum it qualifies, rather
than living in surrounding prose. Worth stating once as a general principle in the rewritten proposal.

Retained consequences:
- The 121 pass-3 rows stay filterable — they carry zero Difficulty signal (EVIDENCE 6) and any future
  Difficulty work needs to exclude them. Option A would have left that only as prose in a datasheet.
- `LabelVersion` is confirmed unusable as the provenance field (EVIDENCE 16: one value, four origins).
- DFD-5's "no row without complete lineage" now has a legacy-compatible reading: complete means every
  field populated, where `untraceable` is a legitimate populated value carrying a `derived` marker —
  NOT a null. The S-3 exit criterion is therefore satisfiable against the 903.

## DECISION S-3.3 — PRESENTED 2026-08-30, AWAITING OWNER
The corpus's home and change-control model: how does a change to a Gold label get made, seen, and
approved? Bears on DFD-8 (owner is Gold authority) and determines what the ingest gate can be.

**Verified facts gathered for this decision (2026-08-30):**
- All seven corpus CSVs under `datasheets/` are TRACKED in git: `collected_v4.csv`,
  `normalized_dataset.csv`, `_m8a.csv`, `_m8a_balanced.csv`, `_m8a_uniform.csv`,
  `synthetic_v3_giuaky_doan.csv`, `vn_input_fixtures.csv`. Plus `_merge_seed.py`.
- Total ~610 KB. No `.gitattributes` (no diff driver, no LFS). No `.gitignore` entry excludes them.
- Existing review path is real: dev/main require branch + PR + CI green since 2026-08-09.
- Every existing reader in the repo is CSV-based (`_merge_seed.py`, ml-pilot `build_split.py`,
  `TextClassifierDatasetImporter`). Under S-3.1=B the export stays CSV regardless.

## NEW EVIDENCE 18 (2026-08-30) — the dependency graph is INVERTED from what S-3.3 implied

`[fact]` `tools/ml-pilot/split/build_split.py:12,37` reads
`SmartStudyPlanner/Services/ML/TextClassifier/seed_intents.csv` READ-ONLY as its source. It does
NOT read `datasheets/`. It writes `train.csv` / `test.csv` / `SPLIT.md`, and line 216 claims
re-running produces **byte-identical** output (deterministic generation already exists in this repo).
`[fact]` `datasheets/_merge_seed.py:11,93` WRITES `seed_intents.csv` in place (`open(SEED,"wb")`).
Its inputs were the seed itself plus `datasheets/collected_v4.csv`.
`[fact]` `tools/ml-pilot/build_fixtures.py` + `fixtures.py` use `datasheets/collected_v4.csv` and
`datasheets/vn_input_fixtures.csv` — a separate fixture lineage, not the corpus lineage.

`[inference]` Therefore **the app's embedded `seed_intents.csv` is TODAY's de facto authoritative
corpus.** `datasheets/*.csv` are historical inputs that were merged into it once and are now
upstream-of-record, not the record. The agent's S-3.3 framing implied `datasheets/` was the corpus;
that was wrong.

**COLLISION with S-3.2 (must be resolved):** under S-3.1=B the app seed becomes a generated export
carrying only app-required columns — i.e. NO provenance. `build_split.py` reads exactly that file.
So as currently wired, **every train/test split would be provenance-blind and structurally incapable
of carrying S-3.2's mandatory derived/recorded marker.** Either the ML consumers repoint at the
canonical corpus, or the S-3.2 condition is unsatisfiable on the split path. This is a required
ruling, queued as S-3.4.

**Weakens an agent counter-argument:** the "two formats" objection raised against S-3.3 option B is
softer than stated. A generation boundary is ALREADY ratified (S-3.1=B); making that generator also
change format is marginal cost on an accepted boundary, not a new categorical cost.

**Constraint that survives all S-3.3 options:** the export must be DETERMINISTIC / byte-stable, or
S-3.1=B's retrain-decoupling evaporates (a nondeterministic generator would churn `ComputeSeedHash()`
on every regeneration). `build_split.py` demonstrates the repo can already do this.

## DECISION S-3.4 — QUEUED (not yet presented)
Do the ML consumers (`build_split.py`, future training paths) read the CANONICAL corpus or the
machine EXPORT? Forced by NEW EVIDENCE 18. Bears directly on whether S-3.2's marker condition is
satisfiable on the split path.

## DECISION S-3.3 — RULED (owner, 2026-08-30): Option B + byte-stable export requirement + S-3 completion gate

**Ruling:** The canonical governance corpus is **JSONL**. `seed_intents.csv` is retained as a
**deterministic generated export**. The "two-format" cost is accepted explicitly, on the stated
ground that the canonical/export boundary was already ratified in S-3.1.

**Owner requirement (binding):** the export must be **byte-stable for unchanged canonical content.**
Not a note, a requirement. Rationale carried: a nondeterministic generator would churn
`ComputeSeedHash()` on every regeneration and destroy S-3.1=B's retrain-decoupling — the property
that motivated S-3.1 in the first place.

**Owner scoping (binding):** the newly discovered consumer path is **S-3.4, not part of S-3.3.**
Production/ML tooling must be repointed so that **no consumer bypasses the canonical corpus or loses
S-3 provenance.**

**Owner completion gate (binding):** **S-3 is NOT complete until the consumer boundary is resolved
AND verified.** Recorded as an exit condition on the stage, additional to §S-3's existing exit
criterion. Per [[feedback_verify_signal_can_fail]], "verified" here cannot mean a check that passes —
it must mean a check demonstrated capable of going red (introduce a violating read, watch it fail).

Note: the owner has ruled the PRINCIPLE for S-3.4 (no bypass, no provenance loss) but not the
MECHANISM. Options remain genuinely open — reading canonical directly and reading a provenance-
carrying derived artifact both satisfy the principle.

## NEW EVIDENCE 19 (2026-08-30) — complete consumer inventory for `seed_intents.csv`

`[fact]` Repo-wide `git grep` for `seed_intents`, plus a scan of C# CSV readers:

FIRST-ORDER consumers (read or write the seed itself):
1. `TextClassifierModelManager.cs:25` — embedded-resource read + SHA-256 hash + train. PRODUCTION RUNTIME.
2. `TextClassifierDatasetImporter.cs:29` — parses that stream. PRODUCTION RUNTIME.
3. `tools/ml-pilot/split/build_split.py:12,37` — read-only; emits train/test/SPLIT.md. RESEARCH.
4. `datasheets/_merge_seed.py:11,93` — **WRITES** the seed in place. One-off/historical, but a write path.
5. `tools/TextClassifierEval/Program.cs:104` — path-parameterised reader; docs show it invoked
   against the seed (`docs/reports/2026-06-25-...:35`). EVAL.

SECOND-ORDER (inherit whatever the split carries):
6. `S0Pilot/Accuracy.cs:32` -> `Split.Load("train.csv")`.
7. `tools/ml-pilot/split/vocab_gap.py:54-55` -> train.csv / test.csv.

SEPARATE LINEAGE — fixtures, NOT the corpus (do not conflate in S-3.4):
8. `build_fixtures.py` / `fixtures.py` -> `datasheets/vn_input_fixtures.csv` from `collected_v4.csv`.
9. `S0Pilot/Runtime.cs:44`, `Sanity.cs:20`, `TokCheck.cs:22` -> `vn_input_fixtures.csv`.

`[fact]` **A hash pin ALREADY EXISTS between a derived artifact and the seed.**
`tools/ml-pilot/split/SPLIT.md:5` names the seed as source, and the audit
(`2026-08-25-data-audit-gap-map.md:525,792`) records the pin `86abb454…` as MATCHING.
=> Two independent pins exist today: `ModelMeta.SeedHash` (runtime cache staleness) and SPLIT.md's
   pin (research reproducibility). **Both point at the export.** Under S-3.3=B, SPLIT.md's pin
   certifies the wrong artifact unless repointed at canonical — a required consequence of S-3.4.

## DECISION S-3.4 — PRESENTED 2026-08-30, AWAITING OWNER
Mechanism for the consumer boundary: do non-app consumers read the canonical JSONL directly, a
second provenance-carrying export, or the lean app export joined to canonical?

## DECISION S-3.4 — RULED (owner, 2026-08-30): Option A — one authority, one export

**Ruling:** Non-production consumers read the **canonical governance corpus directly**. The
production app **alone** may consume the generated lean CSV export. `seed_intents.csv` is a
**deployment artifact, not the source of truth.**

Binding conditions:
- Repoint `build_split.py`, `_merge_seed.py`, `TextClassifierEval`, and downstream research tooling.
- Preserve deterministic export.
- **Update BOTH existing hash pins so they verify the new canonical->export relationship.**
- **No sidecar joins. No second authoritative export.**

### Two consequences that need naming (flagged, NOT silently resolved)

**(a) `_merge_seed.py` is a WRITE path, so "repoint" is ambiguous.** Under A nothing but the
generator may write the export. Two readings, both consistent with the ruling:
  - RETIRE it — it is a one-off that already executed; its job is historically complete; or
  - CONVERT it into a canonical-corpus ingest path.
The INVARIANT is unambiguous either way: **only the generator writes the export.** The choice
between retire and convert is queued as a small confirmation, not assumed.

**(b) The two pins have DIFFERENT reachability — the instruction is satisfiable, by different means.**
  - `SPLIT.md`'s pin (research reproducibility): repoint to canonical. Fully satisfiable.
  - `ModelMeta.SeedHash` (runtime cache staleness): **the app never sees canonical at runtime** —
    canonical is not shipped. So the app cannot verify canonical->export. That link is verifiable at
    **build/CI time**; the runtime pin keeps doing its existing job on the export.
  - **HAZARD — a canonical stamp must NOT live inside the export bytes.** If the export embeds
    canonical's hash/version, then any canonical change (including a provenance-only edit) changes
    the export's bytes -> churns `ComputeSeedHash()` -> forces a retrain on every installation.
    That destroys S-3.1=B's retrain-decoupling and violates S-3.3's byte-stability requirement.
    => the canonical->export attestation belongs in a BUILD MANIFEST / CI record outside the export.
    Read as permitted: it is not a per-row sidecar join, and not a second authoritative export.
    FLAGGED for owner confirmation rather than assumed.

## DECISION S-3.5 — PRESENTED 2026-08-30, AWAITING OWNER
Shape of the row-level provenance schema, now that JSONL (S-3.3=B) makes non-flat structures
available. Note the proposal's "~5 new columns (7 -> ~12)" is measured-wrong regardless of shape.
Interacts with S-2.10's H2 (preserve superseded hash references) which already imposes a HISTORY
requirement, and with S-2.11 (guideline bumps mean labels change over time).

## DECISION S-3.5 — RULED (owner, 2026-08-30): Option B + FAIL-CLOSED validation

**Ruling:** Fixed core + nested `Provenance` object. Its required-key set MUST be **explicitly
versioned** and **mechanically enforced**. **Missing, null, or structurally invalid required
provenance REJECTS ingestion.** Presence of the `Provenance` object alone is NOT sufficient —
validation is structural and content-level, not existence.

H2's originating-hash / supersession history is **preserved**, but WITHOUT adopting full append-only
event sourcing. **The concrete history mechanism belongs to EXECUTION PLANNING, not to this
proposal.** (Consistent with the Proposal Integrity Rule: do not turn implementation choices into
policy.)

Note on how this ruling landed: the agent's counter-argument against B was that its required-key set
relies on ASSUMED discipline — the same thing the agent had rejected in S-3.4. The owner did not
override that objection, they **dissolved** it by making the discipline mechanical and fail-closed.
The objection is answered, not outvoted.

**Interlock with S-3.2 — coherent, worth stating in the rewrite:** S-3.2 established `untraceable`
as a LEGITIMATE POPULATED VALUE, not a null. S-3.5 rejects nulls. The two interlock exactly:
`untraceable` + a `derived` marker PASSES ingestion; a null FAILS. Unknown provenance is
recordable; absent provenance is not.

**VERSION PROLIFERATION — flag for the rewrite.** There are now three distinct version streams with
different semantics: (1) `GuidelineVersion` (S-2.11, annotation guideline); (2) dataset version
(DFD-5 property); (3) provenance-schema required-key-set version (S-3.5, new). Plus `LabelVersion`,
which EVIDENCE 16 disqualified. The S-2.11 lesson was precisely that one version string spanning
several meanings is a failure mode. These MUST be named unambiguously and never collapsed.

## NEW EVIDENCE 20 (2026-08-30) — the datasheet scope in §S-3 is wrong twice

`[fact]` `docs/reports/2026-08-25-data-audit-gap-map.md:58-85`: **21 data sources** bear on ML or
parser work, in four groups. **Group A = 10 items (A1-A10)** — "only group A ever reaches a trained
model."

`[fact]` **A datasheet ALREADY EXISTS.** B1 `datasheets/vn_input_fixtures.csv` + `.md` is recorded as
"**The only source in the repo with a datasheet.**" => §S-3's "8 files, 0 exist" is wrong on BOTH
counts: the governed set is Group A's 10 (+B1), and the count of existing datasheets is 1, not 0.
There is a precedent artifact and an implicit template.

`[fact]` **A8 is a training corpus with NO FILE.** `SeedDataGenerator.Generate()` — code, not a file;
180 rows; "**labels are RNG draws**"; "**Yes — M7 predictor, every run in practice**". A file-level
datasheet regime structurally CANNOT reach it.
  => IN-SCOPE QUESTION, not to be decided by the agent: A8 belongs to the M7 study-time predictor,
     not the text-classifier corpus that S-3.1..S-3.5 have all been about. Pulling it in unasked
     would be silent scope change; leaving it out leaves an RNG-labelled corpus trained on every run
     outside governance. Surfaced to the owner as a discriminator across the S-3.6 options.

`[fact]` Heavy containment among Group A: A2 is 100% inside A1 and A10; A9/A10 are filters of A1;
A3 is 100% inside A1, A4, A9; A7 is a strict subset of A6. A5 is "root of the in-repo lineage".
A4 carries "189 of unknown origin". => Per-file datasheets for derived subsets would largely restate
A1, and would duplicate audit content that already exists.

## DECISION S-3.6 — PRESENTED 2026-08-30, AWAITING OWNER
Which artifacts require an AUTHORED file-level datasheet, and which get a generated one — given that
the audit already holds most of the substance, one datasheet already exists (B1), and A8 has no file.

## DECISION S-3.6 — RULED (owner, 2026-08-30): Option C + A8 as a NAMED EXCLUSION

**Ruling:** Author file-level datasheets for the **canonical corpus and genuine source inputs only.**
Derived exports and splits carry **machine-generated provenance** from the canonical version +
generator manifest — NOT separate hand-written datasheets.

**A8 (`SeedDataGenerator.Generate()`) is EXPLICITLY OUT OF SCOPE for S-3**, on the stated ground that
it belongs to the separate **M7 study-time predictor** data domain. Recorded as a **deliberate,
reasoned exclusion**, not an omission. **NOT silently absorbed into this workstream.**
=> **FU-2 raised (separate governance follow-up, NOT scheduled here):** A8 is 180 rows whose
   "labels are RNG draws", trained in production on every run (audit Group A, A8). It needs its own
   governance decision in the M7 domain. This is a POINTER, not a task in Data Maturation.

Consistency note: C is the option that extends S-3.1..S-3.4's logic rather than interrupting it —
a derived artifact's provenance is "generated from canonical vX by generator vY", which a machine
states more reliably than a person, and which cannot drift from the thing it describes.

## DECISION S-3.7 — PRESENTED 2026-08-30, AWAITING OWNER
Where the fail-closed gate sits: at the WRITER (only a validating tool may write canonical) or at
the FILE (anything may write; a validator over the whole corpus is what actually blocks).
S-3.5 ruled validation is fail-closed and mechanical but did not locate it. There is NO ingest path
today — rows entered by hand-editing or by `_merge_seed.py`. Subsumes the queued `_merge_seed.py`
retire-vs-convert fork.

## DECISION S-3.7 — RULED (owner, 2026-08-30): Option B — gate at the canonical file / repository boundary

**Ruling:** The authoritative fail-closed gate sits at the **canonical file / repository boundary**.
A **complete validator must reject invalid canonical content in CI before it can enter repository
history.** Direct owner edits remain allowed under DFD-8 and need NOT pass through an ingest CLI.
A local/pre-commit validator MAY be added later as convenience; **CI remains the authoritative
enforcement point.** Transient invalid states in the working tree are ACCEPTABLE; invalid canonical
content must NEVER be accepted into committed history.

Consequence: `_merge_seed.py` is **RETIRED**, not converted. Under S-3.4 only the generator writes
the export, and under S-3.7=B no ingest CLI is required for canonical. Its function is historically
complete. (Closes the queued retire-vs-convert fork.)

### NEW EVIDENCE 21 — the CI gate is real, and its exact boundary matters

`[fact]` `.github/workflows/ci.yml` exists. Triggers: `push: [main, dev]`, `pull_request: [main, dev]`,
`workflow_dispatch`. One job `build-test`, windows-latest, .NET 10.
`[fact]` Combined with branch protection (dev/main = branch + PR + CI green since 2026-08-09), the
enforcement point the ruling names is ALREADY WIRED — the validator is a step to add, not
infrastructure to build.

**Precision on the guarantee (stated so the "verified" gate is not overclaimed):** CI gates the
MERGE into main/dev, not the commit. A commit pushed to a feature branch enters repository history
before any gate runs. So the ruling is exactly satisfiable as **"invalid canonical content never
enters main/dev history."** Also: the `pull_request` trigger fires only for PRs TARGETING main/dev —
a PR into a long-lived feature branch is not gated. Both are precision, not objection; the owner's
intent is met on the trunk lines that matter.

## S-3 BLOCK — CLOSED (7 decisions)
S-3.1 B (canonical + generated export) · S-3.2 B (+marker mandatory on export) ·
S-3.3 B (JSONL + byte-stable export + S-3 completion gate) · S-3.4 A (one authority, one export;
repoint all non-app consumers; both pins; no sidecar, no 2nd export) · S-3.5 B (+fail-closed,
versioned required-key set) · S-3.6 C (+A8 named exclusion -> FU-2) · S-3.7 B (gate at the
file/repo boundary; CI authoritative).

**ONE ITEM STILL OPEN before S-3 is fully closed:** the canonical->export attestation location.
Agent flagged in S-3.4 that the stamp must live in a BUILD MANIFEST OUTSIDE the export bytes, or a
provenance-only canonical change churns `ComputeSeedHash()` and forces a global retrain — destroying
S-3.1=B's whole purpose and violating S-3.3's byte-stability requirement. Read as permitted (not a
per-row sidecar join, not a second authoritative export). NOT yet confirmed by the owner.

## S-3 CLOSING CONFIRMATION — RULED (owner, 2026-08-30): external build manifest, RATIFIED

The canonical->export relationship is recorded **outside the export bytes**, in a build manifest.
It is an **attestation artifact** — not a runtime sidecar, not a second authoritative export.
Manifest records: **canonical hash · export/content hash · generator identity/version.**
`ComputeSeedHash()` continues to cover **only the model-relevant export content.**

**Derived constraint — record this, a naive generator will violate it:** because the export is lean
(app-required columns only), whole-file hash == model-relevant-content hash, so the ruling is
satisfied by the EXISTING implementation with no code change. That equality holds ONLY while the
export contains **no generation metadata whatsoever** — no "# generated <date>" header, no generator
version line, no timestamp. Any such line would re-couple provenance changes to
`ComputeSeedHash()` and force a global retrain on every regeneration, defeating S-3.1, S-3.3 and
this ruling at once. **The export must carry data and nothing else.** All generation metadata lives
in the manifest.

## ===== S-3 BLOCK FULLY CLOSED (7 decisions + 1 confirmation) =====

## S-4 — opening position (verified against the proposal text, 2026-08-30)

`[fact]` §S-4 (`docs/plans/2026-08-26-...:322-345`) as written:
  J-1 = **208 rows** · J-3 = **<=136 rows** (untraceable) · J-7 = `[unknown]` residue.
  "208 + <=136 is the measured Gold-A adjudication scope"; "the remaining ~188 are estimated from"
  Q-1's first 20.
  Exit: `gold_a_v1` — every row carrying adjudication record, ruling owner, guideline version,
  full S-3 lineage; separately named/versioned from Gold-R.

`[measured]` **These numbers are obsolete.** 208 was the full pass-1->2 disagreement set INCLUDING
rows whose labels were retired classes (`OnTap`/`NhacNho`). S-1.1 CONFIRMED retirement, removing 172.
**J-1 as originally defined is 36 production-relevant rows** (cross-only 14 + dual 22), on three
boundaries: BaiTapVeNha<->ThiCuoiKy 26 · KiemTraThuongXuyen<->ThiCuoiKy 8 ·
BaiTapVeNha<->KiemTraThuongXuyen 2. The **97** third-pass-only contested rows were NEVER in J-1.
Total distinct contested pool = **133**. §4.1's "208" and §4.2's "remaining ~188" are dead.

`[settled elsewhere]` J-3's PROVENANCE question is now closed by S-3.2: the 136 are marked
`untraceable` with a `derived` marker and are ingestible. What remains open in S-4 is the
**PROMOTION** question — may untraceable-provenance rows ever become Gold-A?

## DECISION S-4.1 — PRESENTED 2026-08-30, AWAITING OWNER
What is Gold-A v1's scope: the originally-defined disputes (36), the full contested pool (133), or a
designed sample that is not systematically hard-biased? Bears on EVIDENCE 8 — the contested pool
contains NO Difficulty level 1 and NO level 2 at all, so a Gold set built only from disputes is
unrepresentative by construction.

## DECISION S-4.1 — RULED (owner, 2026-08-30): Option C, AT SCOPE LEVEL ONLY

**Ruling:** Gold-A = the **full production-relevant contested pool** PLUS a **deliberately designed
representative authored sample** covering the defined TaskType/Difficulty space and ordinary cases,
especially the **currently absent low-Difficulty range**.

Binding conditions:
- Ratified at the SCOPE level, **not at a fixed row count.**
- The additional sample size and sampling rule must be **explicitly designed and approved BEFORE
  selection.** **Do not invent the number from the current data.** (Direct constraint on the agent.)
- The representative sample is **NOT a substitute** for the contested adjudication backlog.
  **Two components, two purposes, both required.**

Consequences recorded:
- **Third instance of pre-registration** as an owner requirement (after S-2.4/G1 composition and
  S-2.9/K2 example-selection). This is now a standing pattern, not three separate conditions —
  state it once as a principle in the rewrite: *selection rules are fixed before the data is seen.*
- **SEQUENCING DEPENDENCY:** the representative sample covers "the defined ... Difficulty space",
  and that space is defined by S-1.4's 1-5 written anchors. **The sample cannot be designed until
  S-1.4's anchors exist.** S-4's design work therefore sits behind S-1, not merely after it.
- **PART OF THE SAMPLE CANNOT BE SAMPLED — IT MUST BE WRITTEN.** EVIDENCE 9: seed Difficulty L1 = 2
  rows, BOTH synthetic; human-origin low-difficulty is effectively L1=0, L2=17. There is nothing in
  the corpus to draw L1 from. The owner's word "authored" is therefore load-bearing and correct: for
  the low range this is authoring, not sampling. The boundary between the two is set by where the
  corpus holds nothing.
- §S-4's "208 + <=136 is the measured Gold-A adjudication scope" is superseded in BOTH terms.

## DECISION S-4.2 — PRESENTED 2026-08-30, AWAITING OWNER
J-3 promotion: may the 136 undocumented-origin rows ever become Gold-A? §S-4 flags this as "a
separate judgement about acceptable provenance" and leaves it open. S-3.2 settled their INGESTION
(`untraceable` + `derived` marker); it did not settle their ELIGIBILITY.

## DECISION S-4.2 — RULED (owner, 2026-08-30): Option C, with the tier DERIVED not stored

**Ruling:** The 136 origin-unknown rows **remain eligible for Gold-A after owner adjudication**,
because **Gold-A certifies label correctness, not source realness.** Their origin-unknown status must
remain a **persistent and visible qualifier whenever Gold-A is summarized or cited.**

Binding conditions:
- **Do NOT duplicate provenance in a second schema field.**
- The existing **provenance object is the single source of truth**; the Gold-A
  eligibility/reporting classification **DERIVES from it**.
- Gold-A must never be presented without preserving that distinction **where it materially affects
  interpretation.**

Note on how it landed: the agent recommended A on the ground that a second tier duplicates S-3.2's
`untraceable` value. The owner did not override that — they **dissolved** it, keeping C's visibility
requirement while making the tier a DERIVED VIEW over the single stored fact. Second time in this
session an agent counter-argument became a design constraint rather than being outvoted
(first: S-3.5 fail-closed). The protocol is working as intended.

**STANDING PRINCIPLE #1 — "the qualifier travels with the datum."** Now ruled THREE times on
unrelated subjects: S-2.4/G4 (batch composition travels with every quoted figure) · S-3.2 (derived/
recorded marker mandatory on export) · S-4.2 (origin-unknown visible wherever Gold-A is cited).
State ONCE as a principle in the rewrite; stop re-deriving it per stage.
**STANDING PRINCIPLE #2 — "selection rules are fixed before the data is seen."** Ruled three times:
S-2.4/G1 · S-2.9/K2 · S-4.1.

## DECISION S-4.3 — PRESENTED 2026-08-30, AWAITING OWNER
AI assistance in the adjudication path (DFD-8). §S-4's AI-assist list is agent-drafted and never
ratified, AND it contains an internal inconsistency: the same section rejects an automated tiebreak
because it "encodes whichever pass the current model was trained on", then proposes AI pre-labelling,
which has the same defect in weaker form.

`[fact]` Audit A1: the production text classifier was trained on **all 903 rows, no split** —
including every contested row. The seed carries ONE label per row, so a pre-label on a contested row
is the model reciting its own training label, i.e. the status-quo winner of the disputed pass.

## DECISION S-4.3 — RULED (owner, 2026-08-30): Option B — commit-then-reveal

**Ruling:** AI predictions are **computed but HIDDEN until the owner commits the adjudication label.**
Only then may the AI result be revealed, as an **optional adversarial / re-review signal.**

Binding conditions:
- **AI is NOT an independent annotator here** — A1 was trained on the disputed rows themselves.
  Its output **must never be treated as validation or authority.**
- If the owner changes a label after seeing the revealed AI result, that is recorded as an
  **explicit SECOND REVIEW**, never a silent overwrite of the first ruling.

Consequences recorded:
- §S-4's AI-assist paragraph is **superseded**: pre-labelling as a starting point is rejected; the
  surviving legitimate uses are label-free (cluster by boundary, order rows).
- §S-4's claim that AI "reduces the work" is **materially weakened and must be restated** — under B
  the saving is ordering and clustering only; per-row adjudication cost is unchanged.
- **S-2.12's annotation-record template must carry three slots:** ruling_1 (committed) ·
  revealed_AI_result · ruling_2 (second review, if any) + reason. This is a concrete requirement
  landing on an artifact already ratified.
- Interacts with S-3.5's preserved history requirement (H2 supersession): a second review is a
  supersession event, not an edit.
- **DFD-8 instantiated a third time** (after S-2.2's "AI supplementary, never a substitute"):
  AI assists, AI never authorises. Candidate STANDING PRINCIPLE #3.

## DECISION S-4.4 — PRESENTED 2026-08-30, AWAITING OWNER
J-7 disposition: what happens to a row the owner CANNOT rule under the spec. §S-4 lists J-7 as
`[unknown]` until J-1 runs. Reframed by S-2.2 (owner is sole Gold authority, so "contested" cannot
mean two parties disagree) — J-7 is really "rows the spec fails to decide", i.e. GUIDELINE GAPS.
Q-1's ratified design already says to "record ambiguous cases; record guideline gaps discovered."

**Downstream cascade surfaced (NOT being asked yet, but visible so the owner rules with it in view):**
a guideline gap plausibly triggers an S-2.11 version bump; a bump raises whether rows already
adjudicated under v1 need re-review under v2. That is a separate decision and will be presented
after S-4.4.

## DECISION S-4.4 — RULED (owner, 2026-09-02): Option A — exclude and log

**Ruling.** A row the canonical guideline cannot decide is EXCLUDED from `gold_a_v1` and logged
explicitly as a guideline gap. Do NOT assign a confidence-qualified Gold label (B rejected). Do NOT
spend an additional human pass merely to force a v1 ruling (C rejected). Preserve the row's
**identity, reason, boundary, and guideline version** so it can be re-evaluated after a later spec
revision.

**Owner addendum, ruled in the same breath — the cascade is pre-empted.** A subsequent guideline
bump triggers re-review **only for rows whose applicable semantic rule actually changed**. The
S-2.11-bump / re-review question I had flagged as "coming after S-4.4" is therefore CLOSED by this
ruling and must NOT be presented as a separate decision.

**What the ruling establishes.**

1. Gold-A contains only rows the guideline actually decides. That is what makes Gold-A usable in S-6
   as the answer to *"is the model consistent with the label definitions"* — a set containing rows the
   definitions could not decide would not answer that question.
2. An undecidable row is reclassified from *bad label* to *evidence about the spec*. The gap log is a
   deliverable of S-4, not a residue of it.
3. Gold-A's size is not a success metric. S-4.1 already refused to fix a row count; S-4.4 refuses to
   inflate one.

**Consequences recorded.**

- **§S-4's J-7 line is superseded.** "Rows where adjudication is itself contested — `[unknown]` until
  J-1 runs" must be restated: under S-2.2 the owner is sole Gold authority, so J-7 is not two parties
  disagreeing. J-7 = **rows the spec fails to decide**, and its disposition is now ruled in advance
  rather than left `[unknown]`. The count stays `[unknown]`; the POLICY no longer is.
- **A guideline-gap log becomes an S-4 exit artifact.** §S-4's exit criteria currently name only
  `gold_a_v1`. They must also name the gap log, carrying per row: identity (S-2.9/K2 content hash +
  source + line, S-3 RowId once it exists), reason, boundary, guideline version.
- **The gap log feeds S-2's next version, not S-4's backlog.** It is an input to a spec revision, not
  a to-do list of rows to re-attempt. Re-attempting is gated on the rule changing (owner addendum).
- **Q-1's ratified design already collects this.** "Record ambiguous cases; record guideline gaps
  discovered" — Q-1 produces the first entries in this log before S-4 proper begins. No new
  instrument is needed; the log must simply be a durable artifact rather than a Q-1 side note.
- **New requirement created by the addendum, and it is not yet satisfiable.** "Only rows whose
  applicable semantic rule actually changed" presupposes the record knows WHICH rule decided each
  row. S-2.12's annotation template (three slots after S-4.3) does not currently carry a rule
  reference, and §S-4's exit criteria record only *the guideline version*. Version alone cannot
  answer "did the rule that decided THIS row change" — that is decision **S-4.5**.
- **Interlock with S-4.1.** Excluded rows come out of the contested-pool component. They must not be
  silently backfilled from the representative authored sample — S-4.1 ruled the two components serve
  different purposes, so a shortfall in one is not repairable from the other.
- **Standing principle #2 reinforced ("selection rules fixed before the data is seen").** Exclusion
  criteria are now fixed before adjudication begins, so "I could not decide it" cannot become a
  post-hoc escape from a row the owner simply dislikes — the gap log makes every exclusion visible
  and reasoned.

**Pattern confirmed, third instance — candidate standing principle #4: "when a bar is not met, revise
the bar's definition, do not lower the bar."** S-2.7 (reproducibility threshold missed => revise the
spec, not the threshold) · S-4.1 (do not invent the sample size from the data at hand) · S-4.4
(undecidable row => fix the spec, not the label).

## DECISION S-4.5 — RULED (owner, 2026-09-02): Option C — targeted rule attribution

**Ruling.** The adjudication record carries a rule citation ONLY where already-recorded structure does
not answer "which rule decided this row":
  - **Difficulty rulings** name the S-1.4 anchor applied. (The contested pool's contestation is on
    TaskType boundaries, so nothing otherwise records which anchor decided a row's Difficulty.)
  - **S-4.1 authored-sample rows** name the rule that placed them. (An authored row has an intended
    class, not a disputed boundary — no structure to inherit.)
  - **Contested-pool TaskType rulings** inherit their recorded boundary and add NOTHING. A
    class-boundary-rule bump resolves mechanically from the boundary already recorded.
A rejected (uniform citation on every row): redundant with the boundary, and post-hoc citation invites
rationalising a gestalt judgement into a rule that did not drive it — a false audit trail.
B rejected (version only): leaves Difficulty-anchor and class-definition bumps with no affected set.

**Fact base this ruling rests on (all previously ratified, none invented here).**
  - S-2.11's bump rule already defines the rule-unit space: class definition | class-boundary rule |
    Difficulty anchor. No new rule taxonomy was created.
  - S-2.9's catalogue is boundary-indexed and the contested pool is defined by its boundary.
  - S-1.6 issues one ruling per production-relevant boundary, so boundaries are individually
    addressable.

**Consequences recorded.**

- **S-4.4's addendum is now operable across all three bump types.** class-boundary-rule bump ->
  affected set = rows recorded at that boundary. Difficulty-anchor bump -> rows citing that anchor.
  class-definition bump -> rows citing that rule (authored sample) plus rows at boundaries touching
  the class. Without S-4.5 the second and third had no mechanical answer.
- **S-2.12's annotation-record template now carries FOUR slots**, not three: ruling_1 (committed) ·
  revealed_AI_result · ruling_2 (second review, if any) + reason · **rule citation, where S-4.5
  requires it.** S-2.12 remains a WORKING ANNOTATION ARTIFACT; storage stays S-3's per the
  S-2/S-3 boundary the owner has now drawn four consecutive times.
- **The rewrite must state BOTH attribution mechanisms explicitly.** This is the cost the owner
  accepted: "which rule decided this row" is answered by inherited-boundary for one component and by
  explicit citation for the other. A reader who does not know which mechanism applies to which
  component will misread the set. This is a documentation obligation, not an optional clarification.
- **Conditional, flagged before the ruling and still live:** if the citation is made a REQUIRED field,
  S-3.5's fail-closed regime rejects an adjudication missing it at ingestion rather than flagging it.
  Whether it is required is an S-3 schema decision, not settled here.
- **Second-order payoff (available, not committed):** the distribution of Difficulty-anchor citations
  shows which anchors carry the adjudication load — direct evidence for the next S-2 version and for
  S-2.9's catalogue. Not a deliverable unless later scoped.

## DECISION S-4.6 — RULED (owner, 2026-09-02): Option B — pinned manifest, fail-closed

**Ruling.** `gold_a_v1` is fixed as a **pinned manifest** containing the selected row identities, the
canonical corpus hash, and the guideline version. Rows are resolved from the canonical corpus at
computation time. **The manifest MUST FAIL CLOSED if its pinned corpus state cannot be resolved.**
Row content is NOT duplicated into a second authoritative artifact.
A rejected (frozen snapshot file): duplicates row content, re-creating the drift condition S-3.4
eliminated. C rejected (living derived view): a figure cited today could not be recomputed tomorrow —
the exact defect S-6 records the correction pass undoing (96.2%, 97.24%/97.25%, the S0 comparison).

**The owner's addendum DISSOLVED my stated counter-argument rather than overriding it.** I argued B's
weakness is a silently dangling pin. Fail-closed resolution converts that silence into a loud failure,
so the objection no longer applies. (Third time this session: S-3.5 fail-closed ingestion,
S-4.2 derived tier, now S-4.6 fail-closed resolution.)

**CANDIDATE STANDING PRINCIPLE #5 — "an unresolvable state is an error, never a default."**
Now instantiated THREE times by owner ruling: S-3.5 (missing/null/invalid provenance REJECTS
ingestion) · S-3.7 (invalid canonical content REJECTED by CI before entering history) · S-4.6
(unresolvable pinned corpus state FAILS the manifest). State once in the rewrite; stop re-deriving it.

**Consequences recorded.**

- **§S-4's exit criteria must be restated as a manifest specification**, not a dataset description.
  What `gold_a_v1` IS: row identities (S-2.10/H-scheme content hash + source + line, S-3 RowId once it
  exists) + canonical corpus hash + guideline version. What it is NOT: a file of rows.
- **The S-3 build-manifest pattern now has a second instance.** S-3's closing confirmation ratified an
  external manifest recording canonical hash · export hash · generator identity/version. S-4.6's is
  the same shape for a different purpose. The rewrite should present them as ONE mechanism with two
  instances, not two inventions.
- **A Gold-A figure is only citable with its manifest.** Standing principle #1 ("the qualifier travels
  with the datum") applies: quoting a Gold-A number without the manifest identity reproduces the
  un-pinned-basis defect in a new place.
- **Dependency: S-4.6 cannot execute before S-3 is complete**, because the pin is a canonical corpus
  hash and S-3.3 ruled S-3 incomplete until the consumer boundary is resolved AND verified.
- **Deliberately NOT decided here:** what Gold-A may be USED for (held-out evaluation vs training) is
  S-6's, per its ratified structural rules.

---

## S-4 BLOCK CLOSED — 2026-09-02. Six decisions.

S-4.1 C (scope level only) / S-4.2 C (tier derived) / S-4.3 B (commit-then-reveal) /
S-4.4 A (exclude + gap log; bump re-review scoped) / S-4.5 C (targeted rule attribution) /
S-4.6 B (pinned manifest, fail-closed).

**Verified against the §S-4 text: every element is now ruled or superseded.** J-1 volume "208 rows"
-> superseded by S-4.1 (scope level; 208 is a dead number, the production-relevant contested pool is
133). J-3 "<=136, decided then applied" -> ruled by S-4.2. J-7 -> ruled by S-4.4. "208 + <=136 is the
measured Gold-A adjudication scope" -> superseded by S-4.1 (contested pool + designed authored
sample). "the remaining ~188 are estimated from that" -> dead number, and S-2.6's scoping condition
already limits Q-1 to contested adjudication time only. The AI-assist paragraph -> superseded by
S-4.3. Exit criteria -> extended by S-4.2/4.3/4.4/4.5 and restated as a manifest by S-4.6.

**NOT open S-4 decisions, correctly deferred by prior ruling — do not re-present:**
  - the authored sample's size and sampling rule (S-4.1: designed and approved before selection;
    depends on S-1.4's anchors being frozen first);
  - J-7's COUNT (S-4.4 ruled the policy; the count stays `[unknown]` until adjudication runs).

# ===== S-5 BLOCK (Gold-R) OPENED 2026-09-02 =====

## DECISION S-5.1 — RULED (owner, 2026-09-02): Option A — explicit per-participant consent at transfer

**Ruling.** Track B rows may continue to accrue LOCALLY, but **no row may leave the user's machine or
enter `gold_r_v1` without explicit per-participant consent for that transfer**, with the consent
record/version preserved alongside the contributed data. Rationale given by the owner: keeps the
governance standard consistent with Track A and prevents reproducing the project's historical
provenance problem with real user data.
B rejected (notice + opt-out): default-on collection from real people makes the dataset's basis
contestable. C rejected (defer): the residual §4.2 says will not collapse on its own.

**Code facts established before the ruling (verified this session, not assumed).**
  - `[fact]` NO outbound network transport exists in the application — no `HttpClient` anywhere in
    `SmartStudyPlanner/`. `Data/SyncSchema.cs` is D-I LAN device-to-device sync metadata, NOT
    telemetry upload.
  - `[fact]` `DifficultyLabelLog` / `StudyTimeOutcomeLog` are LOCAL SQLite tables
    (`Infrastructure/Persistence/SQLite/Repositories/`). Write paths exist in `FocusViewModel` and
    `QuanLyTaskViewModel`.
  - `[unknown]` whether any user's database currently holds rows in those tables.
  - Consequence: the question was never "may the app collect" (it already does, locally, as ordinary
    app function) but "on what basis does a row LEAVE a machine". The transfer channel does not exist,
    so this policy is being set BEFORE the mechanism is built, not retrofitted onto one.

**Consequences recorded.**

- **§S-5's "This track can start earliest and costs the least" is now FALSE and must be corrected in
  the rewrite.** Under S-5.1, Track B carries a consent step and a transfer mechanism that does not
  exist. It is no longer the cheap track. QUEUED DOCUMENTATION FIX.
- **NEW SCOPE, named not absorbed (Proposal Integrity: do not silently change scope).** Track B now
  has a build dependency the proposal does not contain: a consent-and-transfer mechanism. §S-5 says
  Track B's "readiness gates are DFD-9b's"; there is now an ADDITIONAL gate. Flagged to the owner as a
  named dependency, NOT quietly designed.
- **MY COUNTER-ARGUMENT WAS WEAKER THAN I PRESENTED IT — correction.** I argued A might make Track B
  not worth running, since its whole justification is yielding the production rather than a recruited
  distribution. That overstates it. Consent-at-transfer affects **who contributes**, not **how the
  input was produced**. Track A ELICITS inputs (produced for the study); Track B collects inputs
  produced in ordinary use and asks permission to share them. The elicitation artifact — the single
  largest threat to Track A's validity — does NOT appear in Track B even under S-5.1. What remains is
  non-response bias, which is real but strictly smaller. **Track B retains a genuine epistemic
  advantage over Track A.** The rewrite must state this distinction precisely; the sloppy version
  ("Track B = production distribution") is now wrong, and the correct version is still favourable.
- **Symmetric claim-scope requirement.** §S-5 already requires Track A carry participant count and
  recruitment route wherever a distribution figure appears. Track B now needs its analogue: the
  **contributed-subset qualifier** — how many users contributed, out of how many were asked. Without
  it, a Track B distribution figure silently claims to be the usage distribution when it is the
  distribution of the consenting subset.
- **The consent record joins the provenance regime.** It travels with the rows and therefore falls
  under S-3.5's versioned required-key set and fail-closed validation. This is standing principle #1
  ("the qualifier travels with the datum") — FOURTH instance, after S-2.4/G4, S-3.2, S-4.2.
- **One consent regime now covers both tracks**, which keeps them comparable in claim scope without
  violating DFD-4's rule that the two datasets never merge.

## DECISION S-5.2 — RULED (owner, 2026-09-02): Option C — retroactive, tier-capped

**Ruling.** Pre-mechanism rows MAY be contributed with **explicitly derived/backfilled provenance**,
but they:
  - must remain **below the top maturity tier** (Q-5 ladder), and
  - are **ineligible for the S-6 held-out evaluation partition**.
Rows accrued AFTER the consent/provenance mechanism ships may qualify for the higher tier.
**All Gold-R reporting must distinguish these provenance grades explicitly.**
A rejected (unrestricted retroactive): would put inferred lineage under the held-out set. B rejected
(prospective only): discards the scarcest thing the project has — real rows — for a purity C achieves
where it matters.

**Evidence the decision rested on (verified, not assumed).**
  - `[fact]` `Data/SyncSchema.cs` verbatim: "an alpha-tester's pre-Epic-1 database needs its five
    ISyncMetadata columns patched onto each of the six synced tables idempotently" — the code
    anticipates REAL pre-existing user databases.
  - `[unknown]` how many such databases exist and how many rows they hold. The volume is measurable
    before execution; it was NOT measured here and no figure was invented.

**Consequences recorded.**

- **§5's tier ladder gains a HARD INVARIANT, not a threshold.** Q-5 ruled the ladder must distinguish
  hard invariants (binary, set) from quality thresholds (unset until evidence). S-5.2 makes
  **"provenance recorded at creation" a hard invariant of the top tier.** It is binary and it is set
  now. §5 must be updated to carry it, on the invariant side of the line.
- **§S-6's structural rules gain a THIRD exclusion.** Currently: "No synthetic row and no
  `collected_v4` row may enter it (DFD-6, DFD-1)." Add: **no derived-provenance Gold-R row.** QUEUED
  DOCUMENTATION FIX — this is an addition to a ratified rule list, so it must appear there, not only
  in S-5.
- **The grade is DERIVED, not stored — S-4.2's precedent applied.** The provenance grade follows
  mechanically from S-3.2's `derived` vs `recorded-at-creation` marker, which is already mandatory and
  already fail-closed under S-3.5. NO second schema field. Agent applied this by precedent rather than
  asking; flagged to the owner as applied-by-precedent, reversible on request.
- **Standing principle #1, FIFTH instance** ("the qualifier travels with the datum"): S-2.4/G4 ·
  S-3.2 · S-4.2 · S-5.1 (contributed-subset qualifier) · S-5.2 (provenance grade in all Gold-R
  reporting). Five instances across four stages — state ONCE in the rewrite as a governing principle,
  then cite it, rather than re-deriving it per stage.
- **Second non-uniformity cost accepted this session.** S-4.5 accepted two attribution mechanisms
  inside Gold-A; S-5.2 accepts two provenance grades inside Gold-R. Both are reader-facing. The
  rewrite must carry a single explicit "how to read a figure from this project" statement covering
  both, or the obligations will be honoured inconsistently.

## DECISION S-5.3 — RULED (owner, 2026-09-02): Option C — one corpus, physically partitioned by role

**Ruling.** ONE governed canonical corpus and ONE authority, with rows **physically partitioned by
role** so the training/export path has no access to held-out Gold-R rows. Owner conditions:
  - the partition is an **ENFORCEMENT BOUNDARY, not a second authority**;
  - **provenance, track and tier remain governed METADATA** (derived, per S-4.2 / S-5.2);
  - **any role move must be EXPLICIT and HASH-VISIBLE.**
A rejected (predicate guard): `_merge_seed.py` is standing proof a hurried script bypasses an intended
boundary. B rejected (separate corpora): DFD-4 forbids the two tracks merging, so B implies THREE
governed corpora each needing validator + manifest + CI gate, plus the second authority S-3.4 ruled
against in its own domain.

**Role is the ONE physical attribute; everything else stays derived.** This is a deliberate, bounded
exception to the derived-not-stored pattern, not an abandonment of it.

**Principle #5 satisfied STRUCTURALLY rather than by predicate** — there is no guard to bypass because
the export path cannot see the rows. Fourth instance of the owner preferring a state that cannot be
wrong over one that is checked for wrongness (S-3.7, S-4.6, S-5.2, S-5.3).

---

### COLLISION SURFACED BY S-5.3 — must be ruled before partitions can be laid out

**C forces an S-6 question forward.** You cannot lay out role partitions without knowing which roles
exist and where Gold-A's rows sit. Facts:
  - `[fact]` audit A1: the production classifier trains on ALL 903 seed rows, no split — including
    every row Gold-A will adjudicate.
  - S-4.6: Gold-A is a MANIFEST (a selection), so Gold-A membership itself needs no physical move.
  - S-6 (ratified): "Held-out data is reserved BEFORE training merge, never carved out afterwards."
  - S-3.1: `seed_intents.csv` is a PROJECTION of canonical -> anything in the training partition
    reaches the shipped model.
**Therefore:** if Gold-A rows are held-out, they must physically LEAVE the training partition, the
export shrinks, and the shipped model changes. If they are not, any S-6 figure computed on Gold-A is
measured on rows the model trained on. This was deferred to S-6 by the agent's own scope note at
S-4.6; S-5.3 makes it STRUCTURAL and it must be pulled forward. -> queued as **S-5.4**.

### DERIVED CONSTRAINT — S-4.6 manifest granularity (queued, flagged, NOT ruled)

S-4.6 pins "the canonical corpus hash" and fails closed when the pinned state cannot be resolved.
Under S-5.3 a role move changes corpus bytes. **A naive whole-corpus hash would make every existing
manifest fail closed on every unrelated role move** — spurious loud failures are how a fail-closed
guard gets disabled in practice, which would defeat principle #5 by social means rather than technical.
S-2.10's ratified content-hash row addressing already offers the fix: resolution can verify each
pinned row by its own content hash, making the manifest immune to unrelated churn. **NOT ruled here —
flagged to the owner.** Borderline implementation detail, but the FAILURE SEMANTICS is policy.

## DECISION S-5.4 — RULED (owner, 2026-09-02): Option B — Gold-A stays in training

**Ruling.** Gold-A rows remain in the **training partition and export**. Gold-A is a
**reference/adjudication corpus, NOT a held-out performance set.** S-6 performance figures must use a
**separately reserved holdout established before training.** §S-6's wording must be CORRECTED so it
does not imply Gold-A itself provides held-out model-performance evidence.

**AGENT ERROR IN THE OPTION FRAMING — corrected, surfaced to the owner.** I listed "no model change,
no retrain, no accuracy loss" as a benefit of B. The first two are FALSE. Adjudication corrects labels
on rows that remain in the export; export bytes change; `ComputeSeedHash()` (SHA-256 over the raw
embedded resource bytes) differs; a global retrain follows — under B exactly as under A. B avoids the
SHRINKAGE, not the retrain. Assessment given to the owner: this does not flip the comparison (the
retrain becomes a wash between A and B while A's loss of boundary rows stands), and the owner was
offered the chance to revisit. S-3.1 spared the project provenance churn, never content change.

**Consequences recorded.**

- **QUEUED DOCUMENTATION FIX, owner-ordered:** §S-6's "Gold-A answers *is the model consistent with
  the label definitions*" is FALSE as written under S-5.4 and must be restated. Gold-A's surviving
  purposes from §S-4 — annotation-regression detection and validation of the S-2 guideline — both work
  on trained-on rows and are unaffected.
- **S-5.3's partition layout is now determined.** Gold-A needs NO evaluation partition: it is a
  manifest (S-4.6) over rows in the TRAINING partition. The role partitions therefore reduce to
  training vs held-out, with held-out fed by Gold-R only. The question S-5.3 forced forward is closed.
- **S-6's holdout source is Gold-R, and S-5.2 already constrains it:** derived-provenance Gold-R rows
  are ineligible. Do NOT conflate this holdout with S-2.3/S-2.5's pre-reserved 20-row batch, which
  serves the reproducibility test and is a different reservation.
- **DFD-4 separation is maintained by DERIVATION, not by partition.** S-5.3 ruled track stays governed
  metadata, so one held-out partition carries both tracks with track as a derived field. No physical
  split by track; DFD-4's "never merge into one dataset" is honoured at the dataset-identity level.
- **Standing item, still unruled:** S-4.6 manifest granularity (row-level content hash vs whole-corpus
  hash). Remains queued and flagged; the owner has it.

## DECISION S-5.5 — RULED (owner, 2026-09-02): Option A — owner labels Gold-R under the S-2 spec

**Ruling.** Gold-R labels are **owner-assigned under the S-2 specification**. The user's
`FinalDoKho` / `WasOverride` signals are **preserved as governed metadata and NEVER treated as the
Gold label.** Rationale given by the owner: keeps Gold-A and Gold-R on the same spec-defined
construct while preserving the real-user signal for a separate future analysis of perceived
difficulty.
B rejected (user override as label): severe selection bias — override rows are where the model
failed, so a holdout built from them is adversarial, not representative. C rejected (co-equal
labels): doubles unpriced owner work and creates a further ruling rather than settling one.

**Evidence base (verified this session).**
  - `[fact]` `Models/Telemetry/DifficultyLabelLog.cs`: `InputText`, `TaskType`, `SuggestedDoKho`,
    `FinalDoKho`, `WasOverride`, `Source`, `MaTask`, `CreatedUtc`.
  - `[fact]` `ViewModels/QuanLyTaskViewModel.cs:338-342`: `WasOverride = finalDoKho != suggested`,
    `Source = "manual"`.
  - `[inference, strong]` `WasOverride == false` rows are NOT evidence — `FinalDoKho` there IS the
    model's own `SuggestedDoKho`, passively accepted. Same self-confirmation defect S-4.3 ruled
    against.
  - `[fact]` the override signal exists for **Difficulty only**; nothing records user-vs-model
    disagreement on `TaskType`.
  - **Construct-validity framing that decided it:** S-1.4's Difficulty is a SPEC-GOVERNED construct;
    `FinalDoKho` is PERCEIVED PERSONAL difficulty. Different constructs. Merging them silently would
    repeat the founding error — a number produced by one process read as a measurement of another.

**Consequences recorded.**

- **APPLIED BY PRECEDENT, flagged, reversible on request — S-4.3's commit-then-reveal now binds the
  Gold-R labelling view.** The model's prediction is not a separate AI call here: `SuggestedDoKho` is
  a FIELD IN THE ROW the owner is labelling. An unmodified labelling view therefore anchors the owner
  automatically, by data format. The Gold-R labelling view MUST withhold `SuggestedDoKho`,
  `FinalDoKho` and `WasOverride` until the owner commits, then may reveal them. This extends S-4.3
  from "AI predictions" to "model output embedded in the row"; the anchoring rationale applies more
  strongly here, not less.
- **Gold-R inherits Gold-A's record machinery.** Annotation record (S-2.12, four slots) and S-4.5's
  rule citation both apply. Under S-4.5's logic Gold-R rows resemble the authored sample — no
  inherited contested boundary — so they require explicit citation rather than inheritance.
- **NEW UNPRICED HUMAN COST — surfaced, not absorbed.** Q-1's measurement is scoped (S-2.6) to
  CONTESTED Gold-A adjudication only. S-5.5 makes every collected Gold-R row require an owner label,
  so **Gold-R labelling effort scales linearly with collection volume**, which Q-2 explicitly declines
  to assume. This is plausibly the BINDING CONSTRAINT on Track A/B volume, and the proposal's cost
  model does not contain it. Flagged to the owner. Not priced here; no figure invented.
- **FU-3 (new, unscheduled):** perceived-difficulty analysis — owner spec label vs user `FinalDoKho`
  on the same rows. Named by the owner's own rationale as a separate future study. Recorded so it is
  not silently absorbed into Gold-R's scope.

## DECISION S-5.6 — RULED (owner, 2026-09-02): Option B + explicit dependency pinning
### (closes the S-4.6 manifest-granularity item queued at S-5.3)

**Ruling.** A manifest **fails closed when a pinned row is missing or its content no longer matches.**
Unrelated corpus churn must NOT invalidate it. **Owner addition:** any figure dependency beyond its
named rows — a corpus-wide denominator, a comparison set — **must itself be explicitly pinned in the
manifest**, so row-level verification does not silently leave contextual dependencies unpinned.
A rejected (whole-corpus pin): under S-5.3 role moves are routine and hash-visible BY DESIGN, so
fail-closed would fire constantly on manifests whose own rows are untouched — a guard that cries wolf
gets bypassed, defeating principle #5 socially rather than technically. C rejected (partition-scoped):
couples manifest validity to a layout choice rather than to the data the figure used.

**THE OWNER'S ADDITION DISSOLVED MY COUNTER-ARGUMENT — fourth time this session.** I raised B's blind
spot (a denominator outside the named rows changing silently) and grounded it in the project's own
demonstrated failure: §4.1's 29.6% denominator is 703, not 1028 — already queued as a documentation
fix. Explicit dependency pinning removes the blind spot structurally rather than mitigating it.
(Prior three: S-3.5 fail-closed validation, S-4.2 derived tier, S-4.6 fail-closed resolution.)

**Consequences recorded.**

- **The manifest is redefined: a DEPENDENCY DECLARATION, not a row list.** S-4.6's specification is
  extended — row identities + canonical corpus hash (recorded, not gating) + guideline version +
  **every contextual dependency the figure rests on.** The rewrite must present it this way.
- **This converts a documentation fix into a structural prevention.** The 29.6%/703-vs-1028 class of
  error becomes unrepresentable for any manifest-backed figure: the denominator is either pinned or
  the figure is not manifest-backed.
- **Corollary, and it is principle #5 again:** a figure whose dependency CANNOT be pinned cannot be
  published as a manifest-backed figure. There is no "pinned except for" state.
- **Standing principle #1, SIXTH instance** ("the qualifier travels with the datum"): S-2.4/G4 ·
  S-3.2 · S-4.2 · S-5.1 · S-5.2 · S-5.6 (a figure's dependencies travel with the figure). Six
  instances across four stages. This is the single most-instantiated principle of the session.

## DECISION S-5.7 — RULED (owner, 2026-09-02): Option B + three conditions

**Ruling.** A **pre-registered assignment rule**, fixed BEFORE observing row content, assigns Gold-R
rows to held-out vs training; the **split fraction is instantiated after the initial volume is known.**
Owner conditions:
  1. **Neither arrival order NOR post-hoc class balancing may determine holdout membership.**
  2. **Q-4's per-class floor is a READINESS / COVERAGE criterion, not the allocation rule.**
  3. **If volume is insufficient, mark Gold-R evaluation NOT YET READY** rather than forcing an
     undersized split.
A rejected (all recorded-provenance rows held out). C rejected (floor-first, surplus to training) —
its allocation depends on arrival order, which condition 1 forbids outright.

**This does NOT reopen Q-4.** Q-4 ratified the floor as the shape of COLLECTION sampling. S-5.7
clarifies that it never governed ALLOCATION. Both readings stand together.

**Condition 1 is a direct guard against re-creating `_balanced.csv`.** §S-5 already warns that a floor
treated as a quota "has re-created `_balanced.csv`'s 1.11x balancing against an unobserved target."
S-5.7 forbids the same error one level down, in allocation rather than collection.

**Consequences recorded.**

- **"Not yet ready" is now a legitimate, nameable state for Gold-R evaluation** — and refusing to
  produce an undersized artifact is standing principle #4 ("when a bar is not met, revise or refuse;
  do not lower the bar"), FOURTH instance: S-2.7 · S-4.1 · S-4.4 · S-5.7.
- **Binds cleanly to §5's tier ladder.** Q-5's ladder already contains an "evaluation-ready" tier.
  The per-class floor is now explicitly that tier's COVERAGE criterion. It stays on the THRESHOLD side
  of Q-5's invariant/threshold line (it is a number, deliberately unset), but its ROLE is now fixed.
- **The rule/number split follows S-4.1's shape exactly**: fix the rule now, instantiate the number
  after observation. This reconciles principle #2 (selection rules fixed before data is seen) with
  Q-4's bar on inventing quotas before a distribution exists — they only appeared to conflict.
- **Provenance inversion, recorded as accepted:** the weakest-provenance real rows (derived, per
  S-5.2) go training-side; the strongest go to evaluation. Correct practice — evaluation integrity is
  where provenance matters most — but it means the training-side gaps §S-5 names (`tgk` at 0/698
  training) can only be fixed by pre-mechanism rows, whose volume is `[unknown]`.

---

## S-5 BLOCK CLOSED — 2026-09-02. Seven decisions.

S-5.1 A (per-participant consent at transfer; Q-3 residual RESOLVED) / S-5.2 C (retroactive,
tier-capped, S-6-ineligible) / S-5.3 C (one corpus, role-partitioned enforcement boundary) /
S-5.4 B (Gold-A stays in training; reference corpus, not a perf holdout) / S-5.5 A (owner labels under
the S-2 spec; user signal = metadata) / S-5.6 B+dependency pinning (closed the S-4.6 residue) /
S-5.7 B+3 conditions (pre-registered allocation; floor = readiness not allocation; "not yet ready" is
a legitimate outcome).

**Verified against the §S-5 text: every element is ruled, ratified, or deliberately deferred.**
Track A feasibility (Q-2/Q-3) ratified · class-coverage target ratified · Q-4 hybrid shape ratified
with the floor NUMBER deferred and its ROLE now fixed by S-5.7 · convenience-sample bound already in
the document · sample size / recruitment / privacy explicitly the owner's and outside the proposal ·
Track B consent (S-5.1) and pre-mechanism eligibility (S-5.2) · DFD-4 separation maintained by
DERIVATION per S-5.3 · exit criteria fully covered: gold_r_v1 (S-5.3), owner-verified (S-5.5),
consent records (S-5.1), S-3 lineage (S-5.3), held-out reserved before training merge (S-5.7),
recorded claim scope (document + S-5.1's contributed-subset qualifier).

**NOT S-5 decisions — correctly deferred, do not re-present:** Q-4's floor NUMBER and stopping rule ·
Track A sample size, recruitment mechanics, privacy handling (owner's, outside the proposal) ·
the S-5.7 split FRACTION (instantiated after first observation by ratified rule).

### S-T STRAND — UNADDRESSED, flagged
S-T (telemetry readiness, DFD-9b) is explicitly outside the S-1..S-8 chain and was NOT in the owner's
ratified decision ordering. It now carries an ADDITIONAL gate from S-5.1 (the consent-and-transfer
mechanism). It has had NO decision pass this session. Flagged to the owner; not silently skipped.

# ===== S-6 BLOCK (Evaluation foundation) OPENED 2026-09-02 =====

## DECISION S-6.1 — RULED (owner, 2026-09-02): Option B + headline discipline

**Ruling.** Pre-register, **before Gold-R exists**, the full metric set AND the single headline metric:
  - **TaskType:** exact-match.
  - **Difficulty:** exact-match, within-one, and MAE.
  - **A chance baseline for each applicable metric, computed from the OBSERVED Gold-R marginal.**
  - **Report all registered metrics**, but only the **pre-registered headline** is the primary figure;
    the others are **mandatory diagnostics, NOT post-hoc alternatives.**
A rejected (exact only): treats Difficulty as nominal, contradicting the ordinal construct S-1.4
ratified. C rejected (MAE only): drops metrics that were worth registering.

**"Mandatory diagnostics, not post-hoc alternatives" is the load-bearing phrase.** It removes the
failure mode that made B risky — a dashboard from which a reader picks the flattering number. The
diagnostics are compulsory to publish and forbidden to promote.

**Rule/number split — THIRD instance of the same shape.** The baseline FORMULA is pre-registered; the
baseline VALUE is computed from data. Same structure as S-4.1 (sample rule now, size later) and S-5.7
(allocation rule now, fraction later). Consistent with principle #2 without freezing an unknowable
number. Record once in the rewrite as a general pattern.

**Chance-baseline arithmetic already available (contested-pool marginal, illustrative only).**
Marginal 3:49 / 4:70 / 5:14 (n=133) -> p3=.368, p4=.526, p5=.105.
  - exact-match chance = sum p_i^2 = **.424**
  - within-one chance = **.921**
  - MAE chance (E|i-j| under independence) = **0.654 levels**
`[caveat]` This is the CONTESTED-POOL marginal. Gold-R's marginal is `[unknown]` and may differ. The
figures illustrate WHY the baseline must be computed rather than assumed; they are not predictions.

**Consequences recorded.**

- **§S-6's "Gold-A answers *is the model consistent with the label definitions*" stays queued for
  correction** (S-5.4). Under S-5.4 Gold-A is not a performance set at all, so S-6.1's metrics apply
  to **Gold-R only**. The rewrite must not let S-6.1's metric set imply a Gold-A performance figure.
- **§S-6's "Gold-A and Gold-R ... averaging them destroys the only distinction that matters" must also
  be restated.** Post-S-5.4 the two are not two performance sets to keep separate; only one of them
  produces performance figures at all. The rule survives in spirit, but its stated reason is now wrong.
- **Immediate residual, and it is live NOW:** the ruling commits to naming a headline "before Gold-R
  exists" but does not name it. TaskType has only one registered metric, so its headline is
  determined. **Difficulty's headline is open -> S-6.2.**

## DECISION S-6.2 — RULED (owner, 2026-09-02): Option B — MAE is Difficulty's headline

**Ruling.** Difficulty's headline is **mean absolute error** — the registered metric most faithful to
the ordinal construct, retaining full error-distance information. **Exact-match remains a mandatory
diagnostic. Within-one is diagnostic ONLY**, because its chance baseline can be near-ceiling under a
clustered marginal. **The numeric chance baseline must be recomputed from the ACTUAL Gold-R marginal
when available.**

**Decided on discrimination, which is the owner's own ratified standard applied to a metric.**
Headroom above chance under the contested-pool marginal: exact-match 58 points · within-one **8
points** · MAE the full continuous range. A measure that cannot separate a good model from a bad one
is a broken instrument.

**Consequences recorded.**

- **The two headlines are NOT commensurable.** TaskType reports a proportion; Difficulty reports a
  distance. §S-6's rule against averaging now has a second, sharper application at the METRIC level:
  these two numbers can never be combined into an overall score. State it explicitly in the rewrite.
- **Legibility obligation.** MAE has no intuitive pass/fail reading and Q-5 forbids a threshold, so
  the chance baseline MUST travel with it every time — principle #1 again.
- **"Re-baselining" a historical figure will NOT be like-for-like.** The downgraded figures
  (96.2%, 97.24%/97.25%, the S0 comparison) were accuracy-style numbers on authored data. Under S-6.1
  the Difficulty headline is MAE. So §S-6's "re-baselined against it" means *a new figure under the new
  registry*, not the same metric recomputed. Record this so nobody presents an old number and a new
  number as a before/after.
- **THIRD reader-facing non-uniformity accepted this session** (after S-4.5 dual attribution and
  S-5.2 two provenance grades). The "how to read a figure from this project" statement is hereby
  ESCALATED from suggestion to **REQUIRED ARTIFACT of the rewrite** — three obligations honoured
  inconsistently would be worse than none.

## DECISION S-6.3 — RULED (owner, 2026-09-02): Option C + one-sided-guard clause

**Ruling.** An **interval is required on every figure.** A result whose interval **includes the
pre-registered chance baseline** is labelled **"not distinguishable from chance."** Owner conditions:
  - **Exclusion of chance is NOT validation and NOT a precision guarantee.** It is a
    **negative-evidence guard, one-sided.**
  - **Interval WIDTH remains an explicit uncertainty qualifier**, carried regardless.
  - **No numerical precision threshold before Q-5 permits one.**
A rejected (intervals only): nothing stops a point estimate being quoted without its interval —
this project's demonstrated failure mode. B rejected (blocking width gate): needs a number Q-5
forbids, and the rule/number split cannot rescue it — a width instantiated after the first
observation cannot gate that first observation.

**THE OWNER'S CONDITION DISSOLVED MY COUNTER-ARGUMENT — FIFTH time this session.** I argued C's bar is
weak in the opposite direction and that someone would read exclusion-of-chance as validation. Making
the label explicitly one-sided, and keeping width as a mandatory qualifier, forbids that reading by
rule rather than hoping against it. (Prior four: S-3.5, S-4.2, S-4.6, S-5.6.)

**Consequences recorded.**

- **Readiness now has two distinct axes, and only one of them gates.** COVERAGE gates (Q-4 floor, via
  S-5.7 -> "not yet ready"). PRECISION does not gate; it labels and qualifies. The rewrite must not
  blur them into a single "quality" notion.
- **Interval machinery per metric:** Wilson for proportions (TaskType exact, Difficulty exact,
  within-one), bootstrap for MAE. Method is execution detail; the REQUIREMENT is policy.
- **S-6.1's chance baselines are now load-bearing twice** — once as reported context, once as the
  trigger for the not-distinguishable label. They cannot be dropped as a nicety.

---

## S-6 BLOCK CLOSED — 2026-09-02. Three decisions.

S-6.1 B (full pre-registered metric set + headline discipline) / S-6.2 B (MAE headline for
Difficulty) / S-6.3 C (mandatory intervals + one-sided not-distinguishable-from-chance guard).

**Verified against the §S-6 text: every element is ruled, ratified, inherited, or queued.**
"Held-out reserved before training merge" -> S-5.7, and structurally guaranteed by S-5.3 (the
held-out partition never reaches the export, so "a set no model has seen" holds by construction, not
by discipline) · exclusions -> ratified + S-5.2's third · "reservation recorded" -> S-5.7's
pre-registered rule + S-5.6's manifest · "the first real-input figure" -> S-6.1/6.2/6.3 ·
Gold-A/Gold-R separation -> survives, but its STATED REASON is wrong post-S-5.4 and is queued for
correction · historical figures "re-baselined or scoped as authored-only" -> ratified, with S-6.2's
not-like-for-like caveat recorded.

## S-7 — CONTROLLED EXPANSION (Silver / public / synthetic)

### S-7.1 — Silver promotability to Gold-A  [RULED: C]
**Owner ruling (verbatim):** "C. Do not apply a blanket Silver promotability rule. Rule Gold-A
eligibility per source and record it in the source datasheet under S-3.6, with owner adjudication
still required and provenance/methodology qualifiers preserved in reporting. S-4.2 means provenance
alone is not a Gold-A gate; it does not make every Silver source automatically promotable."

**Context.** SS-7 as written states `collected_v4` and the 136 untraceable rows "land here [Silver]:
usable for training, **never promotable to Gold**." That clause directly contradicts ratified S-4.2,
which held the 136 remain Gold-A eligible after owner adjudication because Gold-A certifies label
correctness, not source realness. Left unresolved, the position was also inverted: origin-UNKNOWN
rows promotable, origin-KNOWN-AI rows (`collected_v4`, P-1) not.

**What is now ruled.**
1. SS-7's blanket "never promotable to Gold" clause is **superseded**. It is not replaced by a
   blanket "always promotable" clause either.
2. Gold-A eligibility is a **per-source ruling**, recorded in that source's **S-3.6 datasheet**.
   Promotability is therefore a governed property OF A SOURCE, not a property of the Silver tier.
3. Owner adjudication remains **required** in every case. A per-source eligibility ruling grants
   only the right to be adjudicated; it never confers a Gold label.
4. Provenance and methodology qualifiers are **preserved in reporting** for every promoted row.
5. **Scope limit on S-4.2 (owner's own words):** S-4.2 establishes that provenance alone is not a
   Gold-A *gate*. It does NOT establish that every Silver source is automatically promotable.
   S-4.2 is a statement about what does not disqualify, not a grant of eligibility.

**Consequences.**
- Silver remains a real tier. Its definition changes from "never promotable" to "**promotability is
  not a property of this tier; consult the source datasheet**."
- Each existing Silver source needs an explicit eligibility ruling before any adjudication of its
  rows: `collected_v4` (205), the 136 untraceable, `synthetic_v3`, and any future synthetic batch.
  The 136 already have theirs by S-4.2 (eligible). `collected_v4`'s is **unruled and now required**.
- S-3.6's datasheet schema gains a mandatory field: **Gold-A eligibility ruling** (eligible /
  not eligible / unruled), with the ruling's date and rationale. `unruled` blocks adjudication.
- Reader cost accepted knowingly: Silver's meaning must be read from N datasheets, not one
  definition. Mitigated because S-3.6 already requires a per-source governance record, so the
  ruling travels with the data (principle #1, seventh instantiation).

**Standing principle #1 — "the qualifier travels with the datum" — 7th instantiation**
(S-2.4/G4, S-3.2, S-4.2, S-5.1, S-5.2, S-5.6, S-7.1).

**Queued rewrite fixes from this decision.**
- SS-7 Silver paragraph: delete "never promotable to Gold"; replace with the per-source rule.
- SS-3.6 datasheet spec: add the mandatory Gold-A eligibility field.
- SS-4 / S-5: state that eligibility grants adjudication, never a label.
- Add explicitly: "S-4.2 removed provenance as a disqualifier; it did not create a presumption of
  eligibility." This blocks the misreading in both directions.

### S-7.2 — Authorisation for synthetic generation  [RULED: A]
**Owner ruling (verbatim):** "S-7.2: A. Suspend creation of new synthetic batches during Data
Maturation. Existing synthetic rows remain in Silver and are untouched. Synthetic generation may
resume only after an observed-data gap and an explicit warrant are available; this stage does not
authorize synthetic generation for distributional or coverage claims."

**Context.** SS-7's synthetic paragraph lists only ENTRY CONTROLS (generator provenance, spec
conformance, distribution checks) -- which answer "does this batch pass?", never "should this batch
exist?". The trap block removes the only warrant anyone would invoke ("Q-4 permits targeting coverage
gaps ... by *collecting*, and this stage does not inherit that permission") and the safeguard that
would restore it is unsatisfiable (five linguistic phenomena `[unknown]`). The section therefore
permitted a technique, forbade its obvious purpose, and named no other. That hole is what A closes.

**What is now ruled.**
1. **Creation of new synthetic batches is SUSPENDED for the duration of Data Maturation.** Not
   restricted, not conditioned -- suspended. There is no permitted purpose during this stage.
2. Existing synthetic rows (`synthetic_v3`, the 136, `collected_v4`) **remain in Silver and are
   untouched**. This ruling is prospective only; it neither removes nor re-grades existing rows.
3. **Resumption requires BOTH, conjunctively:** (a) an **observed-data gap** -- a gap measured
   against observed data, not against the authored corpus; and (b) an **explicit warrant** for the
   batch. Either alone is insufficient.
4. **This stage does not authorise synthetic generation for distributional or coverage claims** --
   stated as a standing prohibition, so it survives the stage that suspended it.

**Note on how this was reached.** The agent recommended B (non-distributional purposes only),
conditional on writing a bright line between "robustness variant" and "coverage gap fill" in the same
ruling, and stated that if that line could not be written in one sentence, A was the more honest
option. The line was not written. A follows from the agent's own condition; it is not an override.

**Consequences.**
- `[inference]` The project's live coverage problem (94.6% of test lines contain a token absent from
  training) is **not addressable by any synthetic option**, since producing absent tokens asserts what
  students write. Only Tracks A/B can close it. Suspension therefore costs little capability.
- SS-7's exit criterion -- the distribution check "proven capable of failing" against `collected_v4` --
  now has **no synthetic batch to gate during this stage**. It does not disappear: it becomes the
  precondition that must exist BEFORE resumption, and it still governs any non-synthetic addition.
  Flag for S-7 closing: whether S-7 retains any live work during Data Maturation at all.
- The cost model must drop any synthetic-generation line item from the Data Maturation stage.

**Standing principle #4 -- "when a bar is not met, revise or refuse; do not lower the bar" -- 5th
instantiation** (S-2.7, S-4.1, S-4.4, S-5.7, S-7.2). Here the bar was a measurement that does not
exist, and the ruling refused rather than substituting a marker for the measurement.

**Queued rewrite fixes from this decision.**
- SS-7 synthetic paragraph: the permission is suspended for this stage; state the two-part resumption
  condition and the standing prohibition on distributional/coverage warrants.
- SS-7 exit criteria: restate the distribution check as a **resumption precondition**, not a
  per-batch filter applied during this stage.
- SS-7 must distinguish entry controls from warrants explicitly -- the original text conflated them,
  which is how the hole formed.

### S-7.3 — ViLexNorm / OD-4 licensing review  [RULED: A]
**Owner ruling (verbatim):** "A. Schedule OD-4 now as an owner licensing-review task, with completion
required before any ViLexNorm use. The review should answer the stated NC/SA and derivative-use
questions; scheduling does not authorize read-only measurement or ingestion, and ViLexNorm does not
satisfy S-7.2's requirement for an observed real-population gap."

**Context.** SS-7 states the OD-4 route is "a decision to schedule, not a blocker to work around."
It had never been scheduled, which is how such an item becomes the blocker it was named to avoid.

**What is now ruled.**
1. **OD-4 is scheduled** as an owner licensing-review task, recorded with its question.
2. **Completion is a hard gate on ANY ViLexNorm use** -- no tier, no purpose, no exception.
3. The review's question: does non-redistributive measurement use create a derivative under **`SA`**,
   and does **`NC`** bind a non-distributed internal artifact? (Agent flagged `SA` as plausibly the
   sharper hazard than the `NC` clause SS-7 names, since share-alike on a derivative could pull the
   governed corpus under `CC BY-NC-SA 4.0`. `[inference]`, not legal advice.)
4. **Scheduling authorises nothing.** Explicitly not read-only measurement, not ingestion. The
   option-C shape -- permit the narrow use now, review later -- is closed.
5. **Ratified agent correction:** ViLexNorm does **not** satisfy S-7.2's resumption condition. As an
   instrument it measures teencode in OUR corpus, which is authored/AI-generated, so it characterises
   the authoring process -- the exact confusion the trap block warns against. As external evidence its
   population is social-media writers, not students entering study tasks. S-7.2 requires an observed
   gap in the RELEVANT population; neither route supplies one.

**Consequences.**
- UIT-VSFC ("no licence field on the dataset card") and PhoATIS ("no licence surfaced") remain
  unusable on the same rule: `public/downloadable != relevant != licensed != approved for project use`.
  With ViLexNorm gated behind OD-4, **all three public candidates are now closed** during this stage.
- The rewrite must record OD-4 as a named blocking task with its question, not as a caveat in prose --
  a caveat is what let it go unscheduled for the whole life of the document.

**Queued rewrite fixes from this decision.**
- SS-7 public track: state that the OD-4 gate is scheduled and blocking, and that scheduling conferred
  no permission.
- SS-7 ViLexNorm row: add the explicit non-satisfaction of S-7.2 so the instrument route cannot later
  be read as the missing measurement.
- SS-4.2 residual / open-decisions table: add OD-4 as an owner task with no Data Maturation deadline.

### S-7.4 — S-7's remaining scope + when the distribution check is built  [RULED: B]
**Owner ruling (verbatim):** "B. Keep S-7 as a governance-only stage during Data Maturation and
build/freeze the distribution check now. Prove it fails on collected_v4's known regularities, but
specify the check over general phenomenon/feature families rather than hard-coding those seven
patterns. Future changes must be versioned rather than tuned against a pending batch."

**Context.** S-7.1/7.2/7.3 emptied the stage: no row enters the corpus via S-7 during Data Maturation.
The open question was whether S-7 leaves the stage entirely, stays dormant, or stays as governance
work -- and specifically whether the distribution check is built now or at resumption.

**What is now ruled.**
1. **S-7 remains in Data Maturation as a GOVERNANCE-ONLY stage.** No ingestion, no generation.
   Its work is per-source eligibility rulings (S-7.1) and the check below.
2. **The distribution check is built AND FROZEN now**, while no batch is pending.
3. **Proof obligation:** it must be demonstrated to FAIL on `collected_v4`'s known regularities.
4. **Specification constraint (dissolves the agent's counter-argument):** the check is specified over
   **general phenomenon / feature families**, NOT hard-coded to the seven observed patterns. The seven
   are one instantiation used as the proof case, never the definition.
5. **Change control:** future changes are **versioned**, never tuned against a pending batch.

**How the counter-argument was dissolved (6th occurrence this session).** The agent objected that
proving the check catches `collected_v4` proves it catches YESTERDAY's defect, and that a check
specified with no future adversary in view could be calibrated to the one known failure and blind to
the next. That objection bites only if the check IS the seven patterns. Constraint 4 makes the seven
a test case rather than the specification, so the proof demonstrates a family detector fires, not that
seven patterns match themselves. Constraint 5 closes the remaining hole: the check can still be
wrong, but correcting it is a visible versioned act rather than a silent recalibration by whoever
wants the next batch through.
(Prior occurrences: S-3.5, S-4.2, S-4.6, S-5.6, S-6.3.)

**Standing principle #2 -- "selection rules are fixed before the data is seen" -- 6th instantiation**
(S-2.4/G1, S-2.9/K2, S-4.1, S-5.7, S-6.1, S-7.4), now with a **corollary recorded for the first
time: when a fixed rule must later change, version it visibly rather than tune it silently.** This is
the same mechanic S-2 uses for guideline versions and S-4.4 uses for guideline bumps; the rewrite
should state it once as a general control rather than three times locally.

**Consequences.**
- Data Maturation's stage list stays S-1..S-7, but S-7's cost model contains **governance and one
  engineering deliverable (the check)** -- zero ingestion, zero generation.
- The check is a **frozen artifact with a version**, so it needs an identity in the manifest regime
  (S-4.6/S-5.6): any figure or gate that depends on it must pin its version.
- "Specify over feature families" is a design constraint the rewrite must carry into the check's spec,
  because it is the only thing preventing the proof from being circular.

**Queued rewrite fixes from this decision.**
- SS-7 header: describe the stage as governance-only for Data Maturation, with tracks gated/suspended.
- SS-7 exit criteria: split into (a) the frozen, versioned check with its family-level specification
  and its `collected_v4` proof case, and (b) the per-row criteria that apply only on resumption.
- Add the versioning corollary to the standing-principles section.

**=== S-7 BLOCK CLOSED -- four decisions (S-7.1 C, S-7.2 A, S-7.3 A, S-7.4 B) ===**
Every SS-7 element is now ruled or explicitly gated: Silver promotability (per-source, S-7.1),
synthetic authorisation (suspended, S-7.2), public candidates (all three closed; OD-4 scheduled and
blocking, S-7.3), stage scope and the distribution check (governance-only, built and frozen now,
S-7.4).

## S-8 — FUTURE MODEL WORK

### S-8.1 — Pre-registration of the encoder-revival bar  [RULED: C]
**Owner ruling (verbatim):** "C. Pre-register the necessary but not sufficient revival preconditions
now: real Gold-R holdout, S-6 baseline with required intervals, a specific pre-result hypothesis
identifying the deficit the encoder is expected to address, and DAT-04 unchanged. The hypothesis must
state the observed deficit, proposed mechanism, evaluation metric/contrast, and predicted direction
before results are seen; changing it later requires a new version and does not retroactively qualify
the prior experiment. No numerical revival threshold is set yet, and a separate owner decision remains
mandatory."

**Context.** SS-8 is a placeholder naming a bar without saying what meets it. Left undefined until S-6
produces numbers, the bar would be written by whoever is holding those numbers -- post-hoc criterion
selection, the defect ruled against at S-5.7, S-6.1, S-6.2 and S-7.4. The agent did NOT propose ruling
S-8 substantively (that would be inventing policy); the question put was procedural only.

**What is now ruled -- four NECESSARY, NOT SUFFICIENT preconditions.**
1. A **real Gold-R holdout** exists (per S-5.7 allocation, S-5.3 partition).
2. An **S-6 baseline measured with the required intervals** (S-6.1 metric set, S-6.2 MAE headline,
   S-6.3 intervals + chance-baseline labelling).
3. A **registered pre-result hypothesis**, structured as FOUR mandatory elements:
   - the **observed deficit** (what is actually wrong, observed not assumed),
   - the **proposed mechanism** (why an encoder would address that deficit),
   - the **evaluation metric / contrast** (what is compared against what),
   - the **predicted direction** (which way the result should move if the mechanism holds).
   All four stated **before results are seen**.
4. **DAT-04 unchanged** -- dataset growth alone does not authorise re-running the experiment.

**Change control on the hypothesis.** Amending it requires a **new version**, and a later version
**does not retroactively qualify the prior experiment**. This is the S-7.4 versioning corollary applied
a second time, and it is the clause that makes element 3 auditable.

**Explicitly NOT set.** No numerical revival threshold. This respects S-6.3's "do not introduce a
numerical precision threshold before Q-5 permits one" without needing to resolve whether that
prohibition reaches revival thresholds as well as reported figures -- the ambiguity is left open rather
than settled by implication.

**A separate owner decision remains mandatory** even when all four preconditions are met. The
preconditions gate the question; they never answer it.

**How the counter-argument was dissolved (7th occurrence).** The agent objected that three of the four
items are mechanical, so C risks becoming "satisfy the checkboxes, declare the bar met", with all the
substance resting on an unauditable hypothesis. The ruling answers this by giving element 3 a required
STRUCTURE (deficit / mechanism / metric-contrast / direction) and a versioning rule -- a hypothesis
with a predicted direction registered before results can be WRONG, which is what makes it a real bar
rather than paperwork.
(Prior occurrences: S-3.5, S-4.2, S-4.6, S-5.6, S-6.3, S-7.4.)

**Standing pattern -- rule/number split -- 4th instantiation** (S-4.1 sample rule vs size, S-5.7
allocation rule vs fraction, S-6.1 baseline formula vs value, S-8.1 revival preconditions vs
threshold).

**Agent SUGGESTION, not policy, for the rewrite:** the four-element hypothesis schema is not
encoder-specific. It reads as a general pre-registration template for any future experiment in this
project. Recommend stating it once as a reusable schema and having S-8 reference it. Owner has NOT
ruled on generalising it; flagged only.

**Queued rewrite fixes from this decision.**
- SS-8: replace the bare placeholder with the four preconditions, the hypothesis schema, the
  versioning clause, the explicit non-setting of a numerical threshold, and the mandatory separate
  owner decision.
- SS-8's "a revival would face a higher bar than S0 did" now has a concrete referent -- keep the
  sentence but attach the preconditions to it.

**=== S-8 BLOCK CLOSED -- one decision (S-8.1 C) ===**

**=== RATIFIED DECISION CHAIN S-1 .. S-8 COMPLETE ===**
S-1 (6) . S-2 (12) . S-3 (7+closing) . S-4 (6) . S-5 (7) . S-6 (3) . S-7 (4) . S-8 (1).
Remaining before the proposal rewrite: (a) S-T has had NO decision pass -- flagged repeatedly, sits
outside the ratified ordering, and now carries S-5.1's added consent-and-transfer gate; (b) the
consolidated decision record.

## SCOPE RULING — S-T runs before consolidation  [RULED: A]
**Owner ruling (verbatim):** "Scope decision: A -- run the S-T decision pass before consolidation.
S-5.1 establishes the consent rule for transfer but does not by itself close S-T's separate gate, so
the interaction must be ruled explicitly rather than inferred during the rewrite. Keep any
implementation-dependent details as later follow-ups or amendments; do not defer the governance
decision itself."

**Sequence fixed:** S-T decision pass -> consolidated record covering S-1..S-8 + S-T -> single rewrite.

**Two directives carried forward.**
1. **S-5.1 does NOT by itself close S-T's gate.** The interaction is to be ruled explicitly. Inferring
   it during the rewrite is prohibited by name.
2. **Implementation-dependent details become follow-ups or amendments; the governance decision itself
   is never deferred on implementation grounds.** This dissolves the agent's counter (8th occurrence:
   S-3.5, S-4.2, S-4.6, S-5.6, S-6.3, S-7.4, S-8.1, scope). It is also a reusable rule -- "S-3 is not
   implemented yet" is not grounds to postpone a governance ruling.

## NEW EVIDENCE 22 — the telemetry tables have NO egress path at all  `[fact]`
Verified this session by two independent checks:
- **No network transport.** `grep -rn "new HttpClient|HttpClient "` over `SmartStudyPlanner/` returns
  nothing. The application makes no outbound HTTP calls.
- **Not LAN-synced.** `SmartStudyPlanner/Data/SyncSchema.cs:23-25` enumerates the D-I sync set as
  exactly six tables: `HocKys, MonHocs, StudyTasks, StudyLogs, TaskNotes, TaskReferenceLinks`.
  `ISyncMetadata` is implemented by exactly those six models (`SmartStudyPlanner/Models/`).
  **`DifficultyLabelLogs` and `StudyTimeOutcomeLogs` are in neither list.**

**Therefore:** telemetry rows are strictly local-device-resident. They do not leave via network, and
they do not leave via device-to-device LAN sync. There is no egress mechanism to consent to today.

**Bearing on S-T.** SS-T's consent gate is worded as "a consent basis for collecting from users of a
shipped application". The evidence splits that into two questions the document does not separate:
COLLECTION (local accrual, retention, handling -- live today) and EGRESS (transfer off-device -- no
mechanism exists). S-5.1 governs the second only, by its own terms ("no row may leave the user's
machine ... without explicit per-participant consent for that transfer").

**Already-governed adjacent case.** Retroactive contribution of rows accrued BEFORE any consent
mechanism ships is governed by **S-5.2**: derived/backfilled provenance, tier-capped, ineligible for
the S-6 held-out partition, provenance grade distinguished in all reporting.

## S-T — TELEMETRY READINESS (DFD-9b), strand outside the S-1..S-8 chain

### S-T.1 — What closes S-T's consent gate  [RULED: C]
**Owner ruling (verbatim):** "C. Split S-T into two explicitly independent gates: a local
collection/retention/handling gate that can close now when its policy and controls are satisfied, and
an egress/transfer gate governed by S-5.1 that is N/A while no transfer mechanism exists and becomes
blocking when one is introduced. S-T itself must never be reported as "closed" merely because the
local stage closed; both states remain explicitly visible"

**Context.** SS-T words the gate as "a consent basis for collecting from users of a shipped
application (**open**)", fusing two questions. NEW EVIDENCE 22 separates them: collection is live
today; egress has no mechanism at all. S-5.1 governs egress only, by its own terms.

**What is now ruled — TWO EXPLICITLY INDEPENDENT GATES.**
1. **Local collection / retention / handling gate.** Covers accrual on the user's own device,
   retention duration, handling, and disposal. **Closable now** once its policy and controls are
   satisfied. This is the gate A-style egress-only reasoning would have left unaddressed.
2. **Egress / transfer gate**, governed by **S-5.1**. Its state is **`N/A` while no transfer mechanism
   exists**, and it **becomes BLOCKING the moment one is introduced**. Note the state is `N/A`, not
   `closed` and not `satisfied` -- it cannot be mistaken for a met condition.
3. **S-T must NEVER be reported as "closed" on the strength of the local stage alone.** Both gate
   states remain **explicitly visible** in any status report on this strand.

**How the counter-argument was dissolved (9th occurrence).** The agent objected that a two-stage gate
manufactures a fresh instance of SS-T's own named silent-failure mode ("Declaring the strand done
because the columns now populate ... the one most likely to be mistaken for the whole"). The ruling
answers it structurally: the gates are *explicitly independent* rather than sequential stages of one
gate, the egress gate carries a distinct `N/A` state that reads as unmet, and aggregate "closed"
reporting for S-T is prohibited outright.
(Prior: S-3.5, S-4.2, S-4.6, S-5.6, S-6.3, S-7.4, S-8.1, scope, S-T.1.)

**Consequences.**
- Any S-T status surface must render **two states**, never one. A single roll-up field for this strand
  is now non-conforming.
- Building a transfer mechanism is a **gate-state transition**, not a feature ship: it flips the egress
  gate from `N/A` to `BLOCKING`, which must be visible in the same place.
- S-5.2 continues to govern rows accrued before a consent mechanism exists (derived provenance,
  tier-capped, S-6-ineligible) -- unchanged by this ruling.

**Queued rewrite fixes from this decision.**
- SS-T "Gates, in dependency order": split gate 2 into the two independent gates with their state
  vocabularies (`closable now` / `N/A` -> `BLOCKING`).
- SS-T exit criteria: state that satisfying the local gate does not close the strand.
- SS-T silent-failure block: extend it -- the failure mode now has a second, closely-related form
  (reporting S-T closed on the local gate), and the ruling's structure is the stated defence.
- SS4.2 Q-3 row: SS-T's gate is no longer simply "open"; record the two-state form.

### S-T.2 — Provenance at write time (gate 1)  [RULED: A]
**Owner ruling (verbatim):** "A. Use a capture-complete write-time rule: record every S-3 lineage
field that is meaningful for a runtime-captured row rather than predicting which fields may be needed
later. The fields must still be governed by Gate 4 validation/invariants so an unpopulated or defaulted
column cannot masquerade as valid provenance."

**Evidence this was decided on (NEW EVIDENCE 23, `[fact]`, read this session).**
- `SmartStudyPlanner/Models/Telemetry/DifficultyLabelLog.cs` — 9 fields, **zero provenance fields**.
  `Source` is the nearest thing and is the hardcoded literal `"manual"` at
  `SmartStudyPlanner/ViewModels/QuanLyTaskViewModel.cs:342`.
- `SmartStudyPlanner/Models/Telemetry/StudyTimeOutcomeLog.cs` — 12 fields, **zero provenance fields**.
- **Both tables store model outputs with no model identity.** `SuggestedDoKho`;
  `PredictedMinutes` / `Confidence` / `WasMlPrediction`. A row cannot say WHICH model produced its
  prediction, and that is not reconstructible after the fact — the canonical failure class.

**What is now ruled.**
1. **Capture-complete tie-breaker.** Record every S-3 lineage field meaningful for a runtime-captured
   row. **When it is unclear whether a field will be needed, record it.** Explicitly NOT a
   minimal/predict-what-matters rule.
2. **Gate 4 validation is mandatory on those fields.** An **unpopulated or defaulted column must not
   be able to masquerade as valid provenance** — validation/invariants must reject it.

**How the counter-argument was dissolved (10th occurrence).** The agent objected that unused
provenance columns rot — filled with defaults, never checked — and that a rotted field is worse than an
absent one because it looks like evidence. Clause 2 answers it directly: the rot is only dangerous if a
default reads as valid, so validation must make that state rejectable. This is the owner's standing
"a signal must be able to fail" requirement applied to schema rather than to tests.
(Prior: S-3.5, S-4.2, S-4.6, S-5.6, S-6.3, S-7.4, S-8.1, scope, S-T.1, S-T.2.)

**Standing principle #5 — "an unresolvable state is an error, never a default" — 4th explicit
instantiation** (S-3.5, S-3.7, S-4.6, S-T.2; structurally also S-5.3). S-T.2 extends it from
*resolution failure* to *absence of a value*: a defaulted provenance field is an unresolvable state
wearing a valid-looking value.

**Structural consequence — gate 4 is no longer purely downstream.**
SS-T lists gates in dependency order 1→5 with the dataset contract at 4. This ruling makes **gate 4
supply the validation for gate 1**, so the two are mutually dependent: gate 1 defines what is written,
gate 4 defines what makes a written value valid. The rewrite must state this rather than leaving the
list reading as a strict sequence.

**Queued rewrite fixes from this decision.**
- SS-T gate 1: state the capture-complete rule and the gate-4 validation requirement together.
- SS-T "Gates, in dependency order": correct the ordering claim — 1 and 4 are mutually dependent.
- SS-T source table: record NEW EVIDENCE 23 — both tables currently carry zero provenance, and neither
  can identify the model behind the prediction it stores.
- Add to the standing-principles section: a defaulted field is an unresolvable state in disguise.

### S-T.3 — Measurement authorisation on the telemetry tables (gate 5)  [RULED: A]
**Owner ruling (verbatim):** "A. Authorize only volume and technical-usability measurements now: row
counts, capture-date ranges, and null/usability rates for the already-defined evaluation fields such
as PredictedMinutes and Confidence. Do not inspect label or feature marginals until the S-5.7
allocation rule is pre-registered. Record the measurement scope explicitly so these operational checks
cannot be mistaken for Gold-R distribution analysis."

**Context.** SS-T's exit criteria demand "a `[measured]` row count" but nothing has ever read these
tables and no measurement was authorised, so the criterion was unreachable. The hazard SS-T does not
anticipate: **S-5.7 requires the Gold-R allocation rule to be pre-registered BEFORE row content is
observed**, so measuring label marginals now would contaminate any later split-rule design. Volume
does not carry that hazard — S-5.7 instantiates the split fraction *after* volume is known, so knowing
the count is permitted by design.

**What is now ruled — AUTHORISED.**
- Row counts (both tables).
- Capture-date ranges.
- Null / usability rates on the **already-defined** evaluation fields (`PredictedMinutes`,
  `Confidence`) — i.e. how many pre-DFD-9a rows are unrecoverable.

**What is now ruled — PROHIBITED until the S-5.7 allocation rule is pre-registered.**
- Label marginals (e.g. `FinalDoKho`, `SuggestedDoKho` distributions).
- Feature marginals (`TaskType`, `Difficulty`, `Credits`, `DaysLeft`, `StudiedMinutesSoFar`, …).

**Scope-recording requirement.** The measurement scope must be **recorded explicitly** alongside any
figure produced, so operational usability checks **cannot be mistaken for Gold-R distribution
analysis**. A number from this measurement is a volume/usability fact and must never be cited as
evidence about the data's distribution.

**On the agent's counter-argument.** The agent objected that A's separation is procedural rather than
structural — the same person who measures usability will later help design the allocation rule, so
discipline, not architecture, enforces the boundary. The ruling does **not** make the separation
structural; it makes the boundary a **recorded artifact**, so a later breach is visible rather than
silent. Honest characterisation: mitigated and made auditable, not eliminated.

**Status: AUTHORISED, NOT PERFORMED.** No measurement was run in this session. Task remains open.

**Consequences.**
- Gate 5's exit criterion is now reachable; it was not before.
- Any figure from this measurement enters the proposal as `[measured]` with its scope statement
  attached — an instance of principle #1, "the qualifier travels with the datum" (8th).
- The prohibition list is a **standing constraint tied to S-5.7's pre-registration**, not to this
  session. It lifts when the allocation rule is registered, not when someone judges it safe.

**Queued rewrite fixes from this decision.**
- SS-T gate 5 / exit criteria: state what measurement is authorised, what is prohibited, and the
  trigger that lifts the prohibition.
- SS-T: add the scope-recording requirement so a volume figure cannot be repurposed as distribution
  evidence.
- SS-5.7: cross-reference — pre-registration of the allocation rule is what unlocks marginal
  inspection on telemetry.

### S-T.4 — Retention, and its collision with gate 5  [RULED: B]
**Owner ruling (verbatim):** "B. Define a bounded rolling-retention rule now, but instantiate the
window only after S-T.3 measures its effect on usable volume. Before the number is fixed, verify that
the resulting retained volume can still make Gate 5 attainable; if no reasonable window does so, return
to owner decision rather than selecting a window that makes the gate unreachable."

**Context.** Gate 3 is "retention and handling rules"; for a local SQLite file, handling is largely
OS-level, so retention duration is the load-bearing question. It collides with gate 5 ("sufficient
volume"): any time-bounded retention caps volume, and for a small alpha-tester population a short
window could make gate 5 permanently unreachable — the strand waiting for a quantity its own retention
rule deletes.

**What is now ruled.**
1. **Bounded rolling retention is the RULE**, fixed now. Indefinite device-bounded retention is
   rejected.
2. **The window (the number) is instantiated only after S-T.3's volume measurement**, so it is set
   against evidence rather than guessed. Rule/number split, **5th instantiation** (S-4.1, S-5.7,
   S-6.1, S-8.1, S-T.4).
3. **Feasibility check before the number is fixed:** verify the retained volume under the candidate
   window can still make **gate 5 attainable**.
4. **Escalation clause (new mechanism this session):** if **no reasonable window** leaves gate 5
   attainable, **return to owner decision** — do NOT select a window that makes the gate unreachable,
   and do not quietly relax the gate to fit the window.

**How the counter-argument was dissolved (11th occurrence).** The agent recommended A (indefinite,
device-bounded) on gate-5 grounds and argued against itself that an unbounded silent backlog makes the
eventual consent request cover data the user never knew was accruing. The ruling takes the bounded
rule AND removes the gate-5 objection to it by making window selection conditional on feasibility,
with escalation rather than accommodation when feasibility fails.
(Prior: S-3.5, S-4.2, S-4.6, S-5.6, S-6.3, S-7.4, S-8.1, scope, S-T.1, S-T.2, S-T.4.)

**Standing principle #4 — "when a bar is not met, revise or refuse; do not lower the bar" — 6th
instantiation** (S-2.7, S-4.1, S-4.4, S-5.7, S-7.2, S-T.4). Here the refusal is explicit and routed:
escalate to the owner rather than pick a window that guarantees failure.

**Consequences.**
- S-T.3's measurement now has a **second consumer**: it sets the retention window as well as
  satisfying gate 5's `[measured]` count. Both uses stay inside S-T.3's authorised scope
  (volume + usability), so no marginal inspection is required to fix the window.
- The retention window is a **pending number**, and must be recorded as `[unknown]` in the rewrite —
  not omitted, not estimated.
- S-T.1's local gate cannot close until the window is instantiated, since retention policy is part of
  what that gate requires.

**Queued rewrite fixes from this decision.**
- SS-T gate 3: state the bounded rolling rule, the deferred window, the feasibility check, and the
  escalation clause.
- SS-T gate 5: note the retention/volume interaction explicitly — the document currently treats the
  two gates as independent and they are not.
- Standing-principles section: record the escalation clause as the routed form of principle #4.

### S-T.5 — One governance artifact, or two  [RULED: A]
**Owner ruling (verbatim):** "A. Unify under the existing S-3.6 datasheet as the single governance
artifact type, with explicit static-source and live-source sections. Static-only fields must be
schema-marked N/A for live tables rather than left blank; the live section must carry accrual,
retention, provenance validation, permitted-use, and gate-state requirements. Do not introduce a
second contract vocabulary or rename the ratified datasheet artifact."

**Context.** SS-T gate 4 calls for a "dataset contract"; SS-3.6 already defines a per-source
"datasheet", and S-7.1 made the datasheet the place where per-source Gold-A eligibility is ruled. Two
names, possibly one object, never decided — vocabulary drift of exactly the class this exercise exists
to catch. The real difference is lifecycle: corpus files are static and hashable, telemetry tables are
live and growing.

**What is now ruled.**
1. **ONE artifact type: the S-3.6 datasheet.** "Dataset contract" is not a separate artifact and the
   term must not enter the vocabulary as one.
2. **Two explicit sections:** static-source and live-source.
3. **Static-only fields are schema-marked `N/A` for live tables — never left blank.**
4. **The live-source section must carry:** accrual, retention, provenance validation, permitted use,
   and **gate states**.
5. **No rename of the ratified datasheet artifact**, and no second vocabulary.

**How the counter-argument was dissolved (12th occurrence).** The agent objected that forcing a live
table into an artifact built for static hashed files leaves its most load-bearing fields (content hash,
row count, version) permanently unfillable, and that a datasheet with always-blank fields teaches
readers blank is normal — corroding every other datasheet, the same failure S-T.2 ruled against for
provenance columns. Clause 3 answers it with the same device used at S-T.1: an explicit **`N/A`**
state, distinguishable from blank.
(Prior: S-3.5, S-4.2, S-4.6, S-5.6, S-6.3, S-7.4, S-8.1, scope, S-T.1, S-T.2, S-T.4, S-T.5.)

**EMERGING PATTERN — the explicit `N/A` state (record once in the rewrite).**
Three rulings now solve a "cannot be mistaken for" problem with the same device rather than with
prose: S-T.1 (egress gate is `N/A`, not `closed`), S-T.2 (a defaulted provenance column must be
rejectable, not silently valid), S-T.5 (static-only fields are `N/A` for live tables, not blank).
The general rule: **wherever absence is legitimate, it gets an explicit value; blank is reserved for
"not yet supplied" and is always an error state.** Sibling of principle #5.

**Consequences.**
- S-3.6's datasheet schema now takes THREE additions from this session: the S-7.1 Gold-A eligibility
  field, the static/live section split, and the live-source field set (clause 4).
- Gate states live in the datasheet, so S-T.1's two-gate visibility requirement has a home — the
  datasheet is where both states are rendered.
- SS-T's gate 4 wording ("a dataset contract") must be rewritten to reference the datasheet.

**=== S-T STRAND CLOSED — five decisions (S-T.1 C, S-T.2 A, S-T.3 A, S-T.4 B, S-T.5 A) ===**
All five SS-T gates now ruled: provenance at write time (S-T.2), consent (S-T.1, split into two
independent gates), retention/handling (S-T.4), dataset contract (S-T.5, unified into the datasheet),
sufficient volume (S-T.3, measurement authorised within scope).
Outstanding S-T tasks, not decisions: run the authorised measurement; instantiate the retention window;
implement capture-complete provenance + its gate-4 validation.

**=== ALL DECISION PASSES COMPLETE: S-1..S-8 + S-T ===**
Next and final deliverable: the consolidated decision record, then a single proposal rewrite.

## Open / pending

- S-3.1 CLOSED (B). S-3.2 CLOSED (B + marker mandatory on export). S-3.3 CLOSED (B + byte-stable export + S-3 completion gate). S-3.4 CLOSED (A + repoint + both pins + no sidecar/no 2nd export). S-3.5 CLOSED (B + fail-closed, versioned required-key set). S-3.6 CLOSED (C + A8 named exclusion -> FU-2). S-3.7 CLOSED (B, CI authoritative; _merge_seed.py RETIRED). S-3 block FULLY CLOSED (7 + confirmation). S-4.1 CLOSED (C at scope level; size+rule pre-registered, not invented). S-4.2 CLOSED (C; tier DERIVED from provenance, not stored twice). S-4.3 CLOSED (B commit-then-reveal; AI never authority; changes logged as 2nd review). S-4.4 CLOSED (A; exclude + guideline-gap log; bump re-review scoped to rows whose rule actually changed -- cascade PRE-EMPTED, do not re-present). S-4.5 CLOSED (C; targeted citation -- Difficulty anchor + authored-sample placement rule; contested TaskType inherits boundary). S-4.6 CLOSED (B; pinned manifest + FAIL-CLOSED resolution). **S-4 BLOCK CLOSED, 6 decisions.** Next block: S-5 (Gold-R). S-5.1 CLOSED (A; explicit per-participant consent at transfer, consent record preserved alongside data; Q-3 residual RESOLVED). S-5.2 CLOSED (C; retroactive + derived marker + tier cap + S-6 held-out ineligible; grade DERIVED per S-4.2 precedent). S-5.3 CLOSED (C; one corpus, role-partitioned, enforcement boundary not second authority; role is the ONE physical attribute). S-5.4 CLOSED (B; Gold-A stays in training, is a reference/adjudication corpus not a perf holdout; S-6 wording to be corrected). Partition layout determined: training vs held-out, held-out fed by Gold-R only. S-5.5 CLOSED (A; owner labels under S-2 spec, user signal = metadata only; commit-then-reveal extended to SuggestedDoKho BY PRECEDENT, flagged). FU-3 opened. Gold-R labelling cost = unpriced, scales with volume -- surfaced. S-5.6 CLOSED (B + explicit dependency pinning; manifest = dependency declaration). S-4.6 residue RESOLVED. S-5.7 CLOSED (B + 3 conditions). **S-5 BLOCK CLOSED, 7 decisions.** S-T strand flagged as unaddressed (outside the ratified ordering). Next block: S-6 (evaluation foundation). S-6.1 CLOSED (B + headline discipline: all registered metrics reported, only the pre-registered headline is primary, rest are MANDATORY DIAGNOSTICS not alternatives; chance baseline per metric from observed Gold-R marginal). S-6.2 CLOSED (B; MAE headline, exact-match mandatory diagnostic, within-one diagnostic only). S-6.3 CLOSED (C + one-sided guard; intervals mandatory, width always qualifies, no precision threshold pre-Q-5). **S-6 BLOCK CLOSED, 3 decisions.** Next: S-7 (controlled expansion). ALSO QUEUED: S-4.6 manifest granularity (row-level vs whole-corpus hash) -- flagged, unruled.
- SMALL CONFIRMATIONS QUEUED: (i) `_merge_seed.py` retire vs convert; (ii) canonical->export attestation lives in a build manifest outside the export bytes.
- **S-3 queue (not yet presented):** corpus->export drift guarantee (created by S-3.1=B); which of
  the 8 files get file-level datasheets and who authors them; where the corpus physically lives and
  in what format; whether the ingest gate is a script, a test, or CI.
- Remaining after S-3: S-4, S-5, S-6, S-7, S-8, then the consolidated decision record, then the
  proposal rewrite.
- S-2 queue after that: guideline versioning mechanism (LabelVersion versions the FILE, not the
  guideline); whether "the spec records which parts were AI-drafted" (currently agent-drafted text
  in rev 2, never ratified) is confirmed; overlap between S-2's "label provenance" content and S-3.
- OPEN, S-4 territory, DO NOT RULE IN S-2: do the 97 third-pass contested rows join J-1 as work?
  Q-1's design depends on the answer; option C below is robust to it either way.
- S-2 queue (not yet presented, order provisional):
  - batch size n (NOT owner-ratified; see NEW EVIDENCE 7) and the two pre-registered thresholds,
    incl. exact-match vs within-one-level for Difficulty (chance baselines above bear directly).
  - the pre-registered agreement threshold(s) — incl. whether Difficulty agreement is exact-match
    or within-one-level (68 of 167 cross-pass Difficulty moves were TWO-step).
  - reservation parameters (which pools, batch sizes, draw method) — deferred out of S-1.
    DECISION order is logical; EXECUTION order is fixed by S-1.6's addition: reserve first.
  - how the ambiguous-example catalogue is scoped now that the raw material is 155 production-
    relevant rows in two pools, not "the 208".
  - guideline versioning mechanism (LabelVersion versions the FILE, not the guideline).
- FU-1 (see above) — not scheduled.
- FU-2 (S-3.6): governance for A8 / `SeedDataGenerator` in the M7 domain — named exclusion, not scheduled.
- Stages after S-2: S-3, S-4, S-5, S-6 (now also owns the relocated evaluation defect), S-7, S-8.

## Documentation fixes queued for the eventual rewrite (flagged, NOT yet applied)

- S-1 table: "two of the three largest transitions name retired classes" is FALSE as placed.
  Audit §J-2 carries the identical error.
- S-1 `[inference]` prose says "the fourth item" / "the other three answers"; the table has 5 rows.
- S-1 table: "Difficulty ... is currently never trained on" is FALSE (true only of the text
  classifier). See NEW EVIDENCE 3.
- S-1 table: KiemTraThuongXuyen / ThiCuoiKy described as possibly "aspirational" — they are 51.3%
  of training data. See NEW EVIDENCE 4.
- §8: "S-1 needs no infrastructure, no tooling and no data" is FALSE.
- §S-2: "reserve the reproducibility batch before S-2 starts" is too late — must be before S-1.
- §S-2: "forty rows out of 208 is affordable" — the live pool is 36 + 121 = 157 in two pools.
- §4.1: "Gold-A adjudication scope = 208 rows" is measured against the interim space, not
  production's.
- Denominator: the 29.6% is 208/703, not 208/1028 (NEW EVIDENCE 6).
- §S-3: "file-level datasheets (8 files, 0 exist)" — wrong on both counts (NEW EVIDENCE 20).
- §S-3: "~5 new columns (7 -> ~12)" — the real figure is ~9 new fields, 7 -> ~16.

## Discipline note
Agent has STILL not read any individual row text. Every finding above is aggregate statistics only,
preserving the option to reserve a clean held-out batch before S-1 executes (S-1.2 / S-1.6).
