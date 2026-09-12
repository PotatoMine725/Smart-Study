using System.Text.Json;
using System.Text.Json.Nodes;

namespace S0Pilot;

/// <summary>
/// EVA-08 output 6 / TOK-02 / TOK-04 / TOK-05: is there a workable, VERIFIED
/// tokenization route on the target runtime, per candidate?
///
/// Verified by loading the real vocabulary and diffing element-wise against the
/// candidate's own reference tokenizer -- never by reading a documentation page.
/// An arm with no verified route is REJECTED regardless of its accuracy.
/// </summary>
public static class TokCheck
{
    public static int Run(string[] args)
    {
        bool corrupt = args.Contains("--corrupt-vocab");
        bool noOffset = args.Contains("--no-offset");

        var refPath = Path.Combine(Arm.Repo, "tools", "ml-pilot", "results", "reference_tokens.json");
        var fixPath = Path.Combine(Arm.Repo, "datasheets", "vn_input_fixtures.csv");
        var fixtures = Fixtures.Load(fixPath);
        using var doc = JsonDocument.Parse(File.ReadAllText(refPath));
        var refArms = doc.RootElement.GetProperty("arms");

        var report = new JsonObject();
        int exitCode = 0;

        foreach (var arm in Arm.All)
        {
            var refArm = refArms.GetProperty(arm.Key);
            var refFix = refArm.GetProperty("fixtures");

            // The red demonstration: perturb the route and confirm the check
            // goes red. A guard whose pass is indistinguishable from a broken
            // guard is not a guard (plan §8.4).
            var probe = corrupt
                ? Corrupt(arm)
                : noOffset
                    ? WithOffset(arm, 0)
                    : arm;

            int match = 0, mismatch = 0;
            var byCat = new Dictionary<string, (int ok, int bad)>();
            var firstDiffs = new JsonArray();

            foreach (var f in fixtures)
            {
                var expected = refFix.GetProperty(f.Id).GetProperty("ids")
                                     .EnumerateArray().Select(x => x.GetInt32()).ToArray();
                int[] actual;
                try { actual = probe.Encode(f.Input); }
                catch (Exception e)
                {
                    actual = [];
                    if (firstDiffs.Count < 5)
                        firstDiffs.Add(new JsonObject
                        {
                            ["fixture"] = f.Id, ["category"] = f.Category,
                            ["error"] = $"{e.GetType().Name}: {e.Message}",
                        });
                }

                bool ok = expected.AsSpan().SequenceEqual(actual);
                var (o, b) = byCat.GetValueOrDefault(f.Category, (0, 0));
                byCat[f.Category] = ok ? (o + 1, b) : (o, b + 1);
                if (ok) match++;
                else
                {
                    mismatch++;
                    if (firstDiffs.Count < 5)
                    {
                        int at = FirstDiffIndex(expected, actual);
                        firstDiffs.Add(new JsonObject
                        {
                            ["fixture"] = f.Id,
                            ["category"] = f.Category,
                            ["input"] = Trim(f.Input),
                            ["first_divergence_index"] = at,
                            ["expected_len"] = expected.Length,
                            ["actual_len"] = actual.Length,
                            ["expected_head"] = Str(expected.Take(12)),
                            ["actual_head"] = Str(actual.Take(12)),
                        });
                    }
                }
            }

            bool verified = mismatch == 0;
            if (!verified && !corrupt && !noOffset) exitCode = 1;

            Console.WriteLine($"\n=== {arm.Key}  {arm.Label} ===");
            Console.WriteLine($"    route A source : {Path.GetFileName(arm.SpModelPath)} " +
                              $"(real vocabulary, {new FileInfo(arm.SpModelPath).Length / 1024} KB)");
            Console.WriteLine($"    id offset      : +{probe.IdOffset}   bos={probe.BosId} eos={probe.EosId}");
            Console.WriteLine($"    fixtures       : {match} match / {mismatch} mismatch  " +
                              $"({(verified ? "VERIFIED" : "DIVERGENT")})");
            foreach (var kv in byCat.OrderBy(k => k.Key))
                Console.WriteLine($"      {kv.Key,-14} ok={kv.Value.ok,-3} bad={kv.Value.bad}");
            foreach (var d in firstDiffs)
                Console.WriteLine($"      DIFF {d!["fixture"]} @{d["first_divergence_index"]}  " +
                                  $"exp {d["expected_head"]}  act {d["actual_head"]}");

            report[arm.Key] = new JsonObject
            {
                ["label"] = arm.Label,
                ["route_a_vocabulary"] = Path.GetFileName(arm.SpModelPath),
                ["route_b_in_graph_tokenization"] = false,
                ["id_offset_applied"] = probe.IdOffset,
                ["fixtures_total"] = fixtures.Count,
                ["fixtures_matching_reference"] = match,
                ["fixtures_diverging"] = mismatch,
                ["verified"] = verified,
                ["by_category"] = new JsonObject(byCat.OrderBy(k => k.Key).Select(kv =>
                    new KeyValuePair<string, JsonNode?>(kv.Key, new JsonObject
                    { ["ok"] = kv.Value.ok, ["bad"] = kv.Value.bad }))),
                ["first_divergences"] = firstDiffs,
            };
        }

        var suffix = corrupt ? "_corrupt" : noOffset ? "_nooffset" : "";
        var outPath = Path.Combine(Arm.Repo, "tools", "ml-pilot", "results",
                                   $"tokenization{suffix}.json");
        File.WriteAllText(outPath, report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"\nwrote {outPath}");
        return exitCode;
    }

    /// <summary>Deliberately corrupted vocabulary: proves the comparison can go red
    /// before any pass is trusted.</summary>
    private static Arm Corrupt(Arm a)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"corrupt_{a.Key}.model");
        var bytes = File.ReadAllBytes(a.SpModelPath);
        // Flip bytes deep inside the piece table -- a plausible-looking model that
        // yields different pieces, not a file that fails to parse.
        for (int i = 5000; i < Math.Min(bytes.Length, 5400); i++) bytes[i] ^= 0x5A;
        File.WriteAllBytes(tmp, bytes);
        return Clone(a, tmp, a.IdOffset);
    }

    private static Arm WithOffset(Arm a, int offset) => Clone(a, a.SpModelPath, offset);

    private static Arm Clone(Arm a, string sp, int offset) => new()
    {
        Key = a.Key, Label = a.Label, SpModelPath = sp, Prefix = a.Prefix,
        MaxLen = a.MaxLen, Rank = a.Rank, BosId = a.BosId, EosId = a.EosId,
        IdOffset = offset, SpAddBos = a.SpAddBos, SpAddEos = a.SpAddEos,
        HasSentenceEmbedding = a.HasSentenceEmbedding,
        NeedsTokenTypeIds = a.NeedsTokenTypeIds,
        OnnxFp32 = a.OnnxFp32, OnnxQuantized = a.OnnxQuantized,
    };

    private static int FirstDiffIndex(int[] e, int[] a)
    {
        int n = Math.Min(e.Length, a.Length);
        for (int i = 0; i < n; i++) if (e[i] != a[i]) return i;
        return e.Length == a.Length ? -1 : n;
    }

    private static string Str(IEnumerable<int> ids) => "[" + string.Join(",", ids) + "]";

    private static string Trim(string s) =>
        s.Length <= 48 ? s.Replace("\n", "\\n").Replace("\t", "\\t").Replace("\r", "\\r")
                       : s[..48].Replace("\n", "\\n") + "…";
}
