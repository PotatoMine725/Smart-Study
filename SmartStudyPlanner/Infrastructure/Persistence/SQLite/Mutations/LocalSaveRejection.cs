using System;
using System.Linq;
using SmartStudyPlanner.Sync.Fence;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations
{
    // Epic 2 / T2.4 fence Slice 4 (OD-7, "the rejection is surfaced with the relevant conflict/rule
    // information"). The pieces of the surfacing channel that are not WPF: finding a rejection in an
    // exception reaching the global handler, the text the user is shown, and the restore failure
    // the executor may have attached. App.DispatcherUnhandledException is the only caller in
    // production; it shows the text and logs, it does not catch, retry or reinterpret anything.
    internal static class LocalSaveRejection
    {
        // MutationRejectedException (Sync/Fence, frozen for this slice) has no inner-exception
        // constructor, so a failure of the in-memory restoration travels in Exception.Data under this
        // key instead. The rejection itself is never replaced by the restore failure.
        public const string RestoreFailureDataKey = "SmartStudyPlanner.LocalSave.RestoreFailure";

        // The toolkit's AsyncRelayCommand rethrows the original exception on the Dispatcher, so the
        // handler normally sees the rejection itself; walking InnerException also covers a wrapper
        // (TargetInvocationException, or an AggregateException, whose InnerException is its first item).
        public static bool TryFind(Exception? exception, out MutationRejectedException rejection)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                if (current is MutationRejectedException found)
                {
                    rejection = found;
                    return true;
                }
            }

            rejection = null!;
            return false;
        }

        public static Exception? RestoreFailureOf(MutationRejectedException rejection) =>
            rejection.Data.Contains(RestoreFailureDataKey) ? rejection.Data[RestoreFailureDataKey] as Exception : null;

        public static string UserMessage(MutationRejectedException rejection)
        {
            var restored = RestoreFailureOf(rejection) is null
                ? "Dữ liệu trong ứng dụng đã được đưa về trạng thái đã lưu; nếu màn hình chưa cập nhật, hãy mở lại trang."
                : "Không khôi phục được dữ liệu trên màn hình về trạng thái đã lưu; hãy mở lại trang trước khi sửa tiếp.";

            var rules = rejection.Decision.RouteKnown
                ? string.Join(Environment.NewLine, rejection.Decision.Results
                    .Where(r => r.Outcome is FenceOutcome.Blocked or FenceOutcome.Unsupported)
                    .Select(r => $"• {r.RuleId} ({r.Outcome}) — xung đột {r.ConflictId}"))
                : "• RouteKnown = false (thao tác chạm tới loại dữ liệu hoặc trường chưa được đăng ký)";

            return "Không thể lưu: thay đổi này đụng tới dữ liệu đang có xung đột đồng bộ chưa được giải quyết."
                + Environment.NewLine + "Không có thay đổi nào được lưu, và ứng dụng sẽ không tự lưu lại."
                + Environment.NewLine + restored
                + Environment.NewLine + Environment.NewLine + "Quy tắc chặn:"
                + Environment.NewLine + rules;
        }
    }
}
