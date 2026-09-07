using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace S0Pilot;

/// <summary>
/// EVA-08 outputs 3, 4 and 5, on the stack that ships (EVA-09).
///
/// The protocol is the one PRE-REGISTERED in tools/ml-pilot/README.md §2.1,
/// before any number existed: warm only, 20 discarded warm-up iterations, 200
/// samples, p50/p95/max reported, p95 compared against the ceiling, and NO
/// outlier removal of any kind.
///
/// PRF-05's boundary is not re-opened here: tokenization + forward pass are
/// inside it, model load is outside it and is reported separately as output 3.
/// </summary>
public static class Runtime
{
    private const int WarmupDiscard = 20;
    private const int Samples = 200;
    private const int ColdRuns = 5;

    public static int Run(string[] args)
    {
        if (args.Length > 1 && args[1] == "--coldload-child")
            return ColdLoadChild(args[2], args.Contains("--quantized"));

        bool quantized = args.Contains("--quantized");
        var target = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : "all";
        var arms = target == "all" ? Arm.All : [Arm.ByKey(target)];

        var machine = Machine();
        Console.WriteLine("=== machine (EVA-10) ===");
        foreach (var kv in machine) Console.WriteLine($"    {kv.Key,-22} {kv.Value}");
        Console.WriteLine("""
                *** NOT the PRF-01 reference class. This is UQ-1: the reference class is a
                    10th-gen Intel U-series / 8 GB / integrated-graphics laptop. PRF-03 forbids
                    treating a developer-machine number as the product floor, so outputs 3/4/5
                    below are valid ONLY as a one-directional bound -- they can establish a
                    FAILURE of the 500 ms ceiling, never a pass. Owner's call at CP1.
                """);

        var fixtures = Fixtures.Load(Path.Combine(Arm.Repo, "datasheets", "vn_input_fixtures.csv"));
        var realistic = fixtures.Where(f => f.Category is "diacritics" or "stripped"
                                         or "runtogether" or "abbrev").ToList();

        foreach (var arm in arms)
        {
            var result = Measure(arm, quantized, realistic, fixtures, machine);
            var dest = Path.Combine(Arm.Repo, "tools", "ml-pilot", "results",
                                    $"runtime_{arm.Key}{(quantized ? "_int8" : "")}.json");
            File.WriteAllText(dest, result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"    wrote {dest}\n");
        }
        return 0;
    }

