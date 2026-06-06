using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services.ML
{
    /// <summary>
    /// Parses the TextClassifier CSV (seed or user-provided) into <see cref="TextClassifierInput"/> rows.
    /// Fails fast (throws <see cref="InvalidDataException"/>) if any required column is missing from the header.
    /// </summary>
    public static class TextClassifierDatasetImporter
    {
        private static readonly string[] RequiredColumns =
        {
            "InputText", "TaskType", "Difficulty", "DeadlineHint"
        };

        /// <summary>
        /// Parses CSV content from <paramref name="stream"/>. The stream is read as UTF-8.
        /// </summary>
        public static List<TextClassifierInput> Parse(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

            string? headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
                throw new InvalidDataException("TextClassifier CSV is empty: missing header row.");

            List<string> header = ParseCsvLine(headerLine).Select(h => h.Trim()).ToList();

            // Fail fast on any missing required column.
            string[] missing = RequiredColumns
                .Where(req => !header.Contains(req, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidDataException(
                    $"TextClassifier CSV is missing required column(s): {string.Join(", ", missing)}. " +
                    $"Header was: {string.Join(", ", header)}.");
            }

            int idxInputText = IndexOf(header, "InputText");
            int idxTaskName = IndexOf(header, "TaskName");
            int idxTaskType = IndexOf(header, "TaskType");
            int idxDifficulty = IndexOf(header, "Difficulty");
            int idxDeadlineHint = IndexOf(header, "DeadlineHint");
            int idxSource = IndexOf(header, "Source");
            int idxLabelVersion = IndexOf(header, "LabelVersion");

            var rows = new List<TextClassifierInput>();
            string? line;
            int lineNo = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNo++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                List<string> fields = ParseCsvLine(line);

                string difficultyRaw = Get(fields, idxDifficulty);
                float difficulty = 0f;
                if (!string.IsNullOrWhiteSpace(difficultyRaw) &&
                    !float.TryParse(difficultyRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out difficulty))
                {
                    throw new InvalidDataException(
                        $"TextClassifier CSV: could not parse Difficulty value '{difficultyRaw}' on line {lineNo}.");
                }

                rows.Add(new TextClassifierInput
                {
                    InputText = Get(fields, idxInputText),
                    TaskName = NullIfEmpty(Get(fields, idxTaskName)),
                    TaskType = Get(fields, idxTaskType),
                    Difficulty = difficulty,
                    DeadlineHint = NullIfEmpty(Get(fields, idxDeadlineHint)),
                    Source = NullIfEmpty(Get(fields, idxSource)),
                    LabelVersion = NullIfEmpty(Get(fields, idxLabelVersion)),
                });
            }

            return rows;
        }

        private static int IndexOf(List<string> header, string col)
            => header.FindIndex(h => string.Equals(h, col, StringComparison.OrdinalIgnoreCase));

        private static string Get(List<string> fields, int idx)
            => (idx >= 0 && idx < fields.Count) ? fields[idx] : string.Empty;

        private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

        /// <summary>
        /// Minimal RFC-4180-ish CSV line parser: handles quoted fields containing commas and
        /// escaped double-quotes (""). Sufficient for the seed dataset and simple user input.
        /// </summary>
        private static List<string> ParseCsvLine(string line)
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
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++; // skip escaped quote
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            result.Add(sb.ToString());
            return result;
        }
    }
}
