namespace S0Pilot;

/// <summary>
/// Proves the encoder is actually encoding meaning before any null result is
/// trusted. A broken embedder -- zero vectors, a wrong prefix, a mis-pooled
/// tensor -- produces exactly the same "no improvement over baseline" verdict as
/// a working encoder that genuinely does not help, and the two conclusions are
/// nothing alike.
///
/// The DAT-05 diacritics/stripped pairs make this measurable rather than
/// impressionistic: each pair is one meaning written two ways, so a working
/// encoder must place the pair closer together than it places unrelated inputs.
/// That is also AC-30's premise (preprocessing independence).
/// </summary>
public static class Sanity
{
    public static int Run(string[] args)
    {
        bool quantized = args.Contains("--quantized");
        var fixtures = Fixtures.Load(Path.Combine(Arm.Repo, "datasheets", "vn_input_fixtures.csv"));
        var pairs = fixtures.Where(f => f.Category is "diacritics" or "stripped")
                            .GroupBy(f => f.PairId)
                            .Where(g => g.Count() == 2).ToList();

        foreach (var arm in Arm.All)
        {
            using var emb = new Embedder(arm, quantized);
            Console.WriteLine($"\n=== {arm.Key} {arm.Label} ({(quantized ? "quantized" : "fp32")}) ===");

            var vecs = pairs.ToDictionary(
                g => g.Key,
                g => (dia: emb.Embed(g.First(x => x.Category == "diacritics").Input),
                      str: emb.Embed(g.First(x => x.Category == "stripped").Input)));

            // Degenerate-output check: an all-zero or constant vector would sail
            // through a cosine test between identical inputs.
            var any = vecs.Values.First().dia;
            double mag = Math.Sqrt(any.Sum(x => (double)x * x));
            int distinct = any.Distinct().Count();
            Console.WriteLine($"    vector rank {any.Length}  L2 {mag:F4}  distinct components {distinct}/{any.Length}");
            if (mag < 1e-6 || distinct < 10)
            {
                Console.WriteLine("    *** DEGENERATE EMBEDDING -- any accuracy result is void ***");
                return 1;
            }

            // Determinism (BEH-05): identical input must yield an identical vector.
            var again = emb.Embed(pairs[0].First(x => x.Category == "diacritics").Input);
            double maxDelta = vecs[pairs[0].Key].dia.Zip(again, (a, b) => Math.Abs(a - b)).Max();
            Console.WriteLine($"    reproducibility: max |delta| over a repeat run = {maxDelta:E2}");

            // A cosine-magnitude comparison is the WRONG test here and was tried
            // first: all eight fixtures are same-domain Vietnamese student task
            // text, so unrelated pairs are legitimately similar and the absolute
            // numbers say almost nothing. The rank test is immune to that -- for
            // each diacritics input, is its OWN stripped partner the nearest of
            // all eight candidates? Chance is 1/8.
            int rank1 = 0;
            var ranks = new List<int>();
            foreach (var p in pairs)
            {
                var scored = pairs.Select(q => (key: q.Key, cos: Cos(vecs[p.Key].dia, vecs[q.Key].str)))
                                  .OrderByDescending(x => x.cos).ToList();
                int r = scored.FindIndex(x => x.key == p.Key) + 1;
                ranks.Add(r);
                if (r == 1) rank1++;
                Console.WriteLine($"      {p.Key}  partner rank {r}/{pairs.Count}  " +
                                  $"cos(self)={Cos(vecs[p.Key].dia, vecs[p.Key].str):F4}  " +
                                  $"best={scored[0].cos:F4}");
            }
            Console.WriteLine($"    partner retrieved at rank 1: {rank1}/{pairs.Count} " +
                              $"(chance = {1.0 / pairs.Count:P0});  mean rank {ranks.Average():F2}");
            Console.WriteLine(rank1 >= pairs.Count - 1
                ? "    => stripping diacritics preserves the encoded meaning: WORKING"
                : rank1 * 2 > pairs.Count
                    ? "    => mostly working, some pairs confused"
                    : "    => at or near chance: the encoder is NOT encoding meaning here");
        }
        return 0;
    }

    private static double Cos(float[] a, float[] b)
    {
        double d = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++) { d += (double)a[i] * b[i]; na += (double)a[i] * a[i]; nb += (double)b[i] * b[i]; }
        return d / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-12);
    }
}
