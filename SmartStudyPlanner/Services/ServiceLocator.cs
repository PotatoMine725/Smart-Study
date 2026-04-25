using Microsoft.Extensions.DependencyInjection;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Services.RiskAnalyzer;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services
{
    /// <summary>
    /// DI container tĩnh cho WPF (không có built-in host như ASP.NET Core).
    /// Dùng ServiceLocator.Provider.GetRequiredService&lt;T&gt;() để resolve.
    /// </summary>
    public static class ServiceLocator
    {
        private static IServiceProvider? _provider;

        public static IServiceProvider Provider =>
            _provider ?? throw new InvalidOperationException("ServiceLocator chưa được khởi tạo. Gọi Configure() trong App.OnStartup trước.");

        public static void Configure(Action<IServiceCollection>? extraRegistrations = null)
        {
            var services = new ServiceCollection();

            // ── Infrastructure ──────────────────────────────────────────────
            services.AddSingleton<IStudyRepository, StudyRepository>();

            // ── Strategies / Clock ──────────────────────────────────────────
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<ITaskTypeWeightProvider, DefaultTaskTypeWeightProvider>();

            // ── Domain Services ─────────────────────────────────────────────
            services.AddSingleton<IDecisionEngine, DecisionEngineService>();
            services.AddSingleton<IWorkloadService, WorkloadServiceImpl>();
            services.AddSingleton<IRiskAnalyzer, RiskAnalyzerService>();

            // ── Cho phép caller (thường là test host) đăng ký thêm ──────────
            extraRegistrations?.Invoke(services);

            _provider = services.BuildServiceProvider();
        }

        /// <summary>Lấy service đã đăng ký; ném InvalidOperationException nếu chưa Configure.</summary>
        public static T Get<T>() where T : notnull => Provider.GetRequiredService<T>();
    }
}
