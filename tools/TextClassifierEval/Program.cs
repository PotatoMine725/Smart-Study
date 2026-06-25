using System.Globalization;
using System.Text;
using Microsoft.ML;
using Microsoft.ML.Data;

// §7.3 eval harness for docs/plans/2026-06-16-m8a-textclassifier-retrain.md.
// Stratified 80/20 per-class split, fits the EXACT prod pipeline
// (SmartStudyPlanner/Services/ML/TextClassifierModelManager.cs:147-150) and reports
// per-class recall/precision/support + micro/macro accuracy. Run with 1..2 seed CSVs.

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: dotnet run --project tools/TextClassifierEval -- <seedA.csv> [seedB.csv]");
    return 1;
}

foreach (var path in args)
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"file not found: {path}");
        return 1;
    }
    EvaluateSeed(path);
    Console.WriteLine();
}

return 0;

static void EvaluateSeed(string csvPath)
{
    string label = Path.GetFileNameWithoutExtension(csvPath);
    var rows = ParseCsv(csvPath);

    Console.WriteLine($"=== {label}  ({csvPath}) ===");
    Console.WriteLine($"total rows: {rows.Count}");

    // Class distribution (full seed).
    var dist = rows.GroupBy(r => r.TaskType).OrderBy(g => g.Key)
        .ToDictionary(g => g.Key, g => g.Count());
    Console.WriteLine("class distribution: " +
        string.Join(", ", dist.Select(kv => $"{kv.Key}={kv.Value}")));

    // Stratified 80/20 split, deterministic (Random(42)). Every class lands in train.
    var rng = new Random(42);
    var train = new List<Row>();
    var test = new List<Row>();
    foreach (var grp in rows.GroupBy(r => r.TaskType).OrderBy(g => g.Key))
    {
        var shuffled = grp.OrderBy(_ => rng.Next()).ToList();
        int nTest = (int)Math.Round(shuffled.Count * 0.2, MidpointRounding.AwayFromZero);
        nTest = Math.Min(nTest, shuffled.Count - 1); // keep >=1 in train
        test.AddRange(shuffled.Take(nTest));
        train.AddRange(shuffled.Skip(nTest));
    }
    Console.WriteLine($"train: {train.Count}  test: {test.Count}");

    // EXACT mirror of TextClassifierModelManager.TrainAndSaveAsync pipeline.
    var ml = new MLContext(seed: 42);
    var trainView = ml.Data.LoadFromEnumerable(train);
    var pipeline = ml.Transforms.Conversion.MapValueToKey("Label", "TaskType")
        .Append(ml.Transforms.Text.FeaturizeText("Features", "InputText"))
        .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
        .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
    var model = pipeline.Fit(trainView);

    var engine = ml.Model.CreatePredictionEngine<Row, Pred>(model);

    // Manual per-class tally by exact class name (avoids ConfusionMatrix label-order bugs).
    var classes = dist.Keys.OrderBy(k => k).ToList();
    var support = classes.ToDictionary(c => c, _ => 0);   // actual count in test
    var tp = classes.ToDictionary(c => c, _ => 0);        // predicted == actual
    var predicted = classes.ToDictionary(c => c, _ => 0); // predicted as this class
    int correct = 0;
    foreach (var r in test)
    {
        var p = engine.Predict(r);
        string pred = p.PredictedLabel ?? "";
        support[r.TaskType]++;
        if (predicted.ContainsKey(pred)) predicted[pred]++;
        if (pred == r.TaskType) { tp[r.TaskType]++; correct++; }
    }

    double micro = test.Count == 0 ? 0 : (double)correct / test.Count;
    double macro = classes.Average(c => support[c] == 0 ? 0 : (double)tp[c] / support[c]);

    Console.WriteLine();
    Console.WriteLine($"{"class",-22} {"support",7} {"recall",8} {"precision",10}");
    foreach (var c in classes)
    {
        double recall = support[c] == 0 ? 0 : (double)tp[c] / support[c];
        double precision = predicted[c] == 0 ? 0 : (double)tp[c] / predicted[c];
        Console.WriteLine($"{c,-22} {support[c],7} {recall,8:P1} {precision,10:P1}");
    }
    Console.WriteLine();
    Console.WriteLine($"MicroAccuracy (overall): {micro:P2}");
    Console.WriteLine($"MacroAccuracy (mean recall): {macro:P2}");
}

// Minimal CSV reader: same quote/"" convention as TextClassifierDatasetImporter.ParseCsvLine.
// Only InputText + TaskType are needed for this eval.
static List<Row> ParseCsv(string path)
{
    using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
    string? headerLine = reader.ReadLine()
        ?? throw new InvalidDataException("empty CSV: missing header");
    var header = ParseLine(headerLine).Select(h => h.Trim()).ToList();
    int idxInput = header.FindIndex(h => h.Equals("InputText", StringComparison.OrdinalIgnoreCase));
    int idxType = header.FindIndex(h => h.Equals("TaskType", StringComparison.OrdinalIgnoreCase));
    if (idxInput < 0 || idxType < 0)
        throw new InvalidDataException($"missing InputText/TaskType column. header: {string.Join(",", header)}");

    var rows = new List<Row>();
    string? line;
    while ((line = reader.ReadLine()) != null)
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        var f = ParseLine(line);
        string input = idxInput < f.Count ? f[idxInput] : "";
        string type = idxType < f.Count ? f[idxType] : "";
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(type)) continue;
        rows.Add(new Row { InputText = input, TaskType = type });
    }
    return rows;
}

static List<string> ParseLine(string line)
{
    var result = new List<string>();
    var sb = new StringBuilder();
    bool inQuotes = false;
    for (int i = 0; i < line.Length; i++)
    {
        char c = line[i];
        if (inQuotes)
        {
            if (c == '"')
            {
                if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else inQuotes = false;
            }
            else sb.Append(c);
        }
        else
        {
            if (c == '"') inQuotes = true;
            else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
    }
    result.Add(sb.ToString());
    return result;
}

class Row
{
    public string InputText { get; set; } = "";
    public string TaskType { get; set; } = "";
}

class Pred
{
    [ColumnName("PredictedLabel")]
    public string? PredictedLabel { get; set; }
}
