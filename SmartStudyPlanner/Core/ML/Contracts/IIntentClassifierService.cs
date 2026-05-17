using SmartStudyPlanner.Core.Parsing.Contracts;

namespace SmartStudyPlanner.Core.ML.Contracts
{
    /// <summary>
    /// Adapter port cho M8-A TextClassifier. Implementation sẽ nằm ở Services/ML/TextClassifier/
    /// và được wrap qua một adapter implement <see cref="IIntentClassifier"/> để inject vào ParsingOrchestrator.
    /// </summary>
    public interface IIntentClassifierService
    {
        IntentPrediction? Predict(string rawInput);
        bool IsModelLoaded { get; }
    }
}
