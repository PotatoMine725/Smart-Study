using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.ML.Schema;
using Xunit;

namespace SmartStudyPlanner.Tests.MLTests
{
    /// <summary>
    /// Slice 5 (M8-A) — schema/importer/lifecycle/service coverage for the TextClassifier.
    /// Accuracy is intentionally out of scope here (that's Slice 6); we only assert that the
    /// pipeline stands up, the importer fails fast, and the service maps to <c>IntentPrediction</c>.
    /// </summary>
    public class TextClassifierSchemaTests
    {
        // ---- Importer ----

        [Fact]
        public void Importer_ParsesValidCsv()
        {
            const string csv =
                "InputText,TaskName,TaskType,Difficulty,DeadlineHint,Source,LabelVersion\n" +
                "Làm bài tập chương 1,BT,BaiTapVeNha,2,3 ngày,seed,v1\n" +
                "Ôn thi cuối kỳ môn Lý,Thi,ThiCuoiKy,4.5,2 tuần,seed,v1\n";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

            var rows = TextClassifierDatasetImporter.Parse(stream);

            Assert.Equal(2, rows.Count);
            Assert.Equal("Làm bài tập chương 1", rows[0].InputText);
            Assert.Equal("BaiTapVeNha", rows[0].TaskType);
            Assert.Equal(2f, rows[0].Difficulty);
            Assert.Equal("ThiCuoiKy", rows[1].TaskType);
            Assert.Equal(4.5f, rows[1].Difficulty);
        }

        [Fact]
        public void Importer_Throws_WhenTaskTypeColumnMissing()
        {
            const string csv =
                "InputText,Difficulty,DeadlineHint\n" +
                "Làm bài tập,2,3 ngày\n";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

            Assert.Throws<InvalidDataException>(() => TextClassifierDatasetImporter.Parse(stream));
        }

        [Fact]
        public void Importer_Throws_WhenDeadlineHintColumnMissing()
        {
            const string csv =
                "InputText,TaskType,Difficulty\n" +
                "Làm bài tập,BaiTapVeNha,2\n";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

            Assert.Throws<InvalidDataException>(() => TextClassifierDatasetImporter.Parse(stream));
        }

        // ---- Lifecycle ----

        [Fact]
        public async Task Manager_TrainsFromSeed_WhenNoModelOnDisk()
        {
            string dir = NewTempDir();
            try
            {
                var manager = new TextClassifierModelManager(dir);
                await manager.InitializeAsync();

                Assert.True(manager.IsReady);
                Assert.True(File.Exists(Path.Combine(dir, "text_classifier.zip")));
                Assert.True(File.Exists(Path.Combine(dir, "text_classifier_meta.json")));
            }
            finally { CleanupDir(dir); }
        }

        [Fact]
        public async Task Manager_LoadsExisting_OnSecondInitialize()
        {
            string dir = NewTempDir();
            try
            {
                var first = new TextClassifierModelManager(dir);
                await first.InitializeAsync();
                string zipPath = Path.Combine(dir, "text_classifier.zip");
                var writtenAt = File.GetLastWriteTimeUtc(zipPath);

                var second = new TextClassifierModelManager(dir);
                await second.InitializeAsync();

                Assert.True(second.IsReady);
                // Loaded the existing model rather than retraining → the zip was not rewritten.
                Assert.Equal(writtenAt, File.GetLastWriteTimeUtc(zipPath));
                // "Load if present" must yield a *usable* model, not just a flag flip:
                // the disk-deserialized model still predicts a valid intent.
                Assert.NotNull(new TextClassifierService(second).Predict("Ôn thi cuối kỳ môn Vật lý"));
            }
            finally { CleanupDir(dir); }
        }

        // ---- Service ----

        [Fact]
        public void Service_ReturnsNull_WhenModelNotLoaded()
        {
            var service = new TextClassifierService(new StubManager { Ready = false });

            Assert.False(service.IsModelLoaded);
            Assert.Null(service.Predict("bất kỳ chuỗi nào"));
        }

        [Fact]
        public void Service_ReturnsNull_OnBlankInput()
        {
            var service = new TextClassifierService(new StubManager { Ready = true });

            Assert.Null(service.Predict("   "));
        }

        [Fact]
        public async Task Service_PredictsIntent_WithRealSeedModel()
        {
            string dir = NewTempDir();
            try
            {
                var manager = new TextClassifierModelManager(dir);
                await manager.InitializeAsync();
                var service = new TextClassifierService(manager);

                var pred = service.Predict("Ôn thi cuối kỳ môn Vật lý");

                Assert.NotNull(pred);
                Assert.NotNull(pred!.Loai);                       // label maps to a valid LoaiCongViec
                Assert.True(pred.Confidence > 0 && pred.Confidence <= 1.0001);
                Assert.Null(pred.DoKho);                          // difficulty deferred (Slice 5)
            }
            finally { CleanupDir(dir); }
        }

        // ---- helpers ----

        private static string NewTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ssp_tc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void CleanupDir(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* best-effort cleanup of temp model files */ }
        }

        private sealed class StubManager : ITextClassifierModelManager
        {
            public bool Ready { get; set; }
            public bool IsReady => Ready;
            public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
            public Task RetrainAsync(IReadOnlyList<TextClassifierInput> data, CancellationToken ct = default) => Task.CompletedTask;
            public TextClassifierPrediction? Predict(TextClassifierInput input) => null;
        }
    }
}
