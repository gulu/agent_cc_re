namespace Agent_QC.Services;

/// <summary>
/// 否定检测器 — 判断目标词是否处于否定语境中。
/// 基于否定词 + 作用域窗口 + 边界token + 白名单排除策略。
/// </summary>
public class NegationDetector
{
    private static readonly Dictionary<string, int> NegationScopes = new()
    {
        ["未见"] = 4,
        ["无明显"] = 4,
        ["未显示"] = 3,
        ["未及"] = 3,
        ["排除"] = 3,
        ["除外"] = 2,
    };

    private static readonly HashSet<string> ExcludeWhitelist = new(StringComparer.Ordinal)
    {
        "建议", "复查", "随访", "进一步", "可能", "需", "必要时",
    };

    private static readonly HashSet<char> BoundaryTokens = new()
    {
        '，', '。', '；', '、', '；', '\n', '\r',
    };

    /// <summary>
    /// 判断 targetWord 在 text 中是否被否定词覆盖。
    /// </summary>
    public bool IsNegated(string text, string targetWord)
    {
        var indices = FindAll(text, targetWord);
        if (indices.Count == 0) return false;

        if (ExcludeWhitelist.Contains(targetWord)) return false;

        foreach (var (negWord, negStart, negEnd) in FindNegations(text))
        {
            int scopeEnd = negEnd + NegationScopes[negWord];

            foreach (int targetIdx in indices)
            {
                if (targetIdx >= negStart && targetIdx < scopeEnd)
                {
                    if (!HasBoundaryBetween(text, negEnd, targetIdx))
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>Find all negation words in text, returns (word, startIndex, endIndex).</summary>
    private List<(string Word, int Start, int End)> FindNegations(string text)
    {
        var results = new List<(string, int, int)>();
        foreach (var kw in NegationScopes.Keys)
        {
            foreach (int idx in FindAll(text, kw))
            {
                results.Add((kw, idx, idx + kw.Length));
            }
        }
        return results;
    }

    /// <summary>Find all starting indices of pattern in text.</summary>
    private List<int> FindAll(string text, string pattern)
    {
        var indices = new List<int>();
        int idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) != -1)
        {
            indices.Add(idx);
            idx += pattern.Length;
        }
        return indices;
    }

    /// <summary>Check if a boundary token exists between two character positions.</summary>
    private bool HasBoundaryBetween(string text, int from, int to)
    {
        int lo = Math.Min(from, to);
        int hi = Math.Max(from, to);
        for (int i = lo; i < hi && i < text.Length; i++)
        {
            if (BoundaryTokens.Contains(text[i]))
                return true;
        }
        return false;
    }
}
