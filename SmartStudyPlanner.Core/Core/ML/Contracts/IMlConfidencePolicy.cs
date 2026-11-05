namespace SmartStudyPlanner.Core.ML.Contracts
{
    /// <summary>
    /// Gating policy chung cho mọi ML output (M8-A intent, M8-B weight optimizer, M7 predictor).
    /// Ngưỡng mặc định theo plan: >=0.75 auto-apply, 0.60..0.75 review, <0.60 reject.
    /// </summary>
    public interface IMlConfidencePolicy
    {
        MlConfidenceDecision Decide(double confidence);
    }

    public enum MlConfidenceDecision
    {
        Reject = 0,
        Review = 1,
        AutoApply = 2,
    }
}
