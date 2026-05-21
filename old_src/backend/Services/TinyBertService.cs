// TinyBertService — TinyBERT ONNX Runtime 推理服务
// 加载 tinybert_clinical.onnx，提供特征提取和语义分析
// 未加载时自动降级到规则兜底

using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ReportQC.Services;

public static class TinyBertService
{
    private static InferenceSession? _session;
    private static readonly object _lock = new();
    private static TinyBertTokenizer? _tokenizer;

    private const int MaxSeqLen = 128;

    /// <summary>模型是否已加载</summary>
    public static bool IsLoaded => _session != null;

    /// <summary>尝试加载模型和分词器</summary>
    public static void TryLoad(string? modelPath = null)
    {
        if (_session != null) return;

        var baseDir = AppContext.BaseDirectory;
        var modelsDir = Path.Combine(baseDir, "models");
        var onnxPath = modelPath ?? Path.Combine(modelsDir, "tinybert_clinical.onnx");

        if (!File.Exists(onnxPath))
        {
            JSBaseLogs.JSLogManager.WriteLog(
                $"TinyBERT ONNX 文件不存在：{onnxPath}", "WARN");
            return;
        }

        try
        {
            lock (_lock)
            {
                if (_session != null) return;

                // 加载分词器
                _tokenizer = TinyBertTokenizer.Load(modelsDir);
                if (_tokenizer == null)
                {
                    JSBaseLogs.JSLogManager.WriteLog("分词器加载失败", "WARN");
                    return;
                }

                // 加载 ONNX 会话
                var opts = new Microsoft.ML.OnnxRuntime.SessionOptions();
                _session = new InferenceSession(onnxPath, opts);

                JSBaseLogs.JSLogManager.WriteLog(
                    $"TinyBERT 模型加载成功：{onnxPath}", "INFO");
            }
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(
                $"TinyBERT 模型加载失败：{ex.Message}，走规则兜底", "WARN");
        }
    }

    /// <summary>
    /// 对输入文本进行推理，返回句子嵌入向量
    /// </summary>
    public static float[]? GetEmbedding(string text)
    {
        if (_session == null || _tokenizer == null) return null;

        try
        {
            var (inputIds, attentionMask, tokenTypeIds) = _tokenizer.Encode(text, MaxSeqLen);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids",
                    new DenseTensor<long>(inputIds, new[] { 1, MaxSeqLen })),
                NamedOnnxValue.CreateFromTensor("attention_mask",
                    new DenseTensor<long>(attentionMask, new[] { 1, MaxSeqLen })),
                NamedOnnxValue.CreateFromTensor("token_type_ids",
                    new DenseTensor<long>(tokenTypeIds, new[] { 1, MaxSeqLen })),
            };

            using var results = _session.Run(inputs);

            // 取 pooler_output 作为句子嵌入
            var poolerOutput = results.First(o => o.Name == "pooler_output")
                .AsTensor<float>()
                .ToArray();

            return poolerOutput;
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog($"TinyBERT 推理失败：{ex.Message}", "WARN");
            return null;
        }
    }

    /// <summary>
    /// 计算两个向量的余弦相似度
    /// </summary>
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom > 0 ? dot / denom : 0;
    }
}

/// <summary>
/// 简易中文分词器（兼容 tokenizer.json 格式）
/// </summary>
internal class TinyBertTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private readonly List<string> _idToToken;

    private TinyBertTokenizer(Dictionary<string, int> vocab)
    {
        _vocab = vocab;
        _idToToken = vocab.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
    }

    public static TinyBertTokenizer? Load(string modelsDir)
    {
        // 尝试多种路径加载 vocab
        var vocabPaths = new[]
        {
            Path.Combine(modelsDir, "tokenizer", "tokenizer.json"),
            Path.Combine(modelsDir, "vocab.txt"),
            Path.Combine(modelsDir, "tokenizer", "vocab.txt"),
        };

        foreach (var path in vocabPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    if (path.EndsWith(".json"))
                    {
                        // 从 tokenizer.json 解析 vocab
                        var json = File.ReadAllText(path);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("model", out var model) &&
                            model.TryGetProperty("vocab", out var vocab))
                        {
                            var dict = new Dictionary<string, int>();
                            foreach (var kv in vocab.EnumerateObject())
                            {
                                dict[kv.Name] = kv.Value.GetInt32();
                            }
                            if (dict.Count > 0)
                                return new TinyBertTokenizer(dict);
                        }
                    }
                    else
                    {
                        // 从 vocab.txt 逐行读取
                        var lines = File.ReadAllLines(path);
                        var dict = new Dictionary<string, int>();
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var token = lines[i].Trim();
                            if (!string.IsNullOrEmpty(token))
                                dict[token] = i;
                        }
                        if (dict.Count > 0)
                            return new TinyBertTokenizer(dict);
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 将文本编码为 BERT 输入
    /// </summary>
    public (long[] inputIds, long[] attentionMask, long[] tokenTypeIds) Encode(
        string text, int maxLen)
    {
        // 基础中文分词：按字符拆分 + 特殊 token
        var tokens = new List<string> { "[CLS]" };

        foreach (var ch in text)
        {
            var token = ch.ToString();
            // 检查是否在词表中
            if (_vocab.ContainsKey(token))
                tokens.Add(token);
            else
                tokens.Add("[UNK]");
        }

        tokens.Add("[SEP]");

        // 截断
        if (tokens.Count > maxLen - 1)
        {
            tokens = tokens.Take(maxLen - 1).ToList();
            tokens.Add("[SEP]");
        }

        // Padding
        while (tokens.Count < maxLen)
            tokens.Add("[PAD]");

        var inputIds = tokens.Select(t => _vocab.GetValueOrDefault(t, _vocab.GetValueOrDefault("[UNK]", 100)))
                             .Select(v => (long)v)
                             .ToArray();
        var attentionMask = tokens.Select(t => t == "[PAD]" ? 0L : 1L).ToArray();
        var tokenTypeIds = new long[maxLen];

        return (inputIds, attentionMask, tokenTypeIds);
    }
}
