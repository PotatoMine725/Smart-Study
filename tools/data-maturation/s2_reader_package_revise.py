# -*- coding: utf-8 -*-
"""S-2 reader package - revise the EXISTING instrument for handoff, in Vietnamese.

The 20-row instrument was materialised at `b9691ae` by
tools/data-maturation/s2_materialize.py. **That materialisation is the source of
truth and is not re-selected, re-rendered or reconstructed here.** This script
takes the committed sheet blob, keeps its 20 item blocks byte for byte, and
replaces only the surrounding prose - which is the script's own, not the
instrument - with the Vietnamese prose in s2_reader_prose_vi/.

  In:  git blob b9691ae:docs/specs/2026-09-05-s2-blind-reader-sheet.md
       git blob da98e73:docs/specs/annotation-guideline.md
       tools/data-maturation/s2_reader_prose_vi/*.md

  Out: docs/s2-reader-package/00-READ-ME-FIRST.md
       docs/s2-reader-package/01-annotation-guideline-v1.md      (frozen v1, untouched)
       docs/s2-reader-package/01b-huong-dan-tieng-viet.md        (rendering of v1)
       docs/s2-reader-package/02-annotation-sheet.md
       datasheets/reservations/2026-09-05-s2-reader-package-manifest.json  (private)

**The 20 item blocks are copied through byte for byte** - text, order, ids and
answer fields - and the script asserts that the emitted body is identical to the
source body before it writes anything. **Row text is never translated**: it is
the material under test, it is already Vietnamese, and altering one character of
it would change what the section-10 test measures.

**Frozen `v1` is copied byte for byte and is never translated in place.** The
Vietnamese rendering ships beside it as a separate file with its own hash, and
`v1` remains the text with authority. The rendering is UNRATIFIED - see the
manifest.

No Vietnamese literal lives in this file. All reader-facing prose is read from
s2_reader_prose_vi/ so the translation can be reviewed as prose rather than as
Python string escapes, and every fragment is normalised to LF on read because
those source paths are not covered by the package's .gitattributes rule.

The corpus is never read. The snapshot is never read. Nothing is adjudicated,
scored or annotated. Preparing this package is not execution of the section-10
test.

The reconciliation map stays where it already is - the private scoring key at
datasheets/reservations/2026-09-05-s2-scoring-key.json. This script writes no
second copy of it and no label of any kind.
"""
import hashlib
import io
import json
import os
import re
import subprocess
import sys
from collections import OrderedDict

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PKG = os.path.join(ROOT, "docs", "s2-reader-package")
PROSE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "s2_reader_prose_vi")
MANIFEST = os.path.join(ROOT, "datasheets", "reservations",
                        "2026-09-05-s2-reader-package-manifest.json")
KEY_REL = "datasheets/reservations/2026-09-05-s2-scoring-key.json"

SHEET_COMMIT = "b9691ae131ad4b71d6e69a4b197d3a2de02757ec"
SHEET_PATH = "docs/specs/2026-09-05-s2-blind-reader-sheet.md"
SHEET_SHA = "151d3f803328f42044adc29283b432abe6b5473145c9631243b10b20909d09c1"

GUIDELINE_COMMIT = "da98e73ff64565729e7fe9f2f8afca6c4bdbf306"
GUIDELINE_PATH = "docs/specs/annotation-guideline.md"
GUIDELINE_SHA = "dd4fc2736d83c18c373161fc371070a967e082bbbf7987a142a434103684a433"

RENDERING_NAME = "01b-huong-dan-tieng-viet.md"

# Strings that rules B-2 and B-4 decide on lexically. In the English source they
# only ever appear as marked foreign quotations; in Vietnamese prose they are
# ordinary words that would happily appear in an explanation, and B-4 acts on
# their mere presence. The rendering may therefore use them ONLY inside code
# spans. Enforced by the validator too, restated here so the two lists cannot
# drift apart silently.
TRIGGERS = [u"giữa kỳ", u"cuối kỳ", u"kiểm tra",
            u"đồ án", u"bài tập lớn", u"nhóm"]

