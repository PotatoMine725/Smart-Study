using Microsoft.Extensions.DependencyInjection;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Services.Pipeline;
using SmartStudyPlanner.Services.Pipeline.Stages;
using SmartStudyPlanner.Services.RiskAnalyzer;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services
{
    /// <summary>
    /// Composition root tạm thời cho WPF app.
    /// Đăng ký toàn bộ service theo mô hình DI để tránh new trực tiếp trong ViewModel.
    /// </summary>
    public static class ServiceLocator
    {
        private static ServiceProvider? _provider;

        public static ServiceProvider Provider => _provider ??= BuildProvider();

        public static void Configure()
        {
            _provider = BuildProvider();
        }

        public static T Get<T>() where T : notnull => Provider.GetRequiredService<T>();

        private static ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();

            services.AddSingleton<AppDbContext>();
            services.AddSingleton<IStudyRepository, StudyRepository>();
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<ITaskTypeWeightProvider, DefaultTaskTypeWeightProvider>();
            services.AddSingleton<WeightConfig>();
            services.AddSingleton<IDecisionEngine, DecisionEngineService>();
            services.AddSingleton<IWorkloadService, WorkloadServiceImpl>();
            services.AddSingleton<IRiskAnalyzer, RiskAnalyzerService>();

            services.AddSingleton<IPipelineStage, ParseInputStage>();
            services.AddSingleton<IPipelineStage, PrioritizeStage>();
            services.AddSingleton<IPipelineStage, BalanceWorkloadStage>();
            services.AddSingleton<IPipelineStage, AssessRiskStage>();
            services.AddSingleton<IPipelineStage, AdaptStage>();
            services.AddSingleton<IPipelineOrchestrator, PipelineOrchestrator>();

            return services.BuildServiceProvider();
        }
    }
}
