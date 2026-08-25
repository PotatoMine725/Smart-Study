using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace S0Pilot;

/// <summary>
/// EVA-08 outputs 1 and 2, for every arm, through ONE harness.
///
/// EVA-05: every arm uses the SAME head family and the SAME split, so the
/// featurizer is the only variable. The head is `SdcaMaximumEntropy` throughout,
/// with the same hyperparameters -- the production pipeline's trainer. Only the
/// feature column differs: `FeaturizeText` for the baseline, the encoder vector
/// for the arms.
///
/// EVA-08 forbids a single headline accuracy figure. None is emitted here, in
/// the console output, or in the JSON -- a number that exists in a file ends up
/// in a summary.
/// </summary>
public static class Accuracy
{
    // Pre-registered in tools/ml-pilot/README.md §2.2 BEFORE any number was
    // measured. 42 is the shipped production seed and must be included.
    public static readonly int[] Seeds = [42, 1337, 2026, 7, 99];

    private static readonly double[] BinEdges = [0.0, 0.2, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0001];

    public static int Run(string[] args)
    {
        var which = args.Length > 1 ? args[1] : "all";
        var train = Split.Load("train.csv");
        var test = Split.Load("test.csv");
        Console.WriteLine($"split: train={train.Count} test={test.Count}");

        var arms = new List<string>();
        if (which is "all" or "baseline") arms.Add("baseline");
        if (which is "all" or "arm_a") arms.Add("arm_a");
        if (which is "all" or "arm_b") arms.Add("arm_b");

        foreach (var key in arms)
        {
            var result = key == "baseline"
                ? RunBaseline(train, test)
                : RunEncoderArm(Arm.ByKey(key), train, test, args.Contains("--quantized"));
            var dest = Path.Combine(Arm.Repo, "tools", "ml-pilot", "results", $"{key}{(args.Contains("--quantized") && key != "baseline" ? "_int8" : "")}.json");
            File.WriteAllText(dest, result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"wrote {dest}\n");
        }
        return 0;
    }

    private static JsonObject RunBaseline(List<Row> train, List<Row> test)
    {
        Console.WriteLine("\n=== baseline — current production n-gram featurizer ===");
        var perSeed = new JsonArray();
        var raw = new JsonArray();

        foreach (var seed in Seeds)
        {
            // Reproduces TextClassifierModelManager.TrainAndSaveAsync exactly,
            // INCLUDING MLContext(seed:) -- an unseeded baseline manufactures
            // run-to-run variance and corrupts the EVA-16 kill criterion.
            var ml = new MLContext(seed: seed);
            var dv = ml.Data.LoadFromEnumerable(train);
            var pipeline = ml.Transforms.Conversion.MapValueToKey("Label", "TaskType")
                .Append(ml.Transforms.Text.FeaturizeText("Features", "InputText"))
                .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
            var model = pipeline.Fit(dv);

            var scored = model.Transform(ml.Data.LoadFromEnumerable(test));
            var preds = ml.Data.CreateEnumerable<Pred>(scored, reuseRowObject: false).ToList();
            perSeed.Add(Evaluate(seed, test, preds, raw));
        }
        return Assemble("baseline", "current production n-gram featurizer (FeaturizeText)",
                        null, perSeed, raw, test);
    }

