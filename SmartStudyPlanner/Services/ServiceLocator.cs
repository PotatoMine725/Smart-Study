using Microsoft.Extensions.DependencyInjection;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Core.Parsing.Contracts;
using SmartStudyPlanner.Core.Parsing.Orchestrators;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Services.Analytics;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.Pipeline;
using SmartStudyPlanner.Services.Pipeline.Stages;
using SmartStudyPlanner.Services.RiskAnalyzer;
using SmartStudyPlanner.Services.Strategies;
using SmartStudyPlanner.Services.Telemetry;

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

            // Repository abstractions (Slice 4 seam + StudyRepository split).
            // Mỗi repo nhận factory để chỉ instantiate context khi cần (tránh lifecycle conflict với singleton AppDbContext).
            Func<AppDbContext> ctxFactory = () => new AppDbContext();
            services.AddSingleton<IStudyTaskRepository>(_ => new SqliteStudyTaskRepository(ctxFactory));
            services.AddSingleton<IStudyLogRepository>(_ => new SqliteStudyLogRepository(ctxFactory));
            services.AddSingleton<IMonHocRepository>(_ => new SqliteMonHocRepository(ctxFactory));
            services.AddSingleton<IUserStatsRepository>(_ => new SqliteUserStatsRepository(ctxFactory));
            services.AddSingleton<IHocKyRepository>(_ => new SqliteHocKyRepository(ctxFactory));
            services.AddSingleton<ITaskEditorRepository>(_ => new SqliteTaskEditorRepository(ctxFactory));

            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<ITaskTypeWeightProvider, DefaultTaskTypeWeightProvider>();
            services.AddSingleton<WeightConfig>();
            services.AddSingleton<IDecisionEngine, DecisionEngineService>();
            services.AddSingleton<IWorkloadService, WorkloadServiceImpl>();
            services.AddSingleton<IRiskAnalyzer, RiskAnalyzerService>();

            services.AddSingleton<IModelStorageProvider, LocalModelStorageProvider>();
            services.AddSingleton<IMLModelManager, MLModelManager>();
            services.AddSingleton<IStudyTimePredictor, StudyTimePredictorService>();

            // M8-B WeightOptimizer (Slice 7). Rule-based, đọc UserStatsSnapshot; DI tự inject vào
            // DecisionEngineService/SchedulingOrchestrator qua optional ctor param.
            services.AddSingleton<IWeightOptimizerService>(sp =>
                new SmartStudyPlanner.Services.ML.WeightOptimizer.WeightOptimizerService(
                    sp.GetRequiredService<IUserStatsRepository>(),
                    sp.GetRequiredService<IClock>()));

            // M8-A TextClassifier (Slice 6 wiring). Manager owns its own text_classifier.zip paths.
            services.AddSingleton<ITextClassifierModelManager>(_ => new TextClassifierModelManager());
            services.AddSingleton<IIntentClassifierService, TextClassifierService>();
            services.AddSingleton<IMlConfidencePolicy, DefaultMlConfidencePolicy>();
            services.AddSingleton<IIntentClassifier>(sp =>
                new IntentClassifierAdapter(
                    sp.GetRequiredService<IIntentClassifierService>(),
                    sp.GetRequiredService<IMlConfidencePolicy>()));

            // Parser now consumes the classifier through the adapter. Offline-first: if the model is
            // absent/unloaded the adapter returns null and the orchestrator is byte-equal to heuristic.
            services.AddSingleton<IParsingOrchestrator>(sp =>
                new ParsingOrchestrator(
                    sp.GetRequiredService<IClock>(),
                    sp.GetRequiredService<IIntentClassifier>()));

            services.AddSingleton<IPipelineStage, ParseInputStage>();
            services.AddSingleton<IPipelineStage, PrioritizeStage>();
            services.AddSingleton<IPipelineStage, BalanceWorkloadStage>();
            services.AddSingleton<IPipelineStage, AssessRiskStage>();
            services.AddSingleton<IPipelineStage, AdaptStage>();
            services.AddSingleton<IPipelineOrchestrator, PipelineOrchestrator>();
            services.AddSingleton<IStudyAnalytics, StudyAnalyticsService>();
            services.AddSingleton<IStudyTelemetry, DebugStudyTelemetry>();

            return services.BuildServiceProvider();
        }
    }
}