    private static JsonObject Measure(Arm arm, bool quantized, List<Fixture> realistic,
                                      List<Fixture> all, Dictionary<string, string> machine)
    {
        Console.WriteLine($"\n=== {arm.Key}  {arm.Label} ({(quantized ? "quantized int8" : "fp32")}) ===");

        // --- output 3: cold-start load, in FRESH PROCESSES ---------------------
        var cold = new List<double>();
        var exe = Environment.ProcessPath!;
        for (int i = 0; i < ColdRuns; i++)
        {
            var psi = new ProcessStartInfo(exe)
            {
                RedirectStandardOutput = true, UseShellExecute = false,
            };
            psi.ArgumentList.Add("runtime");
            psi.ArgumentList.Add("--coldload-child");
            psi.ArgumentList.Add(arm.Key);
            if (quantized) psi.ArgumentList.Add("--quantized");
            using var p = Process.Start(psi)!;
            var line = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            cold.Add(double.Parse(line, System.Globalization.CultureInfo.InvariantCulture));
        }
        cold.Sort();
        Console.WriteLine($"    output 3  cold load (fresh process, n={ColdRuns}): " +
                          $"median {cold[ColdRuns / 2]:F0} ms  min {cold[0]:F0}  max {cold[^1]:F0}");

        // --- outputs 4 and 5: warm latency + peak RSS -------------------------
        using var emb = new Embedder(arm, quantized);
        for (int i = 0; i < WarmupDiscard; i++) emb.Embed(realistic[i % realistic.Count].Input);

        var lat = new List<double>(Samples);
        var sw = new Stopwatch();
        for (int i = 0; i < Samples; i++)
        {
            var text = realistic[i % realistic.Count].Input;
            sw.Restart();
            emb.Embed(text);                        // tokenization + forward pass: the PRF-05 boundary
            sw.Stop();
            lat.Add(sw.Elapsed.TotalMilliseconds);
        }
        var sorted = lat.OrderBy(x => x).ToList();
        double p50 = Pct(sorted, 0.50), p95 = Pct(sorted, 0.95), max = sorted[^1];
        Console.WriteLine($"    output 4  warm latency (n={Samples}, {WarmupDiscard} discarded, no trimming): " +
                          $"p50 {p50:F1} ms  p95 {p95:F1} ms  max {max:F1} ms");
        Console.WriteLine($"              p95 vs the 500 ms ceiling (PD-12): " +
                          (p95 < 500 ? $"UNDER by {500 - p95:F0} ms — but see UQ-1: this machine cannot establish a pass"
                                     : $"OVER by {p95 - 500:F0} ms — decisive, since a slower reference machine can only be worse"));

        // Named cases, reported separately rather than blended into the
        // percentile (pilot README §2.1, amended 2026-08-25).
        var named = new JsonObject();
        foreach (var cat in new[] { "empty", "pathological" })
        {
            var arr = new JsonArray();
            foreach (var f in all.Where(x => x.Category == cat))
            {
                sw.Restart(); emb.Embed(f.Input); sw.Stop();
                arr.Add(new JsonObject
                {
                    ["fixture"] = f.Id, ["input_chars"] = f.Input.Length,
                    ["ms"] = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                });
            }
            named[cat] = arr;
            Console.WriteLine($"              {cat,-13} " + string.Join("  ",
                arr.Select(x => $"{x!["fixture"]}({x["input_chars"]}ch)={x["ms"]}ms")));
        }

        var proc = Process.GetCurrentProcess();
        proc.Refresh();
        double peakMb = proc.PeakWorkingSet64 / 1048576.0;
        Console.WriteLine($"    output 5  peak resident memory: {peakMb:F0} MB " +
                          $"= {peakMb / 8192 * 100:F1}% of the 8 GB PRF-01 budget");
        Console.WriteLine("              NO ceiling asserted (PRF-08) — derived at S4 (OP-4)");

        // --- output 8: packaged size ------------------------------------------
        var (encBytes, tokBytes, files) = PackagedSize(arm, quantized);
        Console.WriteLine($"    output 8  packaged: encoder {encBytes / 1048576.0:F1} MB + " +
                          $"tokenizer {tokBytes / 1048576.0:F1} MB = " +
                          $"{(encBytes + tokBytes) / 1048576.0:F1} MB");

        return new JsonObject
        {
            ["_note"] = "EVA-08 outputs 3, 4, 5 and 8, measured on the .NET stack the product " +
                        "would use (EVA-09): Microsoft.ML.OnnxRuntime InferenceSession + the " +
                        "real SentencePiece tokenizer.",
            ["arm"] = arm.Key,
            ["label"] = arm.Label,
            ["precision"] = quantized ? "quantized (int8)" : "fp32",
            ["onnx_file"] = Path.GetFileName(emb.ModelPath),
            ["execution_provider"] = "CPU (PRF-02, AST-07)",
            ["machine"] = new JsonObject(machine.Select(kv =>
                new KeyValuePair<string, JsonNode?>(kv.Key, kv.Value))),
            ["machine_is_prf01_reference_class"] = false,
            ["machine_caveat"] = "UQ-1. Materially faster than the PRF-01 class (10th-gen Intel " +
                                 "U-series, 8 GB, integrated graphics). PRF-03 forbids treating a " +
                                 "developer-machine number as the product floor, so outputs 3/4/5 " +
                                 "are valid ONLY as a one-directional bound: they can establish a " +
                                 "FAILURE of the 500 ms ceiling, never a pass. Owner's call at CP1.",
            ["protocol"] = new JsonObject
            {
                ["pre_registered_in"] = "tools/ml-pilot/README.md §2.1, before any number existed (PRF-06)",
                ["warm_or_cold"] = "warm (PRF-04: 'with the model already loaded')",
                ["warmup_iterations_discarded"] = WarmupDiscard,
                ["samples"] = Samples,
                ["outlier_handling"] = "none — no trimming, no winsorising",
                ["boundary"] = "PRF-05: tokenization + encoder forward pass; model load EXCLUDED",
                ["inputs"] = "DAT-05 realistic categories, cycled deterministically; empty and " +
                             "pathological reported as named cases",
            },
            ["output_3_cold_load_ms"] = new JsonObject
            {
                ["fresh_processes"] = ColdRuns,
                ["median"] = Math.Round(cold[ColdRuns / 2], 1),
                ["min"] = Math.Round(cold[0], 1), ["max"] = Math.Round(cold[^1], 1),
            },
            ["output_4_warm_latency_ms"] = new JsonObject
            {
                ["p50"] = Math.Round(p50, 2), ["p95"] = Math.Round(p95, 2),
                ["max"] = Math.Round(max, 2),
                ["ceiling_ms"] = 500,
                ["p95_under_ceiling_on_this_machine"] = p95 < 500,
                ["named_cases"] = named,
            },
            ["output_5_peak_resident_mb"] = new JsonObject
            {
                ["peak_working_set_mb"] = Math.Round(peakMb, 1),
                ["budget_mb"] = 8192,
                ["pct_of_budget"] = Math.Round(peakMb / 8192 * 100, 2),
                ["ceiling_asserted"] = false,
                ["note"] = "PRF-08: no ceiling asserted. Derived at S4 (OP-4) from this measurement.",
            },
            ["output_8_packaged_bytes"] = new JsonObject
            {
                ["encoder_bytes"] = encBytes, ["tokenizer_bytes"] = tokBytes,
                ["total_bytes"] = encBytes + tokBytes,
                ["total_mb"] = Math.Round((encBytes + tokBytes) / 1048576.0, 1),
                ["files"] = new JsonArray(files.Select(f => (JsonNode)f).ToArray()),
            },
        };
    }

