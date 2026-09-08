using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartStudyPlanner.Sync.Merge
{
    /// <summary>
    /// Canonical SnapshotJson v1 (DoR §5). Hand-written on purpose: System.Text.Json's encoders
    /// are deterministic today but their escaping is not a contract across runtime versions, and a
    /// stored fingerprint has to recompute identically after an app update.
    /// </summary>
    public static class CanonicalJson
    {
        public const int Version = 1;

        // Literals are quoted so no character can be mistaken for a format specifier.
        private const string UtcFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
        private const string WallClockFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly JsonDocumentOptions ReaderOptions = new()
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 4
        };

        // ---- writer ---------------------------------------------------------------------

        public static string Write(EntitySnapshot snapshot)
        {
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            var spec = MergeSurfaceRegistry.Get(snapshot.EntityType);

            if (snapshot.Fields.Count != spec.SnapshotFields.Count)
            {
                throw new SnapshotContractException(SnapshotContractReason.ExtraField,
                    $"{snapshot.EntityType} carries {snapshot.Fields.Count} fields, registry declares {spec.SnapshotFields.Count}.");
            }

            var sb = new StringBuilder(256);
            sb.Append("{\"v\":").Append(Version.ToString(Inv)).Append(",\"entityType\":");
            AppendString(sb, snapshot.EntityType);

            var p = snapshot.Provenance;
            sb.Append(",\"provenance\":{\"modifiedAtUtc\":");
            AppendDateTime(sb, p.ModifiedAtUtc, utc: true);
            sb.Append(",\"modifiedByDeviceId\":");
            AppendString(sb, p.ModifiedByDeviceId);
            sb.Append(",\"isDeleted\":").Append(p.IsDeleted ? "true" : "false");
            sb.Append(",\"deletedAtUtc\":");
            if (p.DeletedAtUtc is null) sb.Append("null"); else AppendDateTime(sb, p.DeletedAtUtc.Value, utc: true);
            sb.Append("},\"fields\":{");

            for (var i = 0; i < spec.SnapshotFields.Count; i++)
            {
                var f = spec.SnapshotFields[i];
                if (!snapshot.Fields.TryGetValue(f.Name, out var value))
                {
                    throw new SnapshotContractException(SnapshotContractReason.MissingField,
                        $"{snapshot.EntityType}.{f.Name} is absent from the snapshot.");
                }

                if (i > 0) sb.Append(',');
                AppendString(sb, f.Name);
                sb.Append(':').Append(WriteFieldValue(value));
            }

            return sb.Append("}}").ToString();
        }

        /// <summary>Canonical encoding of one scalar (DoR §5.2). Also the LWW third component.</summary>
        public static string WriteFieldValue(FieldValue value) => value switch
        {
            StringValue s => s.Value is null ? "null" : Quote(s.Value),
            Int32Value i => i.Value.ToString(Inv),
            BoolValue b => b.Value ? "true" : "false",
            GuidValue g => "\"" + g.Value.ToString("D", Inv) + "\"",
            WallClockValue w => w.Value is null ? "null" : "\"" + w.Value.Value.ToString(WallClockFormat, Inv) + "\"",
            UtcValue u => u.Value is null ? "null" : "\"" + u.Value.Value.ToString(UtcFormat, Inv) + "\"",
            _ => throw new MergeContractViolationException($"Unknown FieldValue shape '{value?.GetType().Name}'.")
        };

        private static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            AppendString(sb, s);
            return sb.ToString();
        }

        // RFC 8785 §3.2.2.2 escape set, and nothing else: all non-ASCII stays raw UTF-8.
        private static void AppendString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\r': sb.Append("\\r"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4", Inv));
                        else sb.Append(ch);
                        break;
                }
            }
            sb.Append('"');
        }

        // Re-kind, never convert: the machine's time zone must not touch sync data (DoR §5.4).
        private static void AppendDateTime(StringBuilder sb, DateTime value, bool utc) =>
            sb.Append('"').Append(value.ToString(utc ? UtcFormat : WallClockFormat, Inv)).Append('"');

        // ---- reader ---------------------------------------------------------------------

        public static EntitySnapshot Read(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new SnapshotContractException(SnapshotContractReason.NonCanonical, "empty input.");

            using var doc = JsonDocument.Parse(json, ReaderOptions);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new SnapshotContractException(SnapshotContractReason.NonCanonical, "root is not an object.");

            RequireMembers(root, "top level", "v", "entityType", "provenance", "fields");

            if (!root.TryGetProperty("v", out var vEl) || vEl.ValueKind != JsonValueKind.Number
                || !vEl.TryGetInt32(out var v) || v != Version)
            {
                throw new SnapshotContractException(SnapshotContractReason.UnknownVersion,
                    "snapshot version is absent or not 1.");
            }

            if (!root.TryGetProperty("entityType", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
                throw new SnapshotContractException(SnapshotContractReason.UnknownEntityType, "entityType is not a string.");

            var spec = MergeSurfaceRegistry.Get(typeEl.GetString()!);
            var provenance = ReadProvenance(root.GetProperty("provenance"));

            var fieldsEl = root.GetProperty("fields");
            if (fieldsEl.ValueKind != JsonValueKind.Object)
                throw new SnapshotContractException(SnapshotContractReason.TypeMismatch, "fields is not an object.");

            RequireMembers(fieldsEl, "fields", Names(spec));

            var fields = new Dictionary<string, FieldValue>(StringComparer.Ordinal);
            foreach (var f in spec.SnapshotFields)
            {
                if (!fieldsEl.TryGetProperty(f.Name, out var el))
                {
                    throw new SnapshotContractException(SnapshotContractReason.MissingField,
                        $"{spec.EntityType}.{f.Name} is absent.");
                }
                fields[f.Name] = ReadFieldValue(spec.EntityType, f, el);
            }

            var snapshot = new EntitySnapshot(spec.EntityType, provenance, fields);

            // Canonical-only input rule (DoR §5.2): one check that enforces field order, absence of
            // whitespace, lowercase GUIDs, exact DateTime shapes and the exact escape form.
            if (!string.Equals(Write(snapshot), json, StringComparison.Ordinal))
            {
                throw new SnapshotContractException(SnapshotContractReason.NonCanonical,
                    "input is not the canonical serialization of its own content.");
            }

            return snapshot;
        }

        private static string[] Names(EntitySpec spec)
        {
            var names = new string[spec.SnapshotFields.Count];
            for (var i = 0; i < names.Length; i++) names[i] = spec.SnapshotFields[i].Name;
            return names;
        }

        private static void RequireMembers(JsonElement obj, string where, params string[] allowed)
        {
            foreach (var member in obj.EnumerateObject())
            {
                var known = false;
                foreach (var a in allowed)
                {
                    if (string.Equals(a, member.Name, StringComparison.Ordinal)) { known = true; break; }
                }
                if (!known)
                {
                    throw new SnapshotContractException(SnapshotContractReason.ExtraField,
                        $"'{member.Name}' is not part of the {where} contract.");
                }
            }
        }

        private static Provenance ReadProvenance(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Object)
                throw new SnapshotContractException(SnapshotContractReason.TypeMismatch, "provenance is not an object.");

            RequireMembers(el, "provenance", "modifiedAtUtc", "modifiedByDeviceId", "isDeleted", "deletedAtUtc");

            var modifiedAtUtc = ReadUtc(RequireMember(el, "modifiedAtUtc"), "provenance.modifiedAtUtc")
                ?? throw new SnapshotContractException(SnapshotContractReason.MissingProvenance,
                    "modifiedAtUtc must not be null.");

            var deviceEl = RequireMember(el, "modifiedByDeviceId");
            if (deviceEl.ValueKind != JsonValueKind.String)
                throw new SnapshotContractException(SnapshotContractReason.TypeMismatch, "modifiedByDeviceId is not a string.");
            var deviceId = deviceEl.GetString()!;
            if (deviceId.Length == 0)
                throw new SnapshotContractException(SnapshotContractReason.MissingProvenance, "modifiedByDeviceId is empty.");

            var deletedEl = RequireMember(el, "isDeleted");
            if (deletedEl.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new SnapshotContractException(SnapshotContractReason.TypeMismatch, "isDeleted is not a boolean.");
            var isDeleted = deletedEl.GetBoolean();

            var deletedAtUtc = ReadUtc(RequireMember(el, "deletedAtUtc"), "provenance.deletedAtUtc");
            if (isDeleted && deletedAtUtc is null)
            {
                throw new SnapshotContractException(SnapshotContractReason.TombstoneWithoutTimestamp,
                    "isDeleted is true but deletedAtUtc is null.");
            }

            return new Provenance(modifiedAtUtc, deviceId, isDeleted, deletedAtUtc);
        }

        private static JsonElement RequireMember(JsonElement obj, string name) =>
            obj.TryGetProperty(name, out var el)
                ? el
                : throw new SnapshotContractException(SnapshotContractReason.MissingField, $"'{name}' is absent.");

        private static DateTime? ReadUtc(JsonElement el, string where)
        {
            if (el.ValueKind == JsonValueKind.Null) return null;
            if (el.ValueKind != JsonValueKind.String)
                throw new SnapshotContractException(SnapshotContractReason.TypeMismatch, $"{where} is not a string.");

            return DateTime.TryParseExact(el.GetString(), UtcFormat, Inv,
                       DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
                ? parsed
                : throw new SnapshotContractException(SnapshotContractReason.InvalidDateTime,
                    $"{where} is not '{UtcFormat}'.");
        }

        private static FieldValue ReadFieldValue(string entityType, FieldSpec f, JsonElement el)
        {
            var where = $"{entityType}.{f.Name}";

            if (f.ValueType == typeof(string))
            {
                return el.ValueKind switch
                {
                    JsonValueKind.String => new StringValue(el.GetString()),
                    JsonValueKind.Null => new StringValue(null),
                    _ => throw Mismatch(where, "string")
                };
            }

            if (f.ValueType == typeof(int))
            {
                return el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i)
                    ? new Int32Value(i)
                    : throw Mismatch(where, "int");
            }

            if (f.ValueType == typeof(bool))
            {
                return el.ValueKind switch
                {
                    JsonValueKind.True => new BoolValue(true),
                    JsonValueKind.False => new BoolValue(false),
                    _ => throw Mismatch(where, "bool")
                };
            }

            if (f.ValueType == typeof(Guid))
            {
                if (el.ValueKind != JsonValueKind.String) throw Mismatch(where, "Guid");
                return Guid.TryParseExact(el.GetString(), "D", out var g)
                    ? new GuidValue(g)
                    : throw Mismatch(where, "Guid");
            }

            if (f.ValueType == typeof(DateTime))
            {
                if (el.ValueKind == JsonValueKind.Null)
                    return f.IsUtc ? new UtcValue(null) : (FieldValue)new WallClockValue(null);
                if (el.ValueKind != JsonValueKind.String) throw Mismatch(where, "DateTime");

                if (f.IsUtc) return new UtcValue(ReadUtc(el, where));

                return DateTime.TryParseExact(el.GetString(), WallClockFormat, Inv, DateTimeStyles.None, out var wall)
                    ? new WallClockValue(wall)
                    : throw new SnapshotContractException(SnapshotContractReason.InvalidDateTime,
                        $"{where} is not '{WallClockFormat}'.");
            }

            throw new MergeContractViolationException($"{where} has unsupported registry type {f.ValueType}.");
        }

        private static SnapshotContractException Mismatch(string where, string expected) =>
            new(SnapshotContractReason.TypeMismatch, $"{where} is not a {expected}.");

        // ---- fingerprint (DoR §5.3) ------------------------------------------------------

        /// <summary>Lowercase hex SHA-256 of the canonical UTF-8 bytes; <c>"null"</c> for a null snapshot.</summary>
        public static string Fingerprint(EntitySnapshot? snapshot) =>
            snapshot is null ? "null" : Sha256Hex(Write(snapshot));

        internal static string Sha256Hex(string text) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}
