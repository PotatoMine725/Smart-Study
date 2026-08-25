using System.Text;

namespace S0Pilot;

/// <summary>
/// Reads the DAT-05 fixture set. Mirrors <c>tools/ml-pilot/fixtures.py</c> --
/// same escaping convention, same file. DAT-05 requires ONE set; two readers
/// that disagree about escaping would recreate the fork it exists to prevent.
/// </summary>
public sealed record Fixture(string Id, string Category, string PairId, string Input, string Note);

public static class Fixtures
{
    public static string Unescape(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char c = s[i + 1];
                char? m = c switch { 't' => '\t', 'r' => '\r', 'n' => '\n', '\\' => '\\', _ => null };
                if (m is not null) { sb.Append(m.Value); i++; continue; }
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    public static List<Fixture> Load(string path)
    {
        // The file is BOM-less UTF-8 and every field is quoted (QUOTE_ALL).
        var text = new UTF8Encoding(false).GetString(File.ReadAllBytes(path));
        var rows = ParseCsv(text);
        var header = rows[0];
        int Ix(string n) => Array.IndexOf(header, n);
        int iId = Ix("Id"), iCat = Ix("Category"), iPair = Ix("PairId"),
            iIn = Ix("Input"), iNote = Ix("Note");
        if (iId < 0 || iCat < 0 || iIn < 0)
            throw new InvalidDataException($"unexpected fixture header: {string.Join(",", header)}");

        return rows.Skip(1)
                   .Where(r => r.Length > iNote)
                   .Select(r => new Fixture(r[iId], r[iCat], r[iPair],
                                            Unescape(r[iIn]), r[iNote]))
                   .ToList();
    }

    /// <summary>Minimal RFC-4180 reader. The fixture file has no embedded newlines
    /// precisely because every Input is escaped, but quoted commas are everywhere.</summary>
    private static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cur = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cur.Append('"'); i++; }
                    else inQuotes = false;
                }
                else cur.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { row.Add(cur.ToString()); cur.Clear(); }
            else if (c == '\n')
            {
                row.Add(cur.ToString()); cur.Clear();
                rows.Add(row.ToArray()); row = new List<string>();
            }
            else if (c != '\r') cur.Append(c);
        }
        if (cur.Length > 0 || row.Count > 0)
        {
            row.Add(cur.ToString());
            rows.Add(row.ToArray());
        }
        return rows.Where(r => r.Length > 1).ToList();
    }
}
