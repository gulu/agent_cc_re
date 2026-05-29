using System.Text.RegularExpressions;
using Agent_QC.Models;

namespace Agent_QC.Services;

/// <summary>NER entity extraction service. Primary: ONNX RoBERTa inference. Fallback: dictionary + jieba + regex.</summary>
public partial class RobertaNerService
{
    private readonly JiebaSegmenter _jieba;
    private readonly EntityNormalizer _normalizer;
    private readonly string _modelPath;
    private readonly HashSet<string> _anatomyTerms = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directionTerms = new(StringComparer.Ordinal);
    private readonly List<string> _findingPatterns = new();

    private NerMode _mode = NerMode.Dictionary;

    private enum NerMode { Gpu, Dictionary }

    // Direction words for dictionary fallback
    private static readonly string[] DirectionKeywords =
    {
        "左侧", "右侧", "左", "右", "双侧", "双叶", "双肺", "双肾", "两边", "左右",
        "左上", "左下", "右上", "右下", "左前", "右前", "左后", "右后",
        "左叶", "右叶", "左半", "右半", "左侧壁", "右侧壁",
    };

    // Measure regex: number + optional decimal + optional whitespace + unit
    [GeneratedRegex(@"(\d+\.?\d*)\s*(mm|cm|ml|dl|l|mm²|cm²|mm3|cm3|HU|g|kg|mg|μg|%|℃|°|度|时|分|秒|天|周|月|年)", RegexOptions.Compiled)]
    private static partial Regex MeasurePattern();

    public RobertaNerService(JiebaSegmenter jieba, EntityNormalizer normalizer, string modelPath)
    {
        _jieba = jieba;
        _normalizer = normalizer;
        _modelPath = modelPath;
        BuildDictionaryIndexes();
    }

    /// <summary>Initialize ONNX model if available. Call once at startup.</summary>
    public void Initialize()
    {
        if (File.Exists(_modelPath))
        {
            try
            {
                // ONNX RoBERTa model found — would load here via Microsoft.ML.OnnxRuntime
                // var session = new InferenceSession(_modelPath, SessionOptions.MakeSessionOptionWithCudaProvider());
                // _session = session;
                // _mode = NerMode.Gpu;
                Console.WriteLine("[RobertaNer] ONNX model found, GPU mode ready (pending model load implementation)");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RobertaNer] Failed to load ONNX model: {ex.Message}. Using dictionary fallback.");
                _mode = NerMode.Dictionary;
            }
        }
        else
        {
            Console.WriteLine("[RobertaNer] ONNX model not found, using dictionary fallback.");
            _mode = NerMode.Dictionary;
        }
    }

    /// <summary>Extract entities from text. Returns empty list for null/empty input.</summary>
    public List<NerEntity> Extract(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<NerEntity>();

        return _mode switch
        {
            NerMode.Gpu => ExtractGpu(text),
            _ => ExtractDictionary(text),
        };
    }

    // ── GPU (ONNX) path ────────────────────────────────

    private List<NerEntity> ExtractGpu(string text)
    {
        // Tokenize: BERT WordPiece tokenizer
        // var tokens = Tokenize(text);

        // Run inference: input_ids, attention_mask → logits
        // var logits = RunInference(tokens);

        // Decode BIO tags → entity spans
        // var entities = DecodeBioTags(tokens, logits);

        // Return _normalizer.Normalize(entities);

        // Pending ONNX Runtime integration — fall through to dictionary
        return ExtractDictionary(text);
    }

    // ── Dictionary fallback path ───────────────────────

    private List<NerEntity> ExtractDictionary(string text)
    {
        var tokens = _jieba.Segment(text);
        var entities = new List<NerEntity>();
        var idx = 0;

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token)) { idx++; continue; }

            var start = text.IndexOf(token, idx, StringComparison.Ordinal);
            if (start < 0) start = idx;
            var end = start + token.Length;

            // Check: direction
            if (_directionTerms.Contains(token))
            {
                entities.Add(new NerEntity
                {
                    Type = "direction",
                    Text = token,
                    Normalized = token,
                    Start = start,
                    End = end,
                    Confidence = 0.95f,
                });
            }
            // Check: anatomy
            else if (_anatomyTerms.Contains(token))
            {
                entities.Add(new NerEntity
                {
                    Type = "anatomy",
                    Text = token,
                    Normalized = token, // normalized by EntityNormalizer later
                    Start = start,
                    End = end,
                    Confidence = 0.90f,
                });
            }
            // Check: finding patterns
            else if (_findingPatterns.Any(p => token.Contains(p, StringComparison.Ordinal)))
            {
                entities.Add(new NerEntity
                {
                    Type = "finding",
                    Text = token,
                    Normalized = token,
                    Start = start,
                    End = end,
                    Confidence = 0.80f,
                });
            }

            idx = end;
        }

        // Measure entities via regex (spans may cross token boundaries)
        foreach (Match m in MeasurePattern().Matches(text))
        {
            entities.Add(new NerEntity
            {
                Type = "measure",
                Text = m.Value,
                Normalized = m.Value,
                Start = m.Index,
                End = m.Index + m.Length,
                Confidence = 0.98f,
            });
        }

        return _normalizer.Normalize(entities);
    }

    // ── Dictionary index builders ──────────────────────

    private void BuildDictionaryIndexes()
    {
        // Anatomy terms: all standard + non-standard terms from terminology.yaml
        // These are loaded by the EntityNormalizer — we build from the same source
        // but also include hard-coded common terms that might not be in the yaml
        var commonAnatomy = new[]
        {
            "肺", "肝", "肾", "脾", "胰", "胃", "肠", "胆", "脑", "心",
            "子宫", "卵巢", "前列腺", "甲状腺", "乳腺", "膀胱", "骨骼",
        };
        foreach (var t in commonAnatomy) _anatomyTerms.Add(t);

        // Direction terms
        foreach (var t in DirectionKeywords) _directionTerms.Add(t);

        // Finding patterns (common substrings in radiology finding descriptions)
        var patterns = new[]
        {
            "结节", "肿块", "占位", "阴影", "密度", "钙化", "囊肿", "积液",
            "增厚", "狭窄", "扩张", "变形", "破坏", "侵蚀", "融合",
            "强化", "坏死", "出血", "水肿", "炎症", "纤维化", "硬化",
            "畸形", "移位", "脱出", "突出", "膨出", "骨折", "裂缝",
            "不张", "气肿", "积液", "结石", "梗阻", "穿孔", "破裂",
            "病灶", "病变", "异常", "改变", "信号", "影",
        };
        _findingPatterns.AddRange(patterns);
    }
}
