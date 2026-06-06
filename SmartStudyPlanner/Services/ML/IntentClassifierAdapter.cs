using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Core.Parsing.Contracts;

namespace SmartStudyPlanner.Services.ML
{
    /// <summary>
    /// Bridges the M8-A <see cref="IIntentClassifierService"/> into the parser seam
    /// (<see cref="IIntentClassifier"/>) that <c>ParsingOrchestrator</c> consumes.
    /// Applies the confidence gate: a prediction below the merge threshold is dropped
    /// (returns null) so the orchestrator falls back to the heuristic parse.
    /// Offline-first: any failure (model missing, mid-inference error) yields null,
    /// never an exception that could break parsing.
    /// </summary>
    public sealed class IntentClassifierAdapter : IIntentClassifier
    {
        private readonly IIntentClassifierService _service;
        private readonly IMlConfidencePolicy _policy;

        public IntentClassifierAdapter(IIntentClassifierService service, IMlConfidencePolicy policy)
        {
            _service = service;
            _policy = policy;
        }

        public IntentPrediction? Classify(string rawInput)
        {
            try
            {
                var prediction = _service.Predict(rawInput);
                if (prediction is null)
                    return null;

                // Reject = below merge threshold (< 0.60) → heuristic wins.
                return _policy.Decide(prediction.Confidence) == MlConfidenceDecision.Reject
                    ? null
                    : prediction;
            }
            catch
            {
                // ML is always an enhancement; a classifier failure must never break parsing.
                return null;
            }
        }
    }
}
