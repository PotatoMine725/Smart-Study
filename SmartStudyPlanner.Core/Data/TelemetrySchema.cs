using Microsoft.EntityFrameworkCore;

namespace SmartStudyPlanner.Data
{
    /// <summary>
    /// Runtime schema migration cho 2 bảng telemetry M8. <see cref="AppDbContext"/>.EnsureCreated()
    /// KHÔNG thêm bảng mới vào một DB đã tồn tại (DB được tạo trước M8), nên hai bảng phải được
    /// tạo thủ công bằng CREATE TABLE IF NOT EXISTS lúc startup.
    /// <para>
    /// Tách khỏi <c>App.OnStartup</c> để dual-path (DB mới qua EnsureCreated vs. DB cũ qua block này)
    /// kiểm thử được — OnStartup là WPF lifecycle, không gọi được từ unit test.
    /// </para>
    /// </summary>
    public static class TelemetrySchema
    {
        /// <summary>
        /// Idempotent: tạo <c>DifficultyLabelLogs</c> + <c>WeightChangeLogs</c> nếu chưa tồn tại.
        /// An toàn để gọi mỗi lần startup, kể cả khi bảng đã có.
        /// </summary>
        public static void EnsureTables(AppDbContext db)
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS DifficultyLabelLogs (
                    Id TEXT NOT NULL PRIMARY KEY,
                    CreatedUtc TEXT NOT NULL,
                    InputText TEXT NOT NULL DEFAULT '',
                    TaskType INTEGER NOT NULL,
                    SuggestedDoKho INTEGER NULL,
                    FinalDoKho INTEGER NOT NULL,
                    WasOverride INTEGER NOT NULL,
                    Source TEXT NOT NULL DEFAULT '',
                    MaTask TEXT NULL
                )");

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS StudyTimeOutcomeLogs (
                    Id TEXT NOT NULL PRIMARY KEY,
                    CreatedUtc TEXT NOT NULL,
                    MaTask TEXT NULL,
                    TaskType INTEGER NOT NULL,
                    Difficulty REAL NOT NULL,
                    Credits REAL NOT NULL,
                    DaysLeft REAL NOT NULL,
                    StudiedMinutesSoFar REAL NOT NULL,
                    ActualMinutes REAL NOT NULL,
                    PredictedMinutes REAL NULL,
                    WasMlPrediction INTEGER NOT NULL,
                    Confidence REAL NULL
                )");

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS WeightChangeLogs (
                    Id TEXT NOT NULL PRIMARY KEY,
                    AppliedUtc TEXT NOT NULL,
                    Confidence REAL NOT NULL,
                    Rationale TEXT NOT NULL DEFAULT '',
                    BeforeTime REAL NOT NULL,
                    BeforeTaskType REAL NOT NULL,
                    BeforeCredit REAL NOT NULL,
                    BeforeDifficulty REAL NOT NULL,
                    AfterTime REAL NOT NULL,
                    AfterTaskType REAL NOT NULL,
                    AfterCredit REAL NOT NULL,
                    AfterDifficulty REAL NOT NULL,
                    BaselineMissRate REAL NOT NULL,
                    BaselineAvgDelayDays REAL NOT NULL,
                    BaselineFocusStreak INTEGER NOT NULL,
                    BaselineStudyMinutes30 INTEGER NOT NULL,
                    BaselineTaskCount INTEGER NOT NULL,
                    BaselineCompletedCount INTEGER NOT NULL,
                    CohortTaskIdsJson TEXT NOT NULL DEFAULT '[]',
                    OutcomeWindowDays INTEGER NOT NULL DEFAULT 14,
                    OutcomeMaturedUtc TEXT NULL,
                    OutcomeMissRate REAL NULL,
                    OutcomeAvgDelayDays REAL NULL,
                    OutcomeCompletedInWindow INTEGER NULL
                )");
        }

        /// <summary>
        /// T3.7 (Epic 3, Card G) — bảng telemetry <c>OptimizerRunLogs</c> cho
        /// <c>IScheduleOptimizer.Optimize</c> (G2-6, <c>docs/plans/2026-08-04-g2-optimization-pass-semantics.md</c>).
        /// TÁCH RIÊNG khỏi <see cref="EnsureTables"/> (M8) có chủ đích — execution plan Card G nêu rõ:
        /// "not merged into EnsureTables, so Epic 3's addition doesn't blend into the M8 tables on
        /// review/audit". Idempotent (<c>CREATE TABLE IF NOT EXISTS</c>), an toàn gọi mỗi lần startup.
        /// <para>
        /// <b>DoR check 6 (backup/rollback story):</b> <c>OptimizerRunLogs</c> là bảng HOÀN TOÀN MỚI,
        /// RỖNG trên MỌI DB hiện có — không có cột nào được thêm vào một bảng đã mang dữ liệu người
        /// dùng (khác hẳn <c>SyncSchema.EnsureColumns</c>, thứ ALTER bảng <c>HocKys</c> đã có dữ liệu
        /// và vì vậy bắt buộc <c>DbBackup.CreateBackup</c> chạy trước ở <see cref="AppStartup"/>).
        /// Vì không có dữ liệu người dùng nào bị đụng tới, method này KHÔNG gọi
        /// <c>DbBackup.CreateBackup</c> — không có gì để mất nếu nó thất bại giữa chừng (SQLite DDL
        /// đơn câu lệnh, và <c>CREATE TABLE IF NOT EXISTS</c> tái chạy an toàn). Rollback nếu cần:
        /// <c>DROP TABLE OptimizerRunLogs</c> — không bảng nào khác bị đụng tới.
        /// </para>
        /// </summary>
        public static void EnsureOptimizerRunLogTable(AppDbContext db)
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS OptimizerRunLogs (
                    Id TEXT NOT NULL PRIMARY KEY,
                    RunId TEXT NOT NULL,
                    CreatedUtc TEXT NOT NULL,
                    Termination INTEGER NOT NULL,
                    PassCount INTEGER NOT NULL,
                    PassIndex INTEGER NOT NULL,
                    KStar INTEGER NOT NULL,
                    CheckpointIndex INTEGER NOT NULL,
                    ViolationCount INTEGER NOT NULL,
                    OverdueMinutes INTEGER NOT NULL,
                    Score REAL NOT NULL,
                    Reason INTEGER NULL
                )");
        }
    }
}
