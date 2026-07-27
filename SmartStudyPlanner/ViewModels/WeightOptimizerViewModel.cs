using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Models.Telemetry;
using SmartStudyPlanner.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartStudyPlanner.ViewModels
{
    public partial class WeightOptimizerViewModel : ObservableObject
    {
        private readonly IDecisionEngine _engine;
        private readonly IMlConfidencePolicy _policy;
        private readonly Action<WeightConfig> _onSave;
        private readonly IWeightChangeLogRepository? _weightChangeLogRepo;
        private readonly IUserStatsRepository? _userStatsRepo;
        private readonly IStudyTaskRepository? _studyTaskRepo;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ApplySuggestionCommand))]
        private WeightConfigSuggestion? suggestion;

        [ObservableProperty] private MlConfidenceDecision decision;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ApplySuggestionCommand))]
        private bool hasSuggestion;

        [ObservableProperty] private bool isHighConfidence;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string statusMessage = string.Empty;
        [ObservableProperty] private string applyStatus = string.Empty;

        public WeightConfig CurrentConfig => _engine.Config;

        // Default constructor — resolves from DI (used by Window code-behind)
        public WeightOptimizerViewModel()
            : this(
                ServiceLocator.Get<IDecisionEngine>(),
                ServiceLocator.Get<IMlConfidencePolicy>(),
                null,
                ServiceLocator.Get<IWeightChangeLogRepository>(),
                ServiceLocator.Get<IUserStatsRepository>(),
                ServiceLocator.Get<IStudyTaskRepository>()) { }

        // Injection constructor — used by unit tests (optional new params preserve existing call sites)
        public WeightOptimizerViewModel(IDecisionEngine engine, IMlConfidencePolicy policy,
            Action<WeightConfig>? onSave = null,
            IWeightChangeLogRepository? weightChangeLogRepo = null,
            IUserStatsRepository? userStatsRepo = null,
            IStudyTaskRepository? studyTaskRepo = null)
        {
            _engine = engine;
            _policy = policy;
            _onSave = onSave ?? WeightConfigStore.Save;
            _weightChangeLogRepo = weightChangeLogRepo;
            _userStatsRepo = userStatsRepo;
            _studyTaskRepo = studyTaskRepo;
        }

        [RelayCommand]
        private async Task LoadSuggestion()
        {
            IsBusy = true;
            ApplyStatus = string.Empty;
            try
            {
                Suggestion = await _engine.SuggestWeightConfigAsync();
                if (Suggestion == null)
                {
                    Decision = MlConfidenceDecision.Reject;
                    HasSuggestion = false;
                    IsHighConfidence = false;
                    StatusMessage = "Cần thêm dữ liệu để đưa ra gợi ý (ít nhất 5 task với lịch sử học).";
                }
                else
                {
                    Decision = _policy.Decide(Suggestion.Confidence);
                    HasSuggestion = Decision != MlConfidenceDecision.Reject;
                    IsHighConfidence = Decision == MlConfidenceDecision.AutoApply;
                    StatusMessage = HasSuggestion
                        ? string.Empty
                        : "Cần thêm dữ liệu để đưa ra gợi ý (ít nhất 5 task với lịch sử học).";
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanApply))]
        private void ApplySuggestion()
        {
            if (Suggestion == null) return;
            var cfg = _engine.Config;

            // Snapshot before-state for ground-truth log (capture before mutation)
            var before = new WeightConfig
            {
                TimeWeight = cfg.TimeWeight,
                TaskTypeWeight = cfg.TaskTypeWeight,
                CreditWeight = cfg.CreditWeight,
                DifficultyWeight = cfg.DifficultyWeight,
            };

            cfg.TimeWeight = Suggestion.Suggested.TimeWeight;
            cfg.TaskTypeWeight = Suggestion.Suggested.TaskTypeWeight;
            cfg.CreditWeight = Suggestion.Suggested.CreditWeight;
            cfg.DifficultyWeight = Suggestion.Suggested.DifficultyWeight;
            cfg.Normalize();
            _onSave(cfg);
            OnPropertyChanged(nameof(CurrentConfig));
            ApplyStatus = "✓ Đã áp dụng thành công.";

            // Ground-truth instrumentation: fire-and-forget, never blocks apply path
            _ = CrashLogger.Observe(LogWeightChangeAsync(before, Suggestion), "WeightChangeLog");
        }

        private bool CanApply() => HasSuggestion && Suggestion != null;

        private async Task LogWeightChangeAsync(WeightConfig before, WeightConfigSuggestion sugg)
        {
            if (_weightChangeLogRepo == null) return;
            try
            {
                var nowUtc = DateTime.UtcNow;
                var snapshot = _userStatsRepo != null
                    ? await _userStatsRepo.GetSnapshotAsync(nowUtc)
                    : new UserStatsSnapshot { ReferenceUtc = nowUtc };

                List<Guid> cohortIds = new();
                if (_studyTaskRepo != null)
                {
                    var all = await _studyTaskRepo.GetAllAsync();
                    cohortIds = all
                        .Where(t => t.TrangThai != StudyTaskStatus.HoanThanh)
                        .Select(t => t.MaTask)
                        .ToList();
                }

                await _weightChangeLogRepo.AddAsync(new WeightChangeLog
                {
                    Id = Guid.NewGuid(),
                    AppliedUtc = nowUtc,
                    Confidence = sugg.Confidence,
                    Rationale = sugg.Rationale ?? string.Empty,
                    BeforeTime = before.TimeWeight,
                    BeforeTaskType = before.TaskTypeWeight,
                    BeforeCredit = before.CreditWeight,
                    BeforeDifficulty = before.DifficultyWeight,
                    AfterTime = sugg.Suggested.TimeWeight,
                    AfterTaskType = sugg.Suggested.TaskTypeWeight,
                    AfterCredit = sugg.Suggested.CreditWeight,
                    AfterDifficulty = sugg.Suggested.DifficultyWeight,
                    BaselineMissRate = snapshot.MissRate,
                    BaselineAvgDelayDays = snapshot.AverageDelayDays,
                    BaselineFocusStreak = snapshot.FocusStreakDays,
                    BaselineStudyMinutes30 = snapshot.TotalStudyMinutesLast30Days,
                    BaselineTaskCount = snapshot.TotalTaskCount,
                    BaselineCompletedCount = snapshot.CompletedTaskCount,
                    CohortTaskIdsJson = JsonSerializer.Serialize(cohortIds),
                });
            }
            catch
            {
                // Logging is an enhancement — never propagate errors to the apply path.
            }
        }
    }
}
