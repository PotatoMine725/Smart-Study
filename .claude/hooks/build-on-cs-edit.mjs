#!/usr/bin/env node
// PostToolUse hook: after an Edit/Write touches a .cs file, run an incremental
// build and surface only the error lines. Never blocks (always exits 0) —
// this is a fast feedback signal, not a gate; CI still has the real say.
import { execSync } from "node:child_process";

let input = "";
process.stdin.on("data", (c) => (input += c));
process.stdin.on("end", () => {
  let payload;
  try {
    payload = JSON.parse(input || "{}");
  } catch {
    process.exit(0);
  }

  const filePath = String(payload?.tool_input?.file_path ?? "");
  if (!filePath.toLowerCase().endsWith(".cs")) process.exit(0);

  try {
    execSync(
      "dotnet build SmartStudyPlanner.slnx --no-restore -v quiet -clp:ErrorsOnly",
      { cwd: process.cwd(), stdio: ["ignore", "pipe", "pipe"], timeout: 90000 }
    );
    process.exit(0);
  } catch (err) {
    const out = (err.stdout?.toString() ?? "") + (err.stderr?.toString() ?? "");
    const errorLines = out
      .split(/\r?\n/)
      .filter((l) => /error/i.test(l))
      .slice(0, 20);
    if (errorLines.length > 0) {
      console.log("dotnet build failed after this edit:\n" + errorLines.join("\n"));
    }
    process.exit(0);
  }
});
