using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.RiskAnalyzer;

namespace SmartStudyPlanner.Services.Pipeline.Stages
{
    /// <summary>
    /// Stage đánh giá rủi ro. Không sửa công thức risk, chỉ gom kết quả để dashboard dùng.
    /// </summary>
    public sealed class AssessRiskStage : IPipelineStage
    {
        private readonly IRiskAnalyzer _riskAnalyzer;

        public AssessRiskStage(IRiskAnalyzer riskAnalyzer)
        {
            _riskAnalyzer = riskAnalyzer;
        }

        public PipelineStageType StageType => PipelineStageType.AssessRisk;
        public int Order => (int)StageType;

        public bool CanRun(PipelineContext context)
        {
            return context is not null && context.Semester is not null && context.Settings.EnableRiskAssessment;
        }

        public PipelineStageResult Execute(PipelineContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (context.Semester is null)
            {
                return new PipelineStageResult
                {
                    StageType = StageType,
                    Success = false,
                    Message = "No semester available for risk assessment."
                };
            }

            var assessments = new List<RiskAssessment>();
            foreach (var mon in context.Semester.DanhSachMonHoc)
            {
                foreach (var task in mon.DanhSachTask)
                {
                    if (task.TrangThai == StudyTaskStatus.HoanThanh) continue;
                    var r = _riskAnalyzer.Assess(task, mon);
                    assessments.Add(new RiskAssessment
                    {
                        TaskId                = task.MaTask,
                        Score                 = r.Score,
                        Level                 = r.Level,
                        DeadlineUrgencyScore  = r.DeadlineUrgencyScore,
                        ProgressGapScore      = r.ProgressGapScore,
                        PerformanceDropScore  = r.PerformanceDropScore
                    });
                }
            }

            context.RiskReport = assessments;
            context.Metadata["risk.count"] = assessments.Count;

            return new PipelineStageResult
            {
                StageType = StageType,
                Success = true,
                Message = $"Assessed risk for {assessments.Count} tasks."
            };
        }
    }
}