    private static int ColdLoadChild(string armKey, bool quantized)
    {
        var t0 = Stopwatch.GetTimestamp();
        var arm = Arm.ByKey(armKey);
        using var emb = new Embedder(arm, quantized);
        emb.Embed("tgk");                            // ready to serve its first inference
        var ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        Console.WriteLine(ms.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        return 0;
    }

    private static (long enc, long tok, List<string> files) PackagedSize(Arm arm, bool quantized)
    {
        var onnx = quantized ? arm.OnnxQuantized : arm.OnnxFp32;
        var files = new List<string>();
        long enc = 0;
        foreach (var p in new[] { onnx, onnx + "_data" })
            if (File.Exists(p)) { enc += new FileInfo(p).Length; files.Add(Path.GetFileName(p)); }
        long tok = new FileInfo(arm.SpModelPath).Length;
        files.Add(Path.GetFileName(arm.SpModelPath));
        return (enc, tok, files);
    }

    private static double Pct(List<double> sorted, double q)
    {
        // Nearest-rank. No interpolation, so the reported value is one that was
        // actually observed.
        int i = (int)Math.Ceiling(q * sorted.Count) - 1;
        return sorted[Math.Clamp(i, 0, sorted.Count - 1)];
    }

    private static Dictionary<string, string> Machine()
    {
        string Wmi(string cls, string prop)
        {
            try
            {
                var psi = new ProcessStartInfo("powershell")
                {
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
                };
                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-Command");
                psi.ArgumentList.Add($"(Get-CimInstance {cls}).{prop} | Select-Object -First 1");
                using var p = Process.Start(psi)!;
                var s = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
                return s;
            }
            catch { return "(unavailable)"; }
        }
        return new Dictionary<string, string>
        {
            ["cpu"] = Wmi("Win32_Processor", "Name"),
            ["logical_processors"] = Environment.ProcessorCount.ToString(),
            ["total_ram"] = Wmi("Win32_ComputerSystem", "TotalPhysicalMemory"),
            ["os"] = Wmi("Win32_OperatingSystem", "Caption"),
            ["os_build"] = Environment.OSVersion.Version.ToString(),
            ["runtime"] = Environment.Version.ToString(),
            ["onnxruntime"] = "Microsoft.ML.OnnxRuntime 1.29.0",
        };
    }
}
