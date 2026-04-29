using System;
using System.IO;

namespace SmartStudyPlanner.Services.ML
{
    public class LocalModelStorageProvider : IModelStorageProvider
    {
        public string BaseDirectory { get; }
        public string ModelZipPath => Path.Combine(BaseDirectory, "study_time.zip");
        public string MetaPath => Path.Combine(BaseDirectory, "meta.json");

        public LocalModelStorageProvider()
        {
            BaseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartStudyPlanner", "models");
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
