"""Read StudyTimeOutcomeLogs — the DFD-9a end-to-end observation channel.

Prints every row of `StudyTimeOutcomeLogs` with nulls shown literally as `NULL`,
never coerced to 0. That distinction *is* the DFD-9a check: a pre-fix row records
that a prediction happened without recording what it was.

Usage (identical in PowerShell, cmd.exe and bash — no shell syntax involved):

    python tools/qa/read_outcome_logs.py                # Debug database, the default
    python tools/qa/read_outcome_logs.py <path-to-.db>  # any other database

Runnable from any working directory: the default path is resolved from this
file's location, not from the caller's cwd.

Companion to docs/plans/2026-08-26-dfd9a-instrumentation-runbook.md §2.
Opens the database read-only, so it is safe to run against a live file.
"""

import datetime
import os
import pathlib
import sqlite3
import sys

DEFAULT_DB = os.path.join(
    "SmartStudyPlanner", "bin", "Debug", "net10.0-windows10.0.19041.0", "SmartStudyData.db"
)
COLUMNS = ("CreatedUtc", "ActualMinutes", "PredictedMinutes", "WasMlPrediction", "Confidence")
ROW_FORMAT = "%-22s %8s %10s %6s %11s"


def repo_root():
    # tools/qa/read_outcome_logs.py -> repository root
    return os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def resolve(argv):
    if len(argv) > 1:
        return os.path.abspath(argv[1])
    return os.path.join(repo_root(), DEFAULT_DB)


def main(argv):
    path = resolve(argv)

    # Provenance first: three SmartStudyData.db files exist in this repository, and
    # reading the wrong one produces a confident, wrong verdict.
    print("FILE :", path)
    if not os.path.exists(path):
        print("MTIME: -")
        print()
        print("ERROR: no database at that path.")
        print("       If the app has never been run from this build, the file does not")
        print("       exist yet. Launch it once, then re-run this command.")
        return 2
    print("MTIME:", datetime.datetime.fromtimestamp(os.path.getmtime(path)))

    # Build the read-only URI with as_uri(), never by string-concatenating the path.
    # Two ways hand-rolling it fails on Windows, both SILENTLY, and both produce an
    # empty result that reads like a real finding rather than a broken instrument:
    #   "file:D:/x.db"  -> not a valid URI; SQLite opens an empty database, 0 tables
    #   ".../C#/..."    -> the '#' starts a URI fragment and truncates the path
    con = sqlite3.connect(pathlib.Path(path).as_uri() + "?mode=ro", uri=True)
    try:
        table_count = con.execute(
            "select count(*) from sqlite_master where type='table'"
        ).fetchone()[0]
        if table_count == 0:
            print()
            print("ERROR: opened, but the database reports zero tables.")
            print("       Treat this as a broken instrument, NOT as an observation about")
            print("       the app -- a real SmartStudyData.db has ~9 tables. Do not record")
            print("       a verdict from this run.")
            return 3

        exists = con.execute(
            "select count(*) from sqlite_master where type='table' and name='StudyTimeOutcomeLogs'"
        ).fetchone()[0]
        if not exists:
            print()
            print("ERROR: table StudyTimeOutcomeLogs is not in this database,")
            print("       though %d other tables are. Wrong file, or a schema older" % table_count)
            print("       than the outcome-log migration.")
            return 2

        rows = con.execute(
            "select %s from StudyTimeOutcomeLogs order by CreatedUtc" % ", ".join(COLUMNS)
        ).fetchall()
    finally:
        con.close()

    print("ROWS :", len(rows))
    print(ROW_FORMAT % ("CreatedUtc", "Actual", "Predicted", "WasML", "Confidence"))
    for created, actual, predicted, was_ml, confidence in rows:
        cell = lambda v: "NULL" if v is None else v
        print(ROW_FORMAT % (str(created)[:22], cell(actual), cell(predicted),
                            cell(was_ml), cell(confidence)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
