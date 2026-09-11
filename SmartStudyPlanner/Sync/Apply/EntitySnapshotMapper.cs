using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Sync.Apply
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-5, DoR §11.3) — the entity ⇄ <see cref="EntitySnapshot"/> boundary. This is
    /// the ONLY place an EF-mapped <c>Models.*</c> entity is turned into something the pure merge core
    /// can see, and the only place a merged snapshot is written back onto a tracked row. The pure core
    /// stays free of EF (enforced by <c>MergeCorePurityTests</c>); this file is the adapter that keeps
    /// it that way.
    /// <para>
    /// Hand-written per entity, no runtime reflection — the reflection lives in
    /// <c>MergeSurfaceRegistryGuardTests</c> (DoR §4.3), which fails the build's test run if a model
    /// gains a property that this mapper and the registry do not classify.
    /// </para>
    /// <para>
    /// DateTime rule (DoR §5.4): <b>re-kind, never convert</b>. EF/SQLite reads every DateTime back as
    /// <see cref="DateTimeKind.Unspecified"/>, so Utc-class fields are re-kinded to
    /// <see cref="DateTimeKind.Utc"/> and WallClock fields to <see cref="DateTimeKind.Unspecified"/>.
    /// <c>ToUniversalTime()</c>/<c>ToLocalTime()</c> must never appear in <c>Sync/</c> — a guard test
    /// greps for them — because shifting a stored tick value by the machine's offset would make two
    /// peers fingerprint the same row differently.
    /// </para>
    /// </summary>
    internal static class EntitySnapshotMapper
    {
        // ------------------------------------------------------------------ entity -> snapshot

        public static EntitySnapshot ToSnapshot(ISyncMetadata entity) => entity switch
        {
            HocKy h => new EntitySnapshot(SyncEntityTypes.HocKy, ProvenanceOf(h), new Dictionary<string, FieldValue>(StringComparer.Ordinal)
            {
                ["Ten"] = Str(h.Ten),
                ["NgayBatDau"] = Wall(h.NgayBatDau),
            }),

            MonHoc m => new EntitySnapshot(SyncEntityTypes.MonHoc, ProvenanceOf(m), new Dictionary<string, FieldValue>(StringComparer.Ordinal)
            {
                ["MaHocKy"] = new GuidValue(m.MaHocKy),
                ["TenMonHoc"] = Str(m.TenMonHoc),
                ["SoTinChi"] = new Int32Value(m.SoTinChi),
            }),

            StudyTask t => new EntitySnapshot(SyncEntityTypes.StudyTask, ProvenanceOf(t), new Dictionary<string, FieldValue>(StringComparer.Ordinal)
            {
                ["MaMonHoc"] = new GuidValue(t.MaMonHoc),
                ["TenTask"] = Str(t.TenTask),
                ["HanChot"] = Wall(t.HanChot),
                ["TrangThai"] = Str(t.TrangThai),
                ["LoaiTask"] = new Int32Value((int)t.LoaiTask),
                ["DoKho"] = new Int32Value(t.DoKho),
                ["ThoiGianDaHoc"] = new Int32Value(t.ThoiGianDaHoc),
                ["NgayHoanThanh"] = WallN(t.NgayHoanThanh),
            }),

            StudyLog l => new EntitySnapshot(SyncEntityTypes.StudyLog, ProvenanceOf(l), new Dictionary<string, FieldValue>(StringComparer.Ordinal)
            {
                ["MaTask"] = new GuidValue(l.MaTask),
                ["NgayHoc"] = Wall(l.NgayHoc),
                ["SoPhutHoc"] = new Int32Value(l.SoPhutHoc),
                ["SoPhutDuKien"] = new Int32Value(l.SoPhutDuKien),
                ["DaHoanThanh"] = new BoolValue(l.DaHoanThanh),
                ["GhiChu"] = Str(l.GhiChu),
                ["CreatedAtUtc"] = Utc(l.CreatedAtUtc),
                ["DeviceId"] = Str(l.DeviceId),
            }),

            TaskNote n => new EntitySnapshot(SyncEntityTypes.TaskNote, ProvenanceOf(n), new Dictionary<string, FieldValue>(StringComparer.Ordinal)
            {
                ["MaTask"] = new GuidValue(n.MaTask),
                ["Content"] = Str(n.Content),
            }),

            TaskReferenceLink r => new EntitySnapshot(SyncEntityTypes.TaskReferenceLink, ProvenanceOf(r), new Dictionary<string, FieldValue>(StringComparer.Ordinal)
            {
                ["MaTask"] = new GuidValue(r.MaTask),
                ["Title"] = Str(r.Title),
                ["Url"] = Str(r.Url),
                ["Category"] = Str(r.Category),
                ["SortOrder"] = new Int32Value(r.SortOrder),
                ["CreatedAtUtc"] = Utc(r.CreatedAtUtc),
            }),

            _ => throw new MergeContractViolationException(
                     $"{entity.GetType().Name} is not one of the six synced entities."),
        };

        // ------------------------------------------------------------------ snapshot -> entity

        /// <summary>
        /// Writes every snapshot field plus the provenance block onto <paramref name="entity"/>.
        /// <c>Rev</c> is deliberately untouched: it is a local-only counter and the apply seam bumps it
        /// exactly once at SaveChanges (DoR §11.1). Identity (the PK) is untouched too — the caller
        /// located or created the row by it. Excluded classes (Derived / NotMapped) keep whatever the
        /// local row or the model default already holds (DoR §4.2 "apply on update: keep local").
        /// </summary>
        public static void ApplyTo(ISyncMetadata entity, EntitySnapshot snapshot)
        {
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));

            switch (entity)
            {
                case HocKy h:
                    Expect(snapshot, SyncEntityTypes.HocKy);
                    h.Ten = ReadStr(snapshot, "Ten")!;
                    h.NgayBatDau = ReadWall(snapshot, "NgayBatDau");
                    break;

                case MonHoc m:
                    Expect(snapshot, SyncEntityTypes.MonHoc);
                    m.MaHocKy = ReadGuid(snapshot, "MaHocKy");
                    m.TenMonHoc = ReadStr(snapshot, "TenMonHoc")!;
                    m.SoTinChi = ReadInt(snapshot, "SoTinChi");
                    break;

                case StudyTask t:
                    Expect(snapshot, SyncEntityTypes.StudyTask);
                    t.MaMonHoc = ReadGuid(snapshot, "MaMonHoc");
                    t.TenTask = ReadStr(snapshot, "TenTask")!;
                    t.HanChot = ReadWall(snapshot, "HanChot");
                    t.TrangThai = ReadStr(snapshot, "TrangThai")!;
                    t.LoaiTask = (LoaiCongViec)ReadInt(snapshot, "LoaiTask");
                    t.DoKho = ReadInt(snapshot, "DoKho");
                    t.ThoiGianDaHoc = ReadInt(snapshot, "ThoiGianDaHoc");
                    t.NgayHoanThanh = ReadWallN(snapshot, "NgayHoanThanh");
                    break;

                case StudyLog l:
                    Expect(snapshot, SyncEntityTypes.StudyLog);
                    l.MaTask = ReadGuid(snapshot, "MaTask");
                    l.NgayHoc = ReadWall(snapshot, "NgayHoc");
                    l.SoPhutHoc = ReadInt(snapshot, "SoPhutHoc");
                    l.SoPhutDuKien = ReadInt(snapshot, "SoPhutDuKien");
                    l.DaHoanThanh = ReadBool(snapshot, "DaHoanThanh");
                    l.GhiChu = ReadStr(snapshot, "GhiChu");
                    l.CreatedAtUtc = ReadUtc(snapshot, "CreatedAtUtc");
                    l.DeviceId = ReadStr(snapshot, "DeviceId") ?? string.Empty;
                    break;

                case TaskNote n:
                    Expect(snapshot, SyncEntityTypes.TaskNote);
                    n.MaTask = ReadGuid(snapshot, "MaTask");
                    n.Content = ReadStr(snapshot, "Content");
                    break;

                case TaskReferenceLink r:
                    Expect(snapshot, SyncEntityTypes.TaskReferenceLink);
                    r.MaTask = ReadGuid(snapshot, "MaTask");
                    r.Title = ReadStr(snapshot, "Title") ?? string.Empty;
                    r.Url = ReadStr(snapshot, "Url") ?? string.Empty;
                    r.Category = ReadStr(snapshot, "Category");
                    r.SortOrder = ReadInt(snapshot, "SortOrder");
                    r.CreatedAtUtc = ReadUtc(snapshot, "CreatedAtUtc");
                    break;

                default:
                    throw new MergeContractViolationException(
                        $"{entity.GetType().Name} is not one of the six synced entities.");
            }

            WriteProvenance(entity, snapshot.Provenance);
        }

        /// <summary>
        /// Builds a brand-new, untracked entity of <paramref name="entityType"/> with the given PK and
        /// snapshot content. Model defaults supply the excluded Derived columns that are NOT NULL in
        /// the schema (<c>StudyTask.MucDoCanhBao</c> exists precisely so non-UI write paths do not hit
        /// SQLite Error 19 — see its comment in the model).
        /// </summary>
        public static ISyncMetadata CreateEntity(string entityType, Guid entityId, EntitySnapshot snapshot)
        {
            ISyncMetadata entity = entityType switch
            {
                SyncEntityTypes.HocKy => new HocKy { MaHocKy = entityId },
                SyncEntityTypes.MonHoc => new MonHoc { MaMonHoc = entityId },
                SyncEntityTypes.StudyTask => new StudyTask { MaTask = entityId },
                SyncEntityTypes.StudyLog => new StudyLog { Id = entityId },
                SyncEntityTypes.TaskNote => new TaskNote { Id = entityId },
                SyncEntityTypes.TaskReferenceLink => new TaskReferenceLink { Id = entityId },
                _ => throw new MergeContractViolationException($"Unknown entity type '{entityType}'."),
            };

            ApplyTo(entity, snapshot);
            return entity;
        }

        public static Guid IdOf(ISyncMetadata entity) => entity switch
        {
            HocKy h => h.MaHocKy,
            MonHoc m => m.MaMonHoc,
            StudyTask t => t.MaTask,
            StudyLog l => l.Id,
            TaskNote n => n.Id,
            TaskReferenceLink r => r.Id,
            _ => throw new MergeContractViolationException($"{entity.GetType().Name} is not a synced entity."),
        };

        public static string EntityTypeOf(ISyncMetadata entity) => entity switch
        {
            HocKy => SyncEntityTypes.HocKy,
            MonHoc => SyncEntityTypes.MonHoc,
            StudyTask => SyncEntityTypes.StudyTask,
            StudyLog => SyncEntityTypes.StudyLog,
            TaskNote => SyncEntityTypes.TaskNote,
            TaskReferenceLink => SyncEntityTypes.TaskReferenceLink,
            _ => throw new MergeContractViolationException($"{entity.GetType().Name} is not a synced entity."),
        };

        // ------------------------------------------------------------------ provenance

        private static Provenance ProvenanceOf(ISyncMetadata m) => new(
            DateTime.SpecifyKind(m.ModifiedAtUtc, DateTimeKind.Utc),
            m.ModifiedByDeviceId ?? string.Empty,
            m.IsDeleted,
            m.DeletedAtUtc is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null);

        private static void WriteProvenance(ISyncMetadata m, Provenance p)
        {
            m.ModifiedAtUtc = p.ModifiedAtUtc;
            m.ModifiedByDeviceId = p.ModifiedByDeviceId;
            m.IsDeleted = p.IsDeleted;
            m.DeletedAtUtc = p.DeletedAtUtc;
        }

        // ------------------------------------------------------------------ value helpers

        private static FieldValue Str(string? v) => new StringValue(v);
        private static FieldValue Wall(DateTime v) => new WallClockValue(DateTime.SpecifyKind(v, DateTimeKind.Unspecified));
        private static FieldValue WallN(DateTime? v) =>
            new WallClockValue(v is { } x ? DateTime.SpecifyKind(x, DateTimeKind.Unspecified) : null);
        private static FieldValue Utc(DateTime v) => new UtcValue(DateTime.SpecifyKind(v, DateTimeKind.Utc));

        private static void Expect(EntitySnapshot snapshot, string entityType)
        {
            if (!StringComparer.Ordinal.Equals(snapshot.EntityType, entityType))
            {
                throw new MergeContractViolationException(
                    $"Cannot apply a {snapshot.EntityType} snapshot to a {entityType} row.");
            }
        }

        private static FieldValue Field(EntitySnapshot s, string name) =>
            s.Fields.TryGetValue(name, out var v)
                ? v
                : throw new MergeContractViolationException($"{s.EntityType} snapshot is missing {name}.");

        private static string? ReadStr(EntitySnapshot s, string name) =>
            Field(s, name) is StringValue v ? v.Value : throw Mismatch(s, name, "string");

        private static int ReadInt(EntitySnapshot s, string name) =>
            Field(s, name) is Int32Value v ? v.Value : throw Mismatch(s, name, "int");

        private static bool ReadBool(EntitySnapshot s, string name) =>
            Field(s, name) is BoolValue v ? v.Value : throw Mismatch(s, name, "bool");

        private static Guid ReadGuid(EntitySnapshot s, string name) =>
            Field(s, name) is GuidValue v ? v.Value : throw Mismatch(s, name, "Guid");

        private static DateTime ReadWall(EntitySnapshot s, string name) =>
            Field(s, name) is WallClockValue { Value: { } x } ? x : throw Mismatch(s, name, "non-null WallClock");

        private static DateTime? ReadWallN(EntitySnapshot s, string name) =>
            Field(s, name) is WallClockValue v ? v.Value : throw Mismatch(s, name, "WallClock");

        private static DateTime ReadUtc(EntitySnapshot s, string name) =>
            Field(s, name) is UtcValue { Value: { } x } ? x : throw Mismatch(s, name, "non-null Utc");

        private static MergeContractViolationException Mismatch(EntitySnapshot s, string name, string expected) =>
            new($"{s.EntityType}.{name} is not a {expected} value.");
    }
}
