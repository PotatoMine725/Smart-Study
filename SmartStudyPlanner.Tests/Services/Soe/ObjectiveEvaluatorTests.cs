using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SmartStudyPlanner.Services.Soe;
using Xunit;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>
    /// T3.2 (Card E) — per-seam unit tests cho <see cref="IObjectiveEvaluator"/>/<see cref="ObjectiveEvaluator"/>.
    /// Mirror namespace 1:1 với <c>SmartStudyPlanner.Services.Soe</c> theo quy ước repo.
    /// Fixture xây tay (hand-built <see cref="ScheduledItem"/> list) theo đúng hướng dẫn của
    /// nhiệm vụ — không phụ thuộc <c>WorkloadServiceImpl</c>/allocator thật.
    /// </summary>
    public sealed class ObjectiveEvaluatorTests
    {
        private static readonly DateTime Day0 = new(2026, 8, 3);
        private static readonly SoeWeights UnitWeights = new(1.0, 1.0, 1.0, 1.0, 1.0);

        private static ScheduledItem Item(
            Guid maTask, DateTime date, int soPhut, string tenMon = "Toán", string tenTaskGoc = "T")
            => new(maTask, date.AddDays(30), tenTaskGoc, tenTaskGoc, tenMon, date, soPhut);

        private readonly IObjectiveEvaluator _sut = new ObjectiveEvaluator();

        // ---------- LoadBalance ----------

        [Fact]
        public void LoadBalance_EvenMinutesAcrossDays_ScoresPerfect()
        {
            var t1 = Guid.NewGuid();
            var schedule = new List<ScheduledItem>
            {
                Item(t1, Day0, 60),
                Item(t1, Day0.AddDays(1), 60),
                Item(t1, Day0.AddDays(2), 60),
            };

            var score = _sut.Evaluate(schedule, UnitWeights);

            Assert.Equal(1.0, score.LoadBalance, precision: 6);
        }

        [Fact]
        public void LoadBalance_HighlyUnevenMinutes_ScoresWorseThanEven()
        {
            var t1 = Guid.NewGuid();
            var even = new List<ScheduledItem>
            {
                Item(t1, Day0, 60),
                Item(t1, Day0.AddDays(1), 60),
                Item(t1, Day0.AddDays(2), 60),
            };
            var uneven = new List<ScheduledItem>
            {
                Item(t1, Day0, 10),
                Item(t1, Day0.AddDays(1), 170),
                Item(t1, Day0.AddDays(2), 0 + 1), // keep > 0 so the day still counts as "used"
            };

            var evenScore = _sut.Evaluate(even, UnitWeights);
            var unevenScore = _sut.Evaluate(uneven, UnitWeights);

            Assert.True(unevenScore.LoadBalance < evenScore.LoadBalance);
        }

        [Fact]
        public void LoadBalance_SingleDayUsed_IsVacuouslyPerfect()
        {
            var t1 = Guid.NewGuid();
            var schedule = new List<ScheduledItem> { Item(t1, Day0, 45) };

            var score = _sut.Evaluate(schedule, UnitWeights);

            Assert.Equal(1.0, score.LoadBalance, precision: 6);
        }

        // ---------- ContextContinuity ----------

        [Fact]
        public void ContextContinuity_OneSubjectPerDay_ScoresPerfect()
        {
            var t1 = Guid.NewGuid();
            var t2 = Guid.NewGuid();
            var schedule = new List<ScheduledItem>
            {
                Item(t1, Day0, 60, tenMon: "Toán"),
                Item(t2, Day0.AddDays(1), 60, tenMon: "Vật Lý"),
            };

            var score = _sut.Evaluate(schedule, UnitWeights);

            Assert.Equal(1.0, score.ContextContinuity, precision: 6);
        }

        [Fact]
        public void ContextContinuity_ManySubjectsSameDay_ScoresWorseThanOneSubjectPerDay()
        {
            var t1 = Guid.NewGuid();
            var t2 = Guid.NewGuid();
            var t3 = Guid.NewGuid();
            var t4 = Guid.NewGuid();

            var focused = new List<ScheduledItem>
            {
                Item(t1, Day0, 30, tenMon: "Toán"),
                Item(t2, Day0, 30, tenMon: "Toán"),
            };
            var scattered = new List<ScheduledItem>
            {
                Item(t1, Day0, 30, tenMon: "Toán"),
                Item(t2, Day0, 30, tenMon: "Vật Lý"),
                Item(t3, Day0, 30, tenMon: "Hóa Học"),
                Item(t4, Day0, 30, tenMon: "Văn Học"),
            };

            var focusedScore = _sut.Evaluate(focused, UnitWeights);
            var scatteredScore = _sut.Evaluate(scattered, UnitWeights);

            Assert.True(scatteredScore.ContextContinuity < focusedScore.ContextContinuity);
        }

        // ---------- SessionQuality ----------

        [Fact]
        public void SessionQuality_IdealLengthChunks_ScorePerfect()
        {
            var t1 = Guid.NewGuid();
            var schedule = new List<ScheduledItem>
            {
                Item(t1, Day0, 45),
                Item(t1, Day0.AddDays(1), 60),
            };

            var score = _sut.Evaluate(schedule, UnitWeights);

            Assert.Equal(1.0, score.SessionQuality, precision: 6);
        }

        [Fact]
        public void SessionQuality_VeryShortChunk_ScoresWorseThanIdeal()
        {
            var t1 = Guid.NewGuid();
            var ideal = new List<ScheduledItem> { Item(t1, Day0, 45) };
            var tooShort = new List<ScheduledItem> { Item(t1, Day0, 5) };

            var idealScore = _sut.Evaluate(ideal, UnitWeights);
            var shortScore = _sut.Evaluate(tooShort, UnitWeights);

            Assert.True(shortScore.SessionQuality < idealScore.SessionQuality);
        }

        [Fact]
        public void SessionQuality_VeryLongChunk_ScoresWorseThanIdeal()
        {
            var t1 = Guid.NewGuid();
            var ideal = new List<ScheduledItem> { Item(t1, Day0, 45) };
            var tooLong = new List<ScheduledItem> { Item(t1, Day0, 240) };

            var idealScore = _sut.Evaluate(ideal, UnitWeights);
            var longScore = _sut.Evaluate(tooLong, UnitWeights);

            Assert.True(longScore.SessionQuality < idealScore.SessionQuality);
            Assert.Equal(0.0, longScore.SessionQuality, precision: 6);
        }

        // ---------- FatiguePenalty ----------

        [Fact]
        public void FatiguePenalty_NoConsecutiveHeavyDays_IsZero()
        {
            var t1 = Guid.NewGuid();
            // Alternating heavy/light with a rest day in between breaks calendar adjacency.
            var schedule = new List<ScheduledItem>
            {
                Item(t1, Day0, 180),
                Item(t1, Day0.AddDays(2), 10),
            };

            var score = _sut.Evaluate(schedule, UnitWeights);

            Assert.Equal(0.0, score.FatiguePenalty, precision: 6);
        }

        [Fact]
        public void FatiguePenalty_ConsecutiveHeavyDays_IsMoreNegativeThanBrokenStreak()
        {
            var t1 = Guid.NewGuid();
            var consecutiveHeavy = new List<ScheduledItem>
            {
                Item(t1, Day0, 10),
                Item(t1, Day0.AddDays(1), 200),
                Item(t1, Day0.AddDays(2), 200),
            };
            var brokenStreak = new List<ScheduledItem>
            {
                Item(t1, Day0, 10),
                Item(t1, Day0.AddDays(1), 200),
                Item(t1, Day0.AddDays(3), 200), // gap at day 2 breaks calendar adjacency
            };

            var consecutiveScore = _sut.Evaluate(consecutiveHeavy, UnitWeights);
            var brokenScore = _sut.Evaluate(brokenStreak, UnitWeights);

            Assert.True(consecutiveScore.FatiguePenalty < brokenScore.FatiguePenalty);
        }

        // ---------- FragmentationPenalty ----------

        [Fact]
        public void FragmentationPenalty_NoTaskSplit_IsZero()
        {
            var t1 = Guid.NewGuid();
            var t2 = Guid.NewGuid();
            var schedule = new List<ScheduledItem>
            {
                Item(t1, Day0, 60),
                Item(t2, Day0.AddDays(1), 60),
            };

            var score = _sut.Evaluate(schedule, UnitWeights);

            Assert.Equal(0.0, score.FragmentationPenalty, precision: 6);
        }

        [Fact]
        public void FragmentationPenalty_HeavilyFragmentedSchedule_ScoresPoorly()
        {
            var t1 = Guid.NewGuid();
            var t2 = Guid.NewGuid();

            var notFragmented = new List<ScheduledItem>
            {
                Item(t1, Day0, 60),
                Item(t2, Day0.AddDays(1), 60),
            };

            // Same task split into 5 chunks across 5 days.
            var fragmented = Enumerable.Range(0, 5)
                .Select(i => Item(t1, Day0.AddDays(i), 20))
                .ToList();

            var notFragmentedScore = _sut.Evaluate(notFragmented, UnitWeights);
            var fragmentedScore = _sut.Evaluate(fragmented, UnitWeights);

            Assert.Equal(0.0, notFragmentedScore.FragmentationPenalty, precision: 6);
            Assert.True(fragmentedScore.FragmentationPenalty < 0.0);
            Assert.True(fragmentedScore.FragmentationPenalty < notFragmentedScore.FragmentationPenalty);
        }

        // ---------- Weighted combination ----------

        [Fact]
        public void Evaluate_WeightedTotal_IsExactLinearCombinationOfRawTerms()
        {
            var t1 = Guid.NewGuid();
            var t2 = Guid.NewGuid();
            var schedule = new List<ScheduledItem>
            {
                Item(t1, Day0, 50, tenMon: "Toán"),
                Item(t2, Day0.AddDays(1), 130, tenMon: "Vật Lý"),
                Item(t1, Day0.AddDays(2), 5, tenMon: "Toán"),
            };
            var weights = new SoeWeights(
                LoadBalanceWeight: 2.0,
                ContextContinuityWeight: 3.0,
                SessionQualityWeight: 0.5,
                FatiguePenaltyWeight: 1.5,
                FragmentationPenaltyWeight: 4.0);

            var score = _sut.Evaluate(schedule, weights);

            double expected =
                2.0 * score.LoadBalance +
                3.0 * score.ContextContinuity +
                0.5 * score.SessionQuality +
                1.5 * score.FatiguePenalty +
                4.0 * score.FragmentationPenalty;

            Assert.Equal(expected, score.Total, precision: 9);
        }

        [Fact]
        public void Evaluate_ZeroWeights_TotalIsZeroRegardlessOfRawTerms()
        {
            var t1 = Guid.NewGuid();
            var schedule = new List<ScheduledItem>
            {
                Item(t1, Day0, 300),
                Item(t1, Day0.AddDays(1), 5),
            };
            var zeroWeights = new SoeWeights(0, 0, 0, 0, 0);

            var score = _sut.Evaluate(schedule, zeroWeights);

            Assert.Equal(0.0, score.Total, precision: 9);
        }

        [Fact]
        public void Evaluate_EmptySchedule_IsVacuouslyNeutral()
        {
            var score = _sut.Evaluate(new List<ScheduledItem>(), UnitWeights);

            Assert.Equal(1.0, score.LoadBalance, precision: 6);
            Assert.Equal(1.0, score.ContextContinuity, precision: 6);
            Assert.Equal(1.0, score.SessionQuality, precision: 6);
            Assert.Equal(0.0, score.FatiguePenalty, precision: 6);
            Assert.Equal(0.0, score.FragmentationPenalty, precision: 6);
        }

        // ---------- SoeWeights ----------

        [Fact]
        public void SoeWeights_NegativeWeight_IsInvalid()
        {
            var weights = new SoeWeights(LoadBalanceWeight: -0.1);

            Assert.False(weights.IsValid());
        }

        [Fact]
        public void SoeWeights_AllNonNegative_IsValid()
        {
            var weights = new SoeWeights(0, 0.2, 0.3, 0.4, 0.5);

            Assert.True(weights.IsValid());
        }

        [Fact]
        public void SoeWeights_DoesNotNeedToSumToOne_UnlikeWeightConfig()
        {
            // Deliberate contrast with WeightConfig.IsValid() (sum == 1.0). This is documented as
            // an intentional design difference, not an oversight -- assert it stays that way.
            var weights = new SoeWeights(5.0, 5.0, 5.0, 5.0, 5.0); // sums to 25, not 1

            Assert.True(weights.IsValid());
        }

        // ---------- D-G: no deadline-shaped surface anywhere ----------

        /// <summary>
        /// D-G hard requirement: the objective evaluator must never see a deadline. This is
        /// demonstrably tested two ways, not just asserted in prose: (1) reflection over every
        /// public member's parameter/return/property types across the whole SOE objective
        /// surface, rejecting DateTime anywhere and any member name containing deadline-shaped
        /// vocabulary; (2) a textual scan of the actual committed source files for the literal
        /// token "HanChot" (production's deadline field name) and deadline/urgency vocabulary.
        /// Either check alone could be gamed by a determined future edit; both together make
        /// smuggling a deadline term back in require deliberately defeating two independent
        /// signals.
        /// </summary>
        [Fact]
        public void PublicApiSurface_HasNoDateTimeOrDeadlineShapedMember()
        {
            var typesToCheck = new[]
            {
                typeof(IObjectiveEvaluator),
                typeof(ObjectiveEvaluator),
                typeof(SoeWeights),
                typeof(ObjectiveScore),
            };

            string[] forbiddenNameFragments = { "deadline", "hanchot", "urgency", "duedate" };

            foreach (var type in typesToCheck)
            {
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    string memberName = member.Name;
                    Assert.False(
                        ContainsAny(memberName, forbiddenNameFragments),
                        $"{type.FullName}.{memberName} has a deadline-shaped name -- forbidden by D-G.");

                    foreach (var t in MemberValueTypes(member))
                    {
                        Assert.NotEqual(typeof(DateTime), t);
                        Assert.NotEqual(typeof(DateTime?), t);
                    }
                }

                foreach (var ctor in type.GetConstructors())
                {
                    foreach (var p in ctor.GetParameters())
                    {
                        Assert.False(
                            ContainsAny(p.Name ?? string.Empty, forbiddenNameFragments),
                            $"{type.FullName} constructor parameter '{p.Name}' has a deadline-shaped name -- forbidden by D-G.");
                        Assert.NotEqual(typeof(DateTime), p.ParameterType);
                        Assert.NotEqual(typeof(DateTime?), p.ParameterType);
                    }
                }
            }
        }

        [Fact]
        public void SourceFiles_ContainNoHanChotOrDeadlineToken()
        {
            string repoRoot = RepoLocator.FindRepoRoot();
            string soeDir = Path.Combine(repoRoot, "SmartStudyPlanner", "Services", "Soe");
            string[] filesToScan =
            {
                "IObjectiveEvaluator.cs",
                "ObjectiveEvaluator.cs",
                "SoeWeights.cs",
                "ObjectiveScore.cs",
            };

            string[] forbiddenTokens = { "HanChot", "Deadline", "deadline", "Urgency", "urgency" };

            foreach (var fileName in filesToScan)
            {
                string path = Path.Combine(soeDir, fileName);
                Assert.True(File.Exists(path), $"Expected SOE objective-evaluator file missing: {path}");
                string content = File.ReadAllText(path);

                foreach (var token in forbiddenTokens)
                {
                    Assert.False(
                        content.Contains(token, StringComparison.Ordinal),
                        $"{fileName} contains forbidden token '{token}' -- D-G forbids any deadline reference in the objective evaluator.");
                }
            }
        }

        private static bool ContainsAny(string haystack, IEnumerable<string> fragments) =>
            fragments.Any(f => haystack.Contains(f, StringComparison.OrdinalIgnoreCase));

        private static IEnumerable<Type> MemberValueTypes(MemberInfo member)
        {
            switch (member)
            {
                case PropertyInfo p:
                    yield return p.PropertyType;
                    break;
                case FieldInfo f:
                    yield return f.FieldType;
                    break;
                case MethodInfo m when !m.IsSpecialName:
                    yield return m.ReturnType;
                    foreach (var param in m.GetParameters()) yield return param.ParameterType;
                    break;
            }
        }
    }
}