GITATTRIBUTES = u"""# The reader package is verified by sha256: `01-annotation-guideline-v1.md` must
# hash to dd4fc273...684a433, the frozen `GuidelineVersion: v1` blob. Checkout-time
# CRLF translation would change those bytes and break the check on any machine
# that clones the repo, so these paths are checked out exactly as committed.
docs/s2-reader-package/** -text
"""


def sha_bytes(b):
    return hashlib.sha256(b).hexdigest()


def sha_file(path):
    with open(path, "rb") as f:
        return sha_bytes(f.read())


def blob(commit, path):
    """Raw committed bytes - never the working copy, which core.autocrlf rewrites."""
    return subprocess.check_output(["git", "cat-file", "blob", "%s:%s" % (commit, path)],
                                   cwd=ROOT)


def prose(name):
    """One reader-facing prose fragment, normalised to LF.

    tools/ is not covered by the package's -text rule, so on a clone with
    core.autocrlf=true these fragments arrive CRLF. Emitting them unnormalised
    would put CRLF into the package and break its byte-level checks.
    """
    path = os.path.join(PROSE, name)
    if not os.path.exists(path):
        sys.exit("prose fragment %s is missing - stopping" % name)
    with io.open(path, encoding="utf-8", newline="") as f:
        text = f.read().replace(u"\r\n", u"\n").replace(u"\r", u"\n")
    if not text.strip():
        sys.exit("prose fragment %s is empty - stopping" % name)
    return text


def split_sheet(text):
    """(header, [20 item blocks], footer) with header + ''.join(blocks) + footer == text."""
    first = text.index(u"### R-01")
    tail = text.index(u"\n---\n\n**When you are done**") + 1
    header, body, footer = text[:first], text[first:tail], text[tail:]
    blocks = [b for b in re.split(r"(?m)^(?=### R-\d\d$)", body) if b.strip()]
    if len(blocks) != 20:
        sys.exit("source sheet holds %d item blocks, expected 20 - stopping" % len(blocks))
    ids = [re.match(r"### (R-\d\d)", b).group(1) for b in blocks]
    if ids != ["R-%02d" % n for n in range(1, 21)]:
        sys.exit("source item ids are not R-01..R-20 - stopping")
    if header + u"".join(blocks) + footer != text:
        sys.exit("split is not lossless - stopping")
    return header, blocks, footer


def check_rendering(rendering, guideline):
    """The rendering must mirror v1's section structure and lock the trigger terms."""
    want = re.findall(r"(?m)^## (\d+)\.", guideline)
    got = re.findall(r"(?m)^## (\d+)\.", rendering)
    if got != want:
        sys.exit("rendering sections %s do not mirror v1's %s - stopping" % (got, want))
    # Trigger terms outside code spans would let the translation move a lexical
    # boundary that B-2/B-4 decide on. Only prose THIS package authored can do
    # that: a line appearing verbatim in frozen v1 is v1's own text - a quoted
    # corpus row in the catalogue, say - so drop those first, then fenced
    # blocks, then inline spans.
    frozen = set(l.strip() for l in guideline.split(u"\n") if l.strip())
    bare = u"\n".join(l for l in rendering.split(u"\n") if l.strip() not in frozen)
    bare = re.sub(r"(?s)```.*?```", u"", bare)
    bare = re.sub(r"`[^`]*`", u"", bare)
    for t in TRIGGERS:
        if t in bare:
            sys.exit("rendering uses trigger term %r outside a code span - stopping"
                     % t.encode("utf-8"))
    return len(want)


