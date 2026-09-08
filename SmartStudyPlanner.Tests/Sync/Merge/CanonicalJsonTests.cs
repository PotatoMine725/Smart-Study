using System;
using System.Text;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Merge
{
    /// <summary>
    /// K-1..K-6 — canonical SnapshotJson v1 (DoR §5.1/§5.2/§5.5). The expected bytes are written
    /// by hand here, NOT produced by the writer, so the writer cannot define its own contract.
    /// </summary>
    public class CanonicalJsonTests
    {
        // ---- K-1: exact envelope, hand-written (DoR §5.1 example) --------------------------

        private const string StudyTaskCanonical =
            "{\"v\":1,\"entityType\":\"StudyTask\",\"provenance\":{" +
            "\"modifiedAtUtc\":\"2026-09-08T01:02:03.1234567Z\"," +
            "\"modifiedByDeviceId\":\"desktop-1a2b3c4d\"," +
            "\"isDeleted\":false,\"deletedAtUtc\":null}," +
            "\"fields\":{" +
            "\"MaMonHoc\":\"0f3a1b2c-0000-0000-0000-000000000002\"," +
            "\"TenTask\":\"Ôn chương 3\"," +
            "\"HanChot\":\"2026-09-10T00:00:00.0000000\"," +
            "\"TrangThai\":\"Chưa làm\"," +
            "\"LoaiTask\":0,\"DoKho\":2,\"ThoiGianDaHoc\":0,\"NgayHoanThanh\":null}}";

        private static EntitySnapshot StudyTaskFixture() => MergeTestData.Snap(
            SyncEntityTypes.StudyTask,
            new Provenance(MergeTestData.Utc0, "desktop-1a2b3c4d", false, null),
            ("MaMonHoc", new GuidValue(MergeTestData.G2)),
            ("TenTask", new StringValue("Ôn chương 3")),
            ("HanChot", new WallClockValue(new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Unspecified))),
            ("TrangThai", new StringValue("Chưa làm")),
            ("LoaiTask", new Int32Value(0)),
            ("DoKho", new Int32Value(2)),
            ("ThoiGianDaHoc", new Int32Value(0)),
            ("NgayHoanThanh", new WallClockValue(null)));

        [Fact] // K-1
        public void Write_ProducesExactHandWrittenEnvelope_InRegistryOrder_WithNoWhitespace()
        {
            Assert.Equal(StudyTaskCanonical, CanonicalJson.Write(StudyTaskFixture()));
        }

        // ---- K-2: escaping set (DoR §5.2, RFC 8785 §3.2.2.2) --------------------------------

        // a " b \ c TAB U+0001 U+001B ế 😀
        private const string RawContent = "a\"b\\c\t\u0001\u001b\u1EBF\U0001F600";
        private const string EscapedContent = "a\\\"b\\\\c\\t\\u0001\\u001b\u1EBF\U0001F600";

        private const string TaskNoteCanonical =
            "{\"v\":1,\"entityType\":\"TaskNote\",\"provenance\":{" +
            "\"modifiedAtUtc\":\"2026-09-08T01:02:03.1234567Z\"," +
            "\"modifiedByDeviceId\":\"dev-1\",\"isDeleted\":false,\"deletedAtUtc\":null}," +
            "\"fields\":{\"MaTask\":\"0f3a1b2c-0000-0000-0000-000000000001\"," +
            "\"Content\":\"" + EscapedContent + "\"}}";

        private static EntitySnapshot TaskNoteFixture() => MergeTestData.Snap(
            SyncEntityTypes.TaskNote,
            new Provenance(MergeTestData.Utc0, "dev-1", false, null),
            ("MaTask", new GuidValue(MergeTestData.G1)),
            ("Content", new StringValue(RawContent)));

        [Fact] // K-2
        public void Write_EscapesOnlyTheRfc8785Set_AndLeavesVietnameseAndEmojiRaw()
        {
            Assert.Equal(TaskNoteCanonical, CanonicalJson.Write(TaskNoteFixture()));
        }

        [Fact] // K-2b — lowercase hex for the \u form
        public void Write_UsesLowercaseHexForControlCharacters()
        {
            var s = CanonicalJson.WriteFieldValue(new StringValue("\u001b\u001f"));
            Assert.Equal("\"\\u001b\\u001f\"", s);
        }

        // ---- K-3: scalar encodings ----------------------------------------------------------

        [Fact] // K-3a
        public void Write_EmitsGuidsLowercaseDFormat()
        {
            var g = new Guid("AABBCCDD-EEFF-0011-2233-445566778899");
            Assert.Equal("\"aabbccdd-eeff-0011-2233-445566778899\"", CanonicalJson.WriteFieldValue(new GuidValue(g)));
        }

        [Fact] // K-3b — enums travel as their underlying int, never as a name
        public void Write_EmitsEnumsAsInvariantIntegers()
        {
            Assert.Equal("4", CanonicalJson.WriteFieldValue(new Int32Value(4)));
            Assert.Equal("0", CanonicalJson.WriteFieldValue(new Int32Value(0)));
            Assert.Equal("-7", CanonicalJson.WriteFieldValue(new Int32Value(-7)));
        }

        [Fact] // K-3c
        public void Write_DistinguishesEmptyStringFromNull()
        {
            Assert.Equal("\"\"", CanonicalJson.WriteFieldValue(new StringValue("")));
            Assert.Equal("null", CanonicalJson.WriteFieldValue(new StringValue(null)));
        }

        [Fact] // K-3d
        public void Write_EmitsBooleansLowercase()
        {
            Assert.Equal("true", CanonicalJson.WriteFieldValue(new BoolValue(true)));
            Assert.Equal("false", CanonicalJson.WriteFieldValue(new BoolValue(false)));
        }

        // ---- K-4: DateTime canonical rules --------------------------------------------------

        [Fact] // K-4a
        public void Write_WallClockHasNoZoneSuffix_UtcHasZ()
        {
            var wall = new DateTime(2026, 9, 10, 13, 45, 6, DateTimeKind.Unspecified).AddTicks(7654321);
            var utc = new DateTime(2026, 9, 10, 13, 45, 6, DateTimeKind.Utc).AddTicks(7654321);

            Assert.Equal("\"2026-09-10T13:45:06.7654321\"", CanonicalJson.WriteFieldValue(new WallClockValue(wall)));
            Assert.Equal("\"2026-09-10T13:45:06.7654321Z\"", CanonicalJson.WriteFieldValue(new UtcValue(utc)));
        }

        [Fact] // K-4b — a Local-kinded value must NOT pick up the machine offset
        public void Write_NeverEmitsAMachineLocalOffset()
        {
            var local = DateTime.SpecifyKind(new DateTime(2026, 9, 10, 13, 45, 6), DateTimeKind.Local);
            var s = CanonicalJson.WriteFieldValue(new WallClockValue(local));
            Assert.DoesNotContain("+", s);
            Assert.Equal("\"2026-09-10T13:45:06.0000000\"", s);
        }

        [Fact] // K-4c
        public void Write_EmitsNullForNullableDateTimes()
        {
            Assert.Equal("null", CanonicalJson.WriteFieldValue(new WallClockValue(null)));
            Assert.Equal("null", CanonicalJson.WriteFieldValue(new UtcValue(null)));
        }

        [Fact] // K-4d — tombstone provenance carries a Z-suffixed deletedAtUtc
        public void Write_EmitsDeletedAtUtcWithZ()
        {
            var snap = MergeTestData.Snap(SyncEntityTypes.TaskNote,
                new Provenance(MergeTestData.Utc0, "dev-1", true,
                    new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc)));
            Assert.Contains("\"isDeleted\":true,\"deletedAtUtc\":\"2026-09-09T00:00:00.0000000Z\"",
                CanonicalJson.Write(snap));
        }

        // ---- K-5: canonical-only input rule -------------------------------------------------

        [Theory] // K-5
        [InlineData(SyncEntityTypes.HocKy)]
        [InlineData(SyncEntityTypes.MonHoc)]
        [InlineData(SyncEntityTypes.StudyTask)]
        [InlineData(SyncEntityTypes.StudyLog)]
        [InlineData(SyncEntityTypes.TaskNote)]
        [InlineData(SyncEntityTypes.TaskReferenceLink)]
        public void ReadThenWrite_IsByteIdentical_ForEveryEntityType(string entityType)
        {
            var snap = MergeTestData.Snap(entityType, MergeTestData.Live(555, "dev-đ"));
            var json = CanonicalJson.Write(snap);

            Assert.Equal(json, CanonicalJson.Write(CanonicalJson.Read(json)));
            Assert.Equal(snap, CanonicalJson.Read(json));
        }

        [Fact] // K-5b
        public void Read_RestoresDateTimeKinds()
        {
            var snap = MergeTestData.Snap(SyncEntityTypes.TaskReferenceLink);
            var back = CanonicalJson.Read(CanonicalJson.Write(snap));

            Assert.Equal(DateTimeKind.Utc, back.Provenance.ModifiedAtUtc.Kind);
            Assert.Equal(DateTimeKind.Utc, ((UtcValue)back.Fields["CreatedAtUtc"]).Value!.Value.Kind);

            var task = CanonicalJson.Read(CanonicalJson.Write(MergeTestData.Snap(SyncEntityTypes.StudyTask)));
            Assert.Equal(DateTimeKind.Unspecified, ((WallClockValue)task.Fields["HanChot"]).Value!.Value.Kind);
        }

        // ---- K-6: fail-closed matrix (DoR §5.5) ---------------------------------------------

        private static SnapshotContractReason ReasonOf(string json)
            => Assert.Throws<SnapshotContractException>(() => CanonicalJson.Read(json)).Reason;

        [Fact]
        public void Read_UnknownVersion_FailsClosed()
        {
            Assert.Equal(SnapshotContractReason.UnknownVersion,
                ReasonOf(TaskNoteCanonical.Replace("\"v\":1", "\"v\":2")));
            Assert.Equal(SnapshotContractReason.UnknownVersion,
                ReasonOf(TaskNoteCanonical.Replace("\"v\":1,", "")));
        }

        [Fact]
        public void Read_UnknownEntityType_FailsClosed()
        {
            Assert.Equal(SnapshotContractReason.UnknownEntityType,
                ReasonOf(TaskNoteCanonical.Replace("\"TaskNote\"", "\"Bogus\"")));
        }

        [Fact]
        public void Read_MissingRegistryField_FailsClosed()
        {
            Assert.Equal(SnapshotContractReason.MissingField,
                ReasonOf(TaskNoteCanonical.Replace(",\"Content\":\"" + EscapedContent + "\"", "")));
        }

        [Fact]
        public void Read_ExtraField_FailsClosed()
        {
            Assert.Equal(SnapshotContractReason.ExtraField,
                ReasonOf(TaskNoteCanonical.Replace("\"fields\":{", "\"fields\":{\"Rev\":3,")));
            Assert.Equal(SnapshotContractReason.ExtraField,
                ReasonOf(TaskNoteCanonical.Replace("{\"v\":1,", "{\"v\":1,\"extra\":0,")));
            Assert.Equal(SnapshotContractReason.ExtraField,
                ReasonOf(TaskNoteCanonical.Replace("\"provenance\":{", "\"provenance\":{\"rev\":1,")));
        }

        [Fact]
        public void Read_TypeMismatch_FailsClosed()
        {
            Assert.Equal(SnapshotContractReason.TypeMismatch,
                ReasonOf(TaskNoteCanonical.Replace("\"Content\":\"" + EscapedContent + "\"", "\"Content\":5")));
        }

        [Fact]
        public void Read_InvalidDateTimeShape_FailsClosed()
        {
            // offset form must be rejected outright
            Assert.Equal(SnapshotContractReason.InvalidDateTime,
                ReasonOf(TaskNoteCanonical.Replace("2026-09-08T01:02:03.1234567Z", "2026-09-08T08:02:03.1234567+07:00")));
            // Utc field without the Z
            Assert.Equal(SnapshotContractReason.InvalidDateTime,
                ReasonOf(TaskNoteCanonical.Replace("2026-09-08T01:02:03.1234567Z", "2026-09-08T01:02:03.1234567")));
            // truncated fraction
            Assert.Equal(SnapshotContractReason.InvalidDateTime,
                ReasonOf(TaskNoteCanonical.Replace("2026-09-08T01:02:03.1234567Z", "2026-09-08T01:02:03Z")));
        }

        [Fact]
        public void Read_EmptyDeviceId_FailsClosed()
        {
            Assert.Equal(SnapshotContractReason.MissingProvenance,
                ReasonOf(TaskNoteCanonical.Replace("\"dev-1\"", "\"\"")));
        }

        [Fact]
        public void Read_TombstoneWithoutTimestamp_FailsClosed()
        {
            Assert.Equal(SnapshotContractReason.TombstoneWithoutTimestamp,
                ReasonOf(TaskNoteCanonical.Replace("\"isDeleted\":false", "\"isDeleted\":true")));
        }

        [Fact]
        public void Read_NonCanonicalInput_FailsClosed()
        {
            // whitespace
            Assert.Equal(SnapshotContractReason.NonCanonical,
                ReasonOf(TaskNoteCanonical.Replace("\"fields\":", " \"fields\": ")));
            // uppercase GUID
            Assert.Equal(SnapshotContractReason.NonCanonical,
                ReasonOf(TaskNoteCanonical.Replace("0f3a1b2c-0000-0000-0000-000000000001",
                                                   "0F3A1B2C-0000-0000-0000-000000000001")));
            // field order swapped inside "fields"
            Assert.Equal(SnapshotContractReason.NonCanonical,
                ReasonOf(TaskNoteCanonical
                    .Replace("\"fields\":{\"MaTask\":\"0f3a1b2c-0000-0000-0000-000000000001\"," +
                             "\"Content\":\"" + EscapedContent + "\"}",
                             "\"fields\":{\"Content\":\"" + EscapedContent + "\"," +
                             "\"MaTask\":\"0f3a1b2c-0000-0000-0000-000000000001\"}")));
            // over-escaped non-ASCII (STJ default encoder shape)
            Assert.Equal(SnapshotContractReason.NonCanonical,
                ReasonOf(TaskNoteCanonical.Replace("ế", "\\u1ebf")));
        }

        [Fact]
        public void Read_RejectsCommentsAndTrailingCommas()
        {
            Assert.ThrowsAny<Exception>(() => CanonicalJson.Read(TaskNoteCanonical.Replace("}}", "},}")));
            Assert.ThrowsAny<Exception>(() => CanonicalJson.Read("/*c*/" + TaskNoteCanonical));
        }

        [Fact]
        public void Write_ProducesNoBomAndValidUtf8()
        {
            var bytes = Encoding.UTF8.GetBytes(CanonicalJson.Write(TaskNoteFixture()));
            Assert.NotEqual(0xEF, bytes[0]);
            Assert.Equal((byte)'{', bytes[0]);
        }
    }
}
