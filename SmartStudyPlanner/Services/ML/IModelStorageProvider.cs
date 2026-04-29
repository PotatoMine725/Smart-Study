using System.IO;

namespace SmartStudyPlanner.Services.ML
{
    public interface IModelStorageProvider
    {
        string BaseDirectory { get; }
        string ModelZipPath { get; }
        string MetaPath { get; }
        Stream OpenReadModel();
        Stream OpenWriteModel();
        Stream OpenReadMeta();
        Stream OpenWriteMeta();
        bool ModelExists();
        bool MetaExists();
    }
}
