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
    }
}
