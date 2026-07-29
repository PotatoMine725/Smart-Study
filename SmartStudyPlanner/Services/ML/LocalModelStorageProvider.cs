using System;
using System.IO;

namespace SmartStudyPlanner.Services.ML
{
    public class LocalModelStorageProvider : IModelStorageProvider
    {
        public string BaseDirectory { get; }
        public string ModelZipPath => Path.Combine(BaseDirectory, "study_time.zip");
        public string MetaPath => Path.Combine(BaseDirectory, "meta.json");

        public static string DefaultBaseDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SmartStudyPlanner", "models");

        // baseDirectory chỉ để test trỏ vào thư mục tạm; production luôn dùng default,
        // nên DI registration (ServiceLocator.cs) không phải đổi.
        public LocalModelStorageProvider(string? baseDirectory = null)
        {
            BaseDirectory = baseDirectory ?? DefaultBaseDirectory;
            Directory.CreateDirectory(BaseDirectory);
        }

        public bool ModelExists() => File.Exists(ModelZipPath);
        public bool MetaExists() => File.Exists(MetaPath);
        public Stream OpenReadModel() => File.OpenRead(ModelZipPath);
        public Stream OpenWriteModel() => File.Create(ModelZipPath);
        public Stream OpenReadMeta() => File.OpenRead(MetaPath);
        public Stream OpenWriteMeta() => File.Create(MetaPath);
    }
}
