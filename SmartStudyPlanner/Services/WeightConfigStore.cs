using System;
using System.IO;
using System.Text.Json;

namespace SmartStudyPlanner.Services
{
    public static class WeightConfigStore
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        internal static string DefaultPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartStudyPlanner",
            "weight_config.json");

        public static WeightConfig Load() => LoadFrom(DefaultPath);

        public static void Save(WeightConfig config) => SaveTo(config, DefaultPath);

        internal static WeightConfig LoadFrom(string path)
        {
            try
            {
                if (!File.Exists(path)) return new WeightConfig();
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<WeightConfig>(json, JsonOpts) ?? new WeightConfig();
            }
            catch
            {
                return new WeightConfig();
            }
        }

        internal static void SaveTo(WeightConfig config, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOpts));
        }
    }
}