    private static JsonObject RunEncoderArm(Arm arm, List<Row> train, List<Row> test, bool quantized)
    {
        Console.WriteLine($"\n=== {arm.Key} — {arm.Label} ({(quantized ? "quantized" : "fp32")}) ===");
        using var emb = new Embedder(arm, quantized);
        Console.WriteLine($"    model : {Path.GetFileName(emb.ModelPath)}");
        Console.WriteLine($"    prefix: \"{arm.Prefix}\"   rank: {arm.Rank}");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var trainE = Embed(emb, train);
        var testE = Embed(emb, test);
        Console.WriteLine($"    embedded {train.Count + test.Count} rows in {sw.Elapsed.TotalSeconds:F1}s");

        // ML.NET rejects an unknown-size vector column; the rank must be declared.
        var sd = SchemaDefinition.Create(typeof(EmbRow));
        sd["Embedding"].ColumnType = new VectorDataViewType(NumberDataViewType.Single, arm.Rank);

        var perSeed = new JsonArray();
        var raw = new JsonArray();
        foreach (var seed in Seeds)
        {
            var ml = new MLContext(seed: seed);
            var dv = ml.Data.LoadFromEnumerable(trainE, sd);
            // Identical head family and hyperparameters to the baseline (EVA-05).
            // Only "Features" changes: the encoder vector instead of FeaturizeText.
            var pipeline = ml.Transforms.Conversion.MapValueToKey("Label", "TaskType")
                .Append(ml.Transforms.CopyColumns("Features", "Embedding"))
                .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
            var model = pipeline.Fit(dv);

            var scored = model.Transform(ml.Data.LoadFromEnumerable(testE, sd));
            var preds = ml.Data.CreateEnumerable<Pred>(scored, reuseRowObject: false).ToList();
            perSeed.Add(Evaluate(seed, test, preds, raw));
        }

        var meta = new JsonObject
        {
            ["encoder"] = arm.Label,
            ["onnx_file"] = Path.GetFileName(emb.ModelPath),
            ["precision"] = quantized ? "quantized (int8)" : "fp32",
            ["rank"] = arm.Rank,
            ["prefix"] = arm.Prefix,
            ["prefix_source"] = "model card (see tools/ml-pilot/README.md §2.3)",
            ["tokenization_route"] = "Route A — Microsoft.ML.Tokenizers SentencePiece",
            ["id_offset"] = arm.IdOffset,
        };
        return Assemble(arm.Key, arm.Label, meta, perSeed, raw, test);
    }

    private static List<EmbRow> Embed(Embedder emb, List<Row> rows) =>
        rows.Select(r => new EmbRow
        {
            Embedding = emb.Embed(r.InputText), TaskType = r.TaskType, RowId = r.RowId,
        }).ToList();

    /// <summary>Per-class precision/recall (output 1) and the confidence relationship
    /// (output 2) for one seed. Confidence is <c>Score.Max()</c> -- exactly what
    /// production reads (TextClassifierModelManager.Predict).</summary>
    private static JsonObject Evaluate(int seed, List<Row> test, List<Pred> preds, JsonArray raw)
    {
        var classes = test.Select(t => t.TaskType).Distinct().OrderBy(x => x).ToArray();
        var tp = classes.ToDictionary(c => c, _ => 0);
        var fp = classes.ToDictionary(c => c, _ => 0);
        var fn = classes.ToDictionary(c => c, _ => 0);
        var support = classes.ToDictionary(c => c, _ => 0);
        var bins = new int[BinEdges.Length - 1];
        var binCorrect = new int[BinEdges.Length - 1];

        for (int i = 0; i < test.Count; i++)
        {
            var actual = test[i].TaskType;
            var predicted = preds[i].PredictedLabel;
            double conf = preds[i].Score is { Length: > 0 } ? preds[i].Score.Max() : 0d;
            bool correct = predicted == actual;

            support[actual]++;
            if (correct) tp[actual]++;
            else
            {
                fn[actual]++;
                if (fp.ContainsKey(predicted)) fp[predicted]++;
            }

            int b = Bin(conf);
            bins[b]++;
            if (correct) binCorrect[b]++;

            // (row_id, seed, confidence, correct) -- NOT (confidence, correct).
            // Persisting the row id and the seed is what lets a later reader
            // de-pool the bins; WP-2.5 derives the shipped threshold from this.
            raw.Add(new JsonArray(test[i].RowId, seed, Math.Round(conf, 6), correct ? 1 : 0));
        }

        var perClass = new JsonObject();
        var f1s = new List<double>();
        foreach (var c in classes)
        {
            double p = tp[c] + fp[c] == 0 ? 0 : (double)tp[c] / (tp[c] + fp[c]);
            double r = tp[c] + fn[c] == 0 ? 0 : (double)tp[c] / (tp[c] + fn[c]);
            double f1 = p + r == 0 ? 0 : 2 * p * r / (p + r);
            f1s.Add(f1);
            perClass[c] = new JsonObject
            {
                ["precision"] = Math.Round(p, 4), ["recall"] = Math.Round(r, 4),
                ["f1"] = Math.Round(f1, 4), ["support"] = support[c],
                ["tp"] = tp[c], ["fp"] = fp[c], ["fn"] = fn[c],
            };
        }
        // Predictions into classes absent from the real subset are a real
        // behaviour and are reported, not discarded.
        var offClass = preds.Count(p => !classes.Contains(p.PredictedLabel));

        double macroF1 = f1s.Average();
        var binArr = new JsonArray();
        for (int i = 0; i < bins.Length; i++)
            binArr.Add(new JsonObject
            {
                ["lo"] = BinEdges[i], ["hi"] = Math.Min(BinEdges[i + 1], 1.0),
                ["n"] = bins[i],
                ["accuracy"] = bins[i] == 0 ? null : Math.Round((double)binCorrect[i] / bins[i], 4),
            });

        Console.WriteLine($"    seed {seed,-5} macro-F1 {macroF1:F4}   " +
                          string.Join("  ", classes.Select(c =>
                              $"{c[..Math.Min(4, c.Length)]} P{perClass[c]!["precision"]} R{perClass[c]!["recall"]}")));

        return new JsonObject
        {
            ["seed"] = seed,
            ["macro_f1"] = Math.Round(macroF1, 6),
            ["per_class"] = perClass,
            ["predictions_into_absent_classes"] = offClass,
            ["confidence_bins"] = binArr,
        };
    }