def main():
    src = blob(SHEET_COMMIT, SHEET_PATH)
    if sha_bytes(src) != SHEET_SHA:
        sys.exit("source sheet blob hashes %s, expected %s - stopping"
                 % (sha_bytes(src), SHEET_SHA))
    _, blocks, _ = split_sheet(src.decode("utf-8"))
    new_header, new_footer = prose("sheet-header.md"), prose("sheet-footer.md")
    body = u"".join(blocks)
    sheet = new_header + body + new_footer

    # The instrument itself must survive the revision untouched.
    if sheet[len(new_header):len(new_header) + len(body)] != body:
        sys.exit("emitted body is not byte-identical to the source body - stopping")

    guideline_bytes = blob(GUIDELINE_COMMIT, GUIDELINE_PATH)
    rendering = prose("01b-guideline-vi.md")
    sections = check_rendering(rendering, guideline_bytes.decode("utf-8"))

    if not os.path.isdir(PKG):
        os.makedirs(PKG)
    instr_dest = os.path.join(PKG, "00-READ-ME-FIRST.md")
    guideline_dest = os.path.join(PKG, "01-annotation-guideline-v1.md")
    rendering_dest = os.path.join(PKG, RENDERING_NAME)
    sheet_dest = os.path.join(PKG, "02-annotation-sheet.md")

    with io.open(instr_dest, "w", encoding="utf-8", newline="") as f:
        f.write(prose("00-instructions.md"))
    with open(guideline_dest, "wb") as f:            # binary: v1 byte for byte
        f.write(guideline_bytes)
    got = sha_file(guideline_dest)
    if got != GUIDELINE_SHA:
        sys.exit("guideline copy hashes %s, expected %s - stopping" % (got, GUIDELINE_SHA))
    with io.open(rendering_dest, "w", encoding="utf-8", newline="") as f:
        f.write(rendering)
    with io.open(sheet_dest, "w", encoding="utf-8", newline="") as f:
        f.write(sheet)

    ga = os.path.join(ROOT, ".gitattributes")
    if not os.path.exists(ga):
        with io.open(ga, "w", encoding="utf-8", newline="") as f:
            f.write(GITATTRIBUTES)

    man = OrderedDict()
    man["event"] = "S-2 reader package revision - Vietnamese"
    man["status"] = ("Package PREPARED. The section-10 reproducibility test is NOT performed, no "
                     "reader is recruited, nothing was annotated, adjudicated or scored, and no "
                     "figure exists. Both blockers are RULED (D-7, D-8); handoff still needs the "
                     "owner's own verification of the rendering under D-8.")
    man["authority"] = ["owner authorisation 2026-09-05 (revise the reader package)",
                        "owner instruction 2026-09-05 (translate the package to Vietnamese)",
                        "S-2.2", "S-2.12",
                        "D-6 (unresolved scoring NOT authorised, 2026-09-05)",
                        "D-7 (16-row secondary reading, pre-registered 2026-09-05)",
                        "D-8 (rendering normative for both passes, 2026-09-05)",
                        "D-9 (catalogue provenance stripped from the rendering, 2026-09-05)"]
    man["numbering_correction"] = (
        "An earlier revision of this file cited 'D-5' for the unresolved-scoring constraint. "
        "The globally-cited owner-decision series (S-1 limited taxonomy review, section 6, which "
        "v1 section 10 cites for D-2 and section 13 for D-4) already uses D-5 for 'the sealed "
        "residual'. The constraint is renumbered D-6 and the series continues at D-7.")
    man["never_hand_to_a_reader"] = True
    man["holds_no_label"] = (
        "This file carries no label value, no Gold answer, no historical pass label, no stratum "
        "and no D-2 class. The reconciliation map is unchanged and stays where it is: %s"
        % KEY_REL)
    man["blocking_finding"] = OrderedDict([
        ("what", "Four reserved rows are template-identical to entries in the frozen v1 section-6 "
                 "catalogue, differing only in the course name. Those catalogue entries carry "
                 "rulings, so a reader who reads the guideline first - as the instructions "
                 "direct - can answer those rows by pattern match instead of by applying the "
                 "rules."),
        ("items", ["R-01", "R-09", "R-14", "R-16"]),
        ("v1_lines", [332, 265, 396, 256]),
        ("similarity", [0.86, 0.83, 0.75, 0.83]),
        ("why_it_matters", "Those rows are scored the same as any other against the "
                           "pre-registered TaskType threshold, so agreement on them is not "
                           "evidence that the guideline reproduces a label."),
        ("no_label_recorded_here", "The derived labels are deliberately NOT written down - not "
                                   "here, not in the plan doc, not in the report. The claim is "
                                   "the template match, nothing more."),
        ("not_fixable_by_this_script", "v1 is frozen under S-2.14 and the batch is sealed under "
                                       "S-0. Both sides are owner decisions."),
        ("status", "RULED by the owner 2026-09-05. Remedy is D-7: the pre-registered 20-row "
                   "gate is unchanged, and the 16 rows that are NOT template twins are reported "
                   "as the primary evidence. See also D-9, which removes the catalogue's label "
                   "and locator metadata from the reader-facing rendering."),
        ("v1_own_invariant_that_fails", "Section 6 states: 'The reserved 60 rows are absent from "
                                        "this catalogue by construction - an example drawn from "
                                        "the scored batch would train a reader on a row they are "
                                        "later measured against.' The exclusion was implemented "
                                        "at ROW granularity while the corpus is template "
                                        "generated, so template siblings passed through. The "
                                        "letter holds - zero verbatim matches - and the spirit "
                                        "does not."),
    ])
    man["d7_secondary_reading"] = OrderedDict([
        ("ruled", "owner, 2026-09-05, BEFORE any annotation - so this is pre-registration, not a "
                  "post-hoc adjustment. The window closes the moment the reader starts."),
        ("gate_unchanged", "TaskType >= 17/20 and Difficulty >= 18/20, full-batch, exact match. "
                           "S-2.7's invariant is untouched: no threshold was changed."),
        ("secondary_reading", "The 16 rows that are not template twins are additionally scored "
                              "and reported as the PRIMARY evidence, at >= 14/16 TaskType and "
                              ">= 15/16 Difficulty."),
        ("excluded_from_the_secondary_reading", ["R-01", "R-09", "R-14", "R-16"]),
        ("both_figures_travel_together", True),
    ])
    man["d8_rendering"] = OrderedDict([
        ("ruled", "owner, 2026-09-05"),
        ("normative_for_this_test", "the Vietnamese rendering"),
        ("both_passes_use_it", "The owner's Gold pass and the reader's blind pass run on the SAME "
                               "text, so translation cannot become an uncontrolled variable "
                               "between them."),
        ("scope_that_travels_with_every_figure",
         "reproducibility of v1 AS RENDERED IN VIETNAMESE"),
        ("outstanding", "The owner has not yet read the rendering through. Ratification is that "
                        "read-through plus recording the rendering's sha256 in the freeze "
                        "record. Until then the package is not handed to a reader."),
    ])
    man["d9_provenance_strip"] = OrderedDict([
        ("ruled", "owner, 2026-09-05"),
        ("what_is_removed", "per catalogue entry: the row sha256, the source file:line locators, "
                            "and the historical pass-label / Difficulty line"),
        ("what_is_kept", "every example row verbatim, and every word of commentary"),
        ("why", "Those three are provenance metadata, not decision support. v1 section 6 itself "
                "says no entry may be cited as its row's label, so removing them takes nothing "
                "the reader is meant to reason from - while removing 19 label progressions and "
                "57 locators from in front of an annotator instructed not to look anything up."),
        ("counts_removed_from_the_rendering", OrderedDict([
            ("label_progressions", 19), ("difficulty_values", 19),
            ("row_sha256", 19), ("source_locators", 57)])),
        ("divergence_from_v1", "metadata-only; frozen v1 still ships byte-exact alongside"),
        ("enforced_by", "validator check C18"),
    ])
    man["instrument_provenance"] = OrderedDict([
        ("source", "%s:%s" % (SHEET_COMMIT, SHEET_PATH)),
        ("source_blob_sha256", SHEET_SHA),
        ("source_unmoved_and_unmodified", True),
        ("selection", "NOT re-selected. NOT re-rendered. The corpus and the sealed S-0 snapshot "
                      "were not read by this revision at all."),
        ("body_bytes_preserved", len(body.encode("utf-8"))),
        ("body_sha256", hashlib.sha256(body.encode("utf-8")).hexdigest()),
        ("body_identity", "the 20 item blocks - text, order, ids and answer fields - are "
                          "byte-identical to the source; asserted before writing"),
        ("row_text_translated", False),
        ("prose_replaced", "header and footer only, from s2_reader_prose_vi/; these are the "
                           "script's own prose and were never part of the instrument"),
        # Carried forward from the 2026-09-05 English revision. That revision made
        # three surgical edits to the source header; the header is now replaced
        # wholesale, so the record of WHAT had to go would otherwise be lost with it.
        # The Vietnamese header must not reintroduce any of these.
        ("source_prose_disclosures_removed", [
            "the title named the batch and its reserved status",
            "the text named the other annotator, disclosing that a second pass exists",
            "a `role` row printed `gold` / `probe`, disclosing that a reference pass exists",
        ]),
    ])
    man["translation"] = OrderedDict([
        ("rendering", "docs/s2-reader-package/%s" % RENDERING_NAME),
        ("derived_from", GUIDELINE_SHA),
        ("frozen_v1_modified", False),
        ("authority", "v1 governs. The rendering is a reading aid and is stated as such in its "
                      "own header and in 00-READ-ME-FIRST.md."),
        ("ratified", False),
        ("owner_decision_required",
         "If the reader works from the rendering then the rendering is in practice the "
         "instrument, and it is unratified. It needs owner ratification before handoff."),
        ("sections_mirrored", sections),
        ("section_6", "Rendered in full - 19 entries, every example row verbatim, all "
                       "commentary translated - with the provenance lines removed under D-9. The "
                       "rendering states that removal in place."),
        ("fidelity_rule", "No rule added, no example added, no ambiguity resolved. v1's known "
                          "soft spots - B-2 in particular - are rendered exactly as unclear as "
                          "they are in the source."),
        ("trigger_terms_locked", TRIGGERS),
        ("trigger_term_rule", "The strings B-2/B-4 decide on appear in the rendering only inside "
                              "code spans, so the translation cannot move a lexical boundary. "
                              "The Vietnamese prose uses longer, non-matching wordings instead."),
    ])
    man["reader_package"] = OrderedDict([
        ("directory", "docs/s2-reader-package/"),
        ("language", "Vietnamese, except the byte-exact frozen v1 copy"),
        ("files", [
            OrderedDict([("path", "docs/s2-reader-package/00-READ-ME-FIRST.md"),
                         ("sha256", sha_file(instr_dest))]),
            OrderedDict([("path", "docs/s2-reader-package/01-annotation-guideline-v1.md"),
                         ("sha256", got),
                         ("is", "byte-exact frozen v1, %s:%s"
                          % (GUIDELINE_COMMIT, GUIDELINE_PATH))]),
            OrderedDict([("path", "docs/s2-reader-package/%s" % RENDERING_NAME),
                         ("sha256", sha_file(rendering_dest)),
                         ("is", "Vietnamese rendering of v1; unratified reading aid")]),
            OrderedDict([("path", "docs/s2-reader-package/02-annotation-sheet.md"),
                         ("sha256", sha_file(sheet_dest))]),
        ]),
        ("line_endings", "LF, as committed. .gitattributes marks these paths -text so a clone "
                         "cannot CRLF-translate them and break the v1 hash check."),
    ])
    man["guideline"] = OrderedDict([("path", GUIDELINE_PATH), ("version", "v1"),
                                    ("commit", GUIDELINE_COMMIT), ("sha256", GUIDELINE_SHA),
                                    ("modified", False)])
    man["reconciliation"] = OrderedDict([
        ("scoring_key", KEY_REL),
        ("how", "reader answers carry item ids R-01..R-20; the key maps each id to its sha256 "
                "and occurrences. Item ids are unchanged by this revision."),
        ("not_copied_here", True),
    ])
    man["d6"] = ("The owner has not authorised a scoring treatment for `unresolved`. The reader "
                 "can still mark it; neither the sheet nor the instructions nor the rendering "
                 "state how it scores, and no scoring semantics were added to v1.")
    payload = json.dumps(man, ensure_ascii=False, indent=2, sort_keys=False)
    man["seal"] = OrderedDict([
        ("algorithm", "sha256 over this document with the seal field absent"),
        ("value", hashlib.sha256(payload.encode("utf-8")).hexdigest()),
    ])
    with io.open(MANIFEST, "w", encoding="utf-8", newline="") as f:
        f.write(json.dumps(man, ensure_ascii=False, indent=2, sort_keys=False) + u"\n")

    print("instructions", instr_dest, sha_file(instr_dest))
    print("guideline   ", guideline_dest, got)
    print("rendering   ", rendering_dest, sha_file(rendering_dest))
    print("sheet       ", sheet_dest, sha_file(sheet_dest))
    print("manifest    ", MANIFEST, man["seal"]["value"])
    print("sections mirrored:", sections, "| trigger terms locked:", len(TRIGGERS))


if __name__ == "__main__":
    main()
