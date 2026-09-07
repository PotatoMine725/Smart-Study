using Microsoft.ML.Data;

namespace S0Pilot;

/// <summary>One row of the S0 split, read verbatim from tools/ml-pilot/split/.</summary>
public sealed class Row
{
    public string InputText { get; set; } = "";
    public string TaskType { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>Stable index within its own file. Persisted with every prediction so
    /// pooled-across-seed confidence bins can be de-pooled later (pilot README §2.2).</summary>
    public int RowId { get; set; }
}

/// <summary>
/// The row shape handed to the encoder arms. The vector column's size is set at
/// runtime through a <c>SchemaDefinition</c> -- ML.NET rejects an unknown-size
/// vector column, and the two arms have different ranks (768 / 384).
/// </summary>
public sealed class EmbRow
{
    public float[] Embedding { get; set; } = [];
    public string TaskType { get; set; } = "";
    public int RowId { get; set; }
}

public sealed class Pred
{
    [ColumnName("PredictedLabel")] public string PredictedLabel { get; set; } = "";
    [ColumnName("Score")] public float[] Score { get; set; } = [];
}

public static class Split
{
    public static List<Row> Load(string file)
    {
        var path = Path.Combine(Arm.Repo, "tools", "ml-pilot", "split", file);
        var text = File.ReadAllText(path, new System.Text.UTF8Encoding(false));
        var rows = ParseCsv(text);
        var h = rows[0];
        int iIn = Array.IndexOf(h, "InputText"), iT = Array.IndexOf(h, "TaskType"),
            iS = Array.IndexOf(h, "Source");
        return rows.Skip(1).Where(r => r.Length > iS)
                   .Select((r, i) => new Row
                   {
                       InputText = r[iIn], TaskType = r[iT], Source = r[iS], RowId = i,
                   })
                   .ToList();
    }

    private static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>(); var row = new List<string>();
        var cur = new System.Text.StringBuilder(); bool q = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (q)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cur.Append('"'); i++; } else q = false; }
                else cur.Append(c);
            }
            else if (c == '"') q = true;
            else if (c == ',') { row.Add(cur.ToString()); cur.Clear(); }
            else if (c == '\n') { row.Add(cur.ToString()); cur.Clear(); rows.Add(row.ToArray()); row = []; }
            else if (c != '\r') cur.Append(c);
        }
        if (cur.Length > 0 || row.Count > 0) { row.Add(cur.ToString()); rows.Add(row.ToArray()); }
        return rows.Where(r => r.Length > 1).ToList();
    }
}