    private static int Bin(double c)
    {
        for (int i = 0; i < BinEdges.Length - 1; i++)
            if (c >= BinEdges[i] && c < BinEdges[i + 1]) return i;
        return BinEdges.Length - 2;
    }

    private static JsonObject Assemble(string key, string label, JsonObject? meta,
                                       JsonArray perSeed, JsonArray raw, List<Row> test)
    {
        var f1 = perSeed.Select(s => s!["macro_f1"]!.GetValue<double>()).ToArray();
        double mean = f1.Average();
        double sd = f1.Length < 2 ? 0
            : Math.Sqrt(f1.Sum(x => (x - mean) * (x - mean)) / (f1.Length - 1));

        Console.WriteLine($"    macro-F1 across {f1.Length} seeds: " +
                          $"mean {mean:F4}  min {f1.Min():F4}  max {f1.Max():F4}  SD {sd:F4}");

        return new JsonObject
        {
            ["_note"] = "EVA-08 outputs 1 and 2. NO headline accuracy figure appears here " +
                        "by requirement: results are per class, and macro-F1 is reported " +
                        "per seed with its spread because EVA-14 and EVA-16 are both " +
                        "defined relative to run-to-run variance.",
            ["arm"] = key,
            ["label"] = label,
            ["featurizer"] = meta,
            ["head"] = "SdcaMaximumEntropy (identical family and hyperparameters across every arm — EVA-05)",
            ["confidence_definition"] = "Score.Max() — exactly what production reads " +
                                        "(TextClassifierModelManager.Predict)",
            ["split"] = new JsonObject
            {
                ["source"] = "tools/ml-pilot/split/ — consumed verbatim, no re-split (EVA-04)",
                ["test_rows"] = test.Count,
            },
            ["seeds"] = new JsonArray(Seeds.Select(s => (JsonNode)s).ToArray()),
            ["macro_f1_across_seeds"] = new JsonObject
            {
                ["mean"] = Math.Round(mean, 6), ["min"] = Math.Round(f1.Min(), 6),
                ["max"] = Math.Round(f1.Max(), 6), ["sample_sd"] = Math.Round(sd, 6),
            },
            ["per_seed"] = perSeed,
            ["raw_predictions_note"] = "[row_id, seed, confidence, correct] — per-seed, so " +
                                       "pooled bins can be de-pooled. 205 rows x 5 seeds is " +
                                       "NOT 1025 independent samples.",
            ["raw_predictions"] = raw,
        };
    }
}
