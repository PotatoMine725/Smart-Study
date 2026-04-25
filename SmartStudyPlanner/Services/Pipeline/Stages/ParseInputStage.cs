using System;
namespace SmartStudyPlanner.Services.Pipeline.Stages
{
    /// <summary>
    /// Stage chuẩn hóa input thô. MVP future-proof: giữ contract riêng để sau này thay parser mà không đụng orchestrator.
    /// </summary>
    public sealed class ParseInputStage : IPipelineStage
    {
        public PipelineStageType StageType => PipelineStageType.ParseInput;
        public int Order => (int)StageType;

        public bool CanRun(PipelineContext context)
        {
            return context is not null && !string.IsNullOrWhiteSpace(context.RawInput);
        }

        public PipelineStageResult Execute(PipelineContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            if (string.IsNullOrWhiteSpace(context.RawInput))
            {
                context.ParsedInput = new ParsedInputModel
                {
                    NormalizedInput = string.Empty,
                    ParsedTaskCount = 0
                };

                return new PipelineStageResult
                {
                    StageType = StageType,
                    Success = false,
                    Skipped = true,
                    Message = "Raw input is empty; parsing skipped."
                };
            }

            var normalized = context.RawInput.Trim();
            context.ParsedInput = new ParsedInputModel
            {
                NormalizedInput = normalized,
                ParsedTaskCount = 1
            };

            return new PipelineStageResult
            {
                StageType = StageType,
                Success = true,
                Message = "Input normalized successfully."
            };
        }
    }
}
