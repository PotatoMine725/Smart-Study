using Microsoft.ML.Tokenizers;

namespace S0Pilot;

/// <summary>
/// Per-arm configuration. Everything here is RECORDED, not invented: the prompt
/// wrapper comes from the model's own card, and the tokenizer correction is a
/// measured finding, not a guess.
/// </summary>
public sealed class Arm
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string SpModelPath { get; init; }
    /// <summary>The wrapper the model's own card mandates. NOT preprocessing of the
    /// user's text -- the user substring inside it is byte-identical across arms
    /// (BEH-04, pilot README §2.3).</summary>
    public required string Prefix { get; init; }
    public required int MaxLen { get; init; }
    public required int Rank { get; init; }
    /// <summary>HuggingFace begin-of-sequence id.</summary>
    public required int BosId { get; init; }
    /// <summary>HuggingFace end-of-sequence id.</summary>
    public required int EosId { get; init; }
    /// <summary>Added to every raw SentencePiece id to reach the HuggingFace id space.
    /// Non-zero for XLM-RoBERTa: fairseq shifted the vocabulary by one, and a .NET
    /// SentencePiece reader that returns raw ids produces a sequence that looks
    /// entirely plausible and is wrong in every position.</summary>
    public required int IdOffset { get; init; }
    public required bool SpAddBos { get; init; }
    public required bool SpAddEos { get; init; }
    /// <summary>true when the ONNX graph exposes a pooled `sentence_embedding`
    /// output; false when the harness must mean-pool `last_hidden_state` itself.</summary>
    public required bool HasSentenceEmbedding { get; init; }
    public required bool NeedsTokenTypeIds { get; init; }
    public required string OnnxFp32 { get; init; }
    public required string OnnxQuantized { get; init; }

    private Tokenizer? _tok;

    public Tokenizer Tokenizer
    {
        get
        {
            if (_tok is null)
            {
                using var fs = File.OpenRead(SpModelPath);
                _tok = SentencePieceTokenizer.Create(fs, SpAddBos, SpAddEos);
            }
            return _tok;
        }
    }

    /// <summary>Route A: the .NET tokenization path under test.</summary>
    public int[] Encode(string userText)
    {
        var raw = Tokenizer.EncodeToIds(Prefix + userText);
        var ids = new List<int>(raw.Count + 2) { BosId };
        foreach (var id in raw) ids.Add(id + IdOffset);
        ids.Add(EosId);
        // HuggingFace truncation keeps the EOS in place; reproduce that rather
        // than a bare Take(), or the last token silently changes meaning.
        if (ids.Count > MaxLen)
        {
            ids.RemoveRange(MaxLen - 1, ids.Count - (MaxLen - 1));
            ids.Add(EosId);
        }
        return ids.ToArray();
    }

    public static readonly string Repo = @"D:\Code\C#\SmartStudyPlanner";
    private static string M(params string[] p) =>
        Path.Combine(new[] { Repo, "tools", "ml-pilot", "models" }.Concat(p).ToArray());

    public static readonly Arm A = new()
    {
        Key = "arm_a",
        Label = "EmbeddingGemma-300M",
        SpModelPath = M("arm_a", "tokenizer.model"),
        Prefix = "task: classification | query: ",
        MaxLen = 2048,
        Rank = 768,
        BosId = 2,          // <bos>
        EosId = 1,          // <eos>
        IdOffset = 0,       // Gemma's SentencePiece ids are the HF ids
        SpAddBos = false,
        SpAddEos = false,
        HasSentenceEmbedding = true,
        NeedsTokenTypeIds = false,
        OnnxFp32 = M("arm_a", "onnx", "model.onnx"),
        OnnxQuantized = M("arm_a", "onnx", "model_quantized.onnx"),
    };

    public static readonly Arm B = new()
    {
        Key = "arm_b",
        Label = "multilingual-e5-small",
        SpModelPath = M("arm_b", "onnx", "sentencepiece.bpe.model"),
        Prefix = "query: ",
        MaxLen = 512,
        Rank = 384,
        BosId = 0,          // <s>
        EosId = 2,          // </s>
        IdOffset = 1,       // fairseq offset -- MEASURED, see WP-0.7 in the report
        SpAddBos = false,
        SpAddEos = false,
        HasSentenceEmbedding = false,
        NeedsTokenTypeIds = true,
        OnnxFp32 = M("arm_b", "onnx", "model.onnx"),
        OnnxQuantized = M("arm_b", "onnx", "model_qint8_avx512_vnni.onnx"),
    };

    public static Arm ByKey(string k) => k switch
    {
        "arm_a" => A,
        "arm_b" => B,
        _ => throw new ArgumentException(
            $"unknown arm '{k}'. Arm C is NOT acquired and NOT runnable: it is unlocked " +
            "only by an explicit owner decision after A and B report (EVA-06)."),
    };

    public static readonly Arm[] All = [A, B];
}
