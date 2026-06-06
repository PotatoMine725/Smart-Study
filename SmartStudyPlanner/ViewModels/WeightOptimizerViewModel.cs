using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Services;
using System;
using System.Threading.Tasks;

namespace SmartStudyPlanner.ViewModels
{
    public partial class WeightOptimizerViewModel : ObservableObject
    {
        private readonly IDecisionEngine _engine;
        private readonly IMlConfidencePolicy _policy;
        private readonly Action<WeightConfig> _onSave;

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
            : this(ServiceLocator.Get<IDecisionEngine>(), ServiceLocator.Get<IMlConfidencePolicy>()) { }

        // Injection constructor — used by unit tests
        public WeightOptimizerViewModel(IDecisionEngine engine, IMlConfidencePolicy policy,
            Action<WeightConfig>? onSave = null)
        {
            _engine = engine;
            _policy = policy;
            _onSave = onSave ?? WeightConfigStore.Save;
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
            cfg.TimeWeight = Suggestion.Suggested.TimeWeight;
            cfg.TaskTypeWeight = Suggestion.Suggested.TaskTypeWeight;
            cfg.CreditWeight = Suggestion.Suggested.CreditWeight;
            cfg.DifficultyWeight = Suggestion.Suggested.DifficultyWeight;
            cfg.Normalize();
            _onSave(cfg);
            OnPropertyChanged(nameof(CurrentConfig));
            ApplyStatus = "✓ Đã áp dụng thành công.";
        }

        private bool CanApply() => HasSuggestion && Suggestion != null;
    }
}
