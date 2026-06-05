using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Core.Parsing.Contracts;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services.ML
{
    /// <summary>
    /// M8-A intent-classifier service. Wraps <see cref="ITextClassifierModelManager"/> and adapts its
    /// domain DTO into the Core <see cref="IntentPrediction"/>. DoKho is always null this slice
    /// (difficulty prediction is deferred to a separate model).
    /// </summary>
    public sealed class TextClassifierService : IIntentClassifierService
    {
        private readonly ITextClassifierModelManager _manager;

        public TextClassifierService(ITextClassifierModelManager manager)
        {
            _manager = manager;
        }

        public bool IsModelLoaded => _manager.IsReady;

        public IntentPrediction? Predict(string rawInput)
        {
            if (!IsModelLoaded || string.IsNullOrWhiteSpace(rawInput))
                return null;

            var pred = _manager.Predict(new TextClassifierInput { InputText = rawInput });
            if (pred == null)
                return null;

            return new IntentPrediction
            {
                Loai = pred.Loai,
                DoKho = null, // Deferred: difficulty prediction is a separate model (not Slice 5).
                Confidence = pred.Confidence,
            };
        }
    }
}
