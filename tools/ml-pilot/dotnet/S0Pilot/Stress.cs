using System.Text.Json;

namespace S0Pilot;

/// <summary>
/// Whitespace/edge stress diff. The DAT-05 set is the contract surface; this
/// corpus exists because the first tokenization diff exposed a
/// trailing-whitespace divergence the realistic fixtures happened not to
/// contain. Characterising a divergence honestly means probing the axis it
/// lives on.
/// </summary>
public static class Stress
{
    public static int Run(string[] args)
    {
        var path = Path.Combine(Arm.Repo, "tools", "ml-pilot", "results", args.Contains("--trimmed") ? "stress_tokens_trimmed.json" : "stress_tokens.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var cases = doc.RootElement.GetProperty("cases")
                       .EnumerateArray().Select(x => x.GetString()!).ToArray();

        foreach (var arm in Arm.All)
        {
            var refIds = doc.RootElement.GetProperty("arms").GetProperty(arm.Key)
                            .GetProperty("ids").EnumerateArray()
                            .Select(a => a.EnumerateArray().Select(x => x.GetInt32()).ToArray())
                            .ToArray();
            int ok = 0, okInner = 0, nInner = 0;
            var bad = new List<string>();
            for (int i = 0; i < cases.Length; i++)
            {
                var actual = arm.Encode(args.Contains("--trimmed") ? cases[i].Trim() : cases[i]);
                bool inner = cases[i].Length > 0 && cases[i].Trim() == cases[i];
                if (inner) nInner++;
                bool m = refIds[i].AsSpan().SequenceEqual(actual);
                if (inner && m) okInner++;
                if (m) ok++;
                else if (bad.Count < 12)
                    bad.Add($"{Show(cases[i]),-22} exp [{string.Join(",", refIds[i])}]  " +
                            $"act [{string.Join(",", actual)}]");
                else bad.Add("");
            }
            int nbad = cases.Length - ok;
            Console.WriteLine($"\n=== {arm.Key} {arm.Label}: {ok}/{cases.Length} match, {nbad} divergent ===");
            Console.WriteLine($"    cases with NO leading/trailing whitespace: {okInner}/{nInner} match");
            foreach (var b in bad.Where(b => b.Length > 0)) Console.WriteLine("   " + b);
            if (nbad > 12) Console.WriteLine($"   … and {nbad - 12} more");
        }
        return 0;
    }

    private static string Show(string s) =>
        "\"" + s.Replace("\t", "\t").Replace("\n", "\n").Replace("\r", "\r") + "\"";
}
