#!/usr/bin/env node
// PreToolUse hook: block Edit/Write on ML training-data/model-artifact files
// so a change to them requires an explicit, visible decision instead of
// happening as a silent side effect of an unrelated task. Exit code 2 blocks
// the tool call and hands the stderr text back to the assistant as the reason.
const GUARDED = [
  /seed_intents\.csv$/i,
  /\.onnx(_data)?$|\.safetensors$|\.gguf$|\.tflite$|\.pt$|\.pth$/i,
];

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
  if (!filePath) process.exit(0);

  const hit = GUARDED.find((re) => re.test(filePath));
  if (hit) {
    console.error(
      `Blocked: "${filePath}" looks like ML training data or a model artifact. ` +
        `This repo has an unresolved confidence-gate calibration issue tied to this ` +
        `pipeline (M8-A) — confirm with the user before editing this file.`
    );
    process.exit(2);
  }
  process.exit(0);
});
