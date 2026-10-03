using System;
using System.Reflection;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 4 — the non-WPF half of OD-7's surfacing channel. The WPF half (the
    /// <c>App.DispatcherUnhandledException</c> branch that shows this text in a MessageBox) is NOT RUN
    /// by any test.
    /// </summary>
    public class LocalSaveRejectionTests
    {
        private static readonly Guid Conflict = Guid.Parse("c0000000-0000-0000-0000-000000000001");

        private static MutationRejectedException Blocked() => new(new FenceDecision(new[]
        {
            new PolicyResult(Conflict, "scope", ConflictShape.ConcurrentReparent,
                new EntitySubject(SyncEntityTypes.StudyTask, Guid.NewGuid()), FenceOutcome.Blocked,
                RoutingStage.CascadeReached, "S1CR.SubjectRemoved", "evidence"),
            new PolicyResult(Guid.NewGuid(), "other", ConflictShape.ConcurrentReparent,
                new EntitySubject(SyncEntityTypes.StudyTask, Guid.NewGuid()), FenceOutcome.Passed,
                RoutingStage.CascadeReached, "S1CR.ChildEdgeOnly", "evidence"),
        }, RouteKnown: true));

        [Fact]
        public void UserMessage_ListsEveryBlockingRuleIdWithItsConflict_AndNotThePassedOnes()
        {
            var text = LocalSaveRejection.UserMessage(Blocked());

            Assert.Contains("S1CR.SubjectRemoved (Blocked)", text, StringComparison.Ordinal);
            Assert.Contains(Conflict.ToString(), text, StringComparison.Ordinal);
            Assert.DoesNotContain("S1CR.ChildEdgeOnly", text, StringComparison.Ordinal);
            Assert.Contains("Không có thay đổi nào được lưu", text, StringComparison.Ordinal);
            Assert.Contains("đã được đưa về trạng thái đã lưu", text, StringComparison.Ordinal);
        }

        [Fact]
        public void UserMessage_RouteUnknown_SaysSo()
        {
            var text = LocalSaveRejection.UserMessage(
                new MutationRejectedException(new FenceDecision(Array.Empty<PolicyResult>(), RouteKnown: false)));

            Assert.Contains("RouteKnown = false", text, StringComparison.Ordinal);
        }

        /// <summary>
        /// The restore failure travels in <c>Exception.Data</c>; prove the runtime accepts an
        /// Exception value there (.NET Framework required [Serializable] values; .NET does not).
        /// </summary>
        [Fact]
        public void RestoreFailure_RoundTripsThroughExceptionData_AndChangesTheMessage()
        {
            var rejection = Blocked();
            var failure = new InvalidOperationException("restore broke");

            rejection.Data[LocalSaveRejection.RestoreFailureDataKey] = failure;

            Assert.Same(failure, LocalSaveRejection.RestoreFailureOf(rejection));
            Assert.Contains("Không khôi phục được", LocalSaveRejection.UserMessage(rejection), StringComparison.Ordinal);
            Assert.Null(LocalSaveRejection.RestoreFailureOf(Blocked()));
        }

        [Fact]
        public void TryFind_FindsTheRejection_DirectlyOrWrapped()
        {
            var rejection = Blocked();

            Assert.True(LocalSaveRejection.TryFind(rejection, out var direct));
            Assert.Same(rejection, direct);
            Assert.True(LocalSaveRejection.TryFind(new TargetInvocationException(rejection), out var wrapped));
            Assert.Same(rejection, wrapped);
            Assert.True(LocalSaveRejection.TryFind(new AggregateException(rejection), out var aggregated));
            Assert.Same(rejection, aggregated);
            Assert.False(LocalSaveRejection.TryFind(new InvalidOperationException("other"), out _));
            Assert.False(LocalSaveRejection.TryFind(null, out _));
        }
    }
}
