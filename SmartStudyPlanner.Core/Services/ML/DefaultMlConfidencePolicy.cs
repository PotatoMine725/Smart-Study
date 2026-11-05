using SmartStudyPlanner.Core.ML.Contracts;

namespace SmartStudyPlanner.Services.ML
{
    /// <summary>
    /// Hard-coded confidence policy for the M8 release (not user-configurable yet).
    /// Thresholds per the M8 plan: &gt;= 0.75 auto-apply, 0.60..0.75 review, &lt; 0.60 reject.
    /// For M8-A intent merge, callers treat anything except <see cref="MlConfidenceDecision.Reject"/>
    /// as "merge" (i.e. confidence &gt;= 0.60).
    /// </summary>
    public sealed class DefaultMlConfidencePolicy : IMlConfidencePolicy
    {
        public const double ReviewThreshold = 0.60;
        public const double AutoApplyThreshold = 0.75;

        public MlConfidenceDecision Decide(double confidence)
        {
            if (confidence >= AutoApplyThreshold) return MlConfidenceDecision.AutoApply;
            if (confidence >= ReviewThreshold) return MlConfidenceDecision.Review;
            return MlConfidenceDecision.Reject;
        }
    }
}
