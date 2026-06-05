using System;
using System.IO;
using System.Threading.Tasks;
using SmartStudyPlanner.Core.Parsing.Models;
using SmartStudyPlanner.Core.Parsing.Orchestrators;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Tests.Helpers;
using Xunit;

namespace SmartStudyPlanner.Tests.MLTests
{
    /// <summary>
    /// Slice 6 — the classifier wired into <see cref="ParsingOrchestrator"/> through the real
    /// adapter + confidence policy, using the embedded v3 seed model. Proves the two formerly
    /// mislabeled classes (ThiGiuaKy, DoAnCuoiKy) no longer regress, and that an unloaded model
    /// falls back to a deterministic heuristic parse.
    /// </summary>
    public class Slice6ParserIntegrationTests
    {
        [Fact]
        public async Task GiuaKy_And_DoAn_AreClassifiedCorrectly_NotRegressed()
        {
            string dir = NewTempDir();
            try
            {
                var manager = new TextClassifierModelManager(dir);
                await manager.InitializeAsync(); // trains from embedded v3 seed
                var orchestrator = new ParsingOrchestrator(
                    new FakeClock(DateTime.Now),
                    new IntentClassifierAdapter(new TextClassifierService(manager), new DefaultMlConfidencePolicy()));

                var giuaKy = orchestrator.Parse("Ôn thi giữa kỳ mai cực khó");
                var doAn = orchestrator.Parse("Làm đồ án cuối kỳ môn lập trình");

                Assert.Equal(LoaiCongViec.ThiGiuaKy, giuaKy.Loai);
                Assert.Equal(LoaiCongViec.DoAnCuoiKy, doAn.Loai);
                Assert.Equal(ParseSource.MlAugmented, giuaKy.Source);
                Assert.Equal(ParseSource.MlAugmented, doAn.Source);
            }
            finally { CleanupDir(dir); }
        }

        [Fact]
        public void ModelNotLoaded_FallsBackToHeuristic_ByteEqual()
        {
            var clock = new FakeClock(DateTime.Now);
            // Manager never initialized → service.IsModelLoaded is false → adapter returns null.
            var unloaded = new TextClassifierModelManager(NewTempDir());
            var withClassifier = new ParsingOrchestrator(
                clock,
                new IntentClassifierAdapter(new TextClassifierService(unloaded), new DefaultMlConfidencePolicy()));
            var heuristicOnly = new ParsingOrchestrator(clock);

            const string input = "Làm bài tập về nhà thứ 5 dễ";
            var a = withClassifier.Parse(input);
            var b = heuristicOnly.Parse(input);

            Assert.Equal(ParseSource.Heuristic, a.Source);
            Assert.Equal(b.Loai, a.Loai);
            Assert.Equal(b.DoKho, a.DoKho);
            Assert.Equal(b.TenTask, a.TenTask);
            Assert.Null(a.Confidence);
        }

        private static string NewTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ssp_s6_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void CleanupDir(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
