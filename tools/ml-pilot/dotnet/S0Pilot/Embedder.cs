using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace S0Pilot;

/// <summary>
/// The encoder forward pass, on the stack that ships: Microsoft.ML.OnnxRuntime
/// <c>InferenceSession</c> + the real tokenizer (EVA-09).
///
/// One session, constructed once and reused. BEH-12 forbids paying cold-start
/// load cost per parse, and plan risk R-7 names the per-call
/// <c>CreatePredictionEngine</c> pattern as the mistake to avoid replicating.
/// </summary>
public sealed class Embedder : IDisposable
{
    private readonly Arm _arm;
    private readonly InferenceSession _session;

    public Arm Arm => _arm;
    public string ModelPath { get; }

    public Embedder(Arm arm, bool quantized)
    {
        _arm = arm;
        ModelPath = quantized ? arm.OnnxQuantized : arm.OnnxFp32;
        var so = new SessionOptions();
        // PRF-02 / AST-07: the CPU execution provider is the measurement surface
        // and the product's baseline. DirectML is acceleration only and is not
        // exercised here.
        so.AppendExecutionProvider_CPU();
        _session = new InferenceSession(ModelPath, so);
    }

    /// <summary>Runs tokenization + forward pass + pooling for one input.</summary>
    public float[] Embed(string userText)
    {
        var ids = _arm.Encode(userText);
        int n = ids.Length;

        var inputIds = new DenseTensor<long>(new[] { 1, n });
        var mask = new DenseTensor<long>(new[] { 1, n });
        for (int i = 0; i < n; i++) { inputIds[0, i] = ids[i]; mask[0, i] = 1; }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", mask),
        };
        if (_arm.NeedsTokenTypeIds)
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids",
                new DenseTensor<long>(new[] { 1, n })));

        using var results = _session.Run(inputs);

        float[] vec;
        if (_arm.HasSentenceEmbedding)
        {
            // Arm A's graph exposes a pooled, dense-projected, normalised output.
            // Using it is what "invoke the model as its card specifies" means;
            // re-pooling last_hidden_state ourselves would skip the dense layers
            // and silently produce a different representation.
            vec = results.First(r => r.Name == "sentence_embedding")
                         .AsEnumerable<float>().ToArray();
        }
        else
        {
            // Arm B: mean pooling over last_hidden_state, per its 1_Pooling
            // config (pooling_mode_mean_tokens: true), then L2 normalisation.
            var hidden = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();
            int rank = _arm.Rank;
            vec = new float[rank];
            for (int t = 0; t < n; t++)
                for (int d = 0; d < rank; d++)
                    vec[d] += hidden[0, t, d];
            for (int d = 0; d < rank; d++) vec[d] /= n;
            L2Normalize(vec);
        }

        // Plan risk R-19: the rank is a compile-time constant downstream. A
        // silent reshape here would mismatch it without failing.
        if (vec.Length != _arm.Rank)
            throw new InvalidOperationException(
                $"{_arm.Key}: encoder returned rank {vec.Length}, expected {_arm.Rank}");

        return vec;
    }

    private static void L2Normalize(float[] v)
    {
        double sum = 0;
        foreach (var x in v) sum += (double)x * x;
        var norm = Math.Sqrt(sum);
        if (norm <= 1e-12) return;
        for (int i = 0; i < v.Length; i++) v[i] = (float)(v[i] / norm);
    }

    public void Dispose() => _session.Dispose();
}
