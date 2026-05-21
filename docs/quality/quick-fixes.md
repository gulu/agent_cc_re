# 快速止血清单（本周可执行）

> 不需要引入任何新模型或新依赖，纯 C# 代码加固。
> 预期可将"低级错误"减少 50-60%。

---

| # | 行动 | 解决盲区 | 工作量 | 具体操作 |
|---|------|----------|:----:|----------|
| 1 | 所有规则加 `exclude_patterns` 白名单 | 否定语境 | 1 天 | 每条规则新增 `exclude_patterns` YAML 字段 |
| 2 | 基础否定词预检 | 否定语境 | 0.5 天 | 新增 `NegationDetector`，检测"未见/无/排除" |
| 3 | 缩小高危规则关键词粒度 | 多义词 | 1 天 | "子宫"→"子宫肌瘤/子宫内膜癌"等病理词 |
| 4 | 规则仅在 Findings/Impression 段生效 | 引用/部位重叠 | 0.5 天 | `SectionParser` 识别段落标题 |
| 5 | 识别引用模式并跳过 | 引用语境 | 1 天 | 正则识别"前片比较""外院""建议"等模式 |
| 6 | 区分 anatomy_only 和 pathology 匹配级别 | 多义词/严重度 | 0.5 天 | 规则新增 `context` 字段 |
| **合计** | | | **3-4 天** | |

---

## 各行动详情

### 1. 添加 exclude_patterns 白名单

```csharp
// KnowledgeBase 实体新增字段
public string? ExcludePatterns { get; set; }  // JSON array of regex patterns

// 规则匹配时检查
bool IsExcluded(string content, string excludePatternsJson)
{
    var patterns = JsonConvert.DeserializeObject<string[]>(excludePatternsJson);
    return patterns?.Any(p => Regex.IsMatch(content, p)) ?? false;
}
```

### 2. 基础否定词预检

```csharp
class NegationDetector
{
    // 否定词（含短语）
    static readonly string[] NegationWords = { 
        "未见", "无", "未发现", "未探及", "排除", "不考虑", 
        "未显示", "未检出", "无明确", "无明显", "无异常",
        "未见明确", "未见明显"
    };
    
    // 否定终止符（这些标点后否定作用域结束）
    static readonly char[] NegationBoundary = { '。', '；', '!', '\n' };
    
    // 否定作用域半径：否定词后 15 个字以内的实体被否定
    const int NegationScope = 15;
    
    bool IsNegated(string text, Entity entity)
    {
        // 从实体位置向前搜索最近的否定词
        int searchStart = Math.Max(0, entity.StartPos - NegationScope);
        string prefix = text[searchStart..entity.StartPos];
        
        foreach (var negWord in NegationWords)
        {
            int idx = prefix.LastIndexOf(negWord);
            if (idx == -1) continue;
            
            // 检查否定词和实体之间是否有否定终止符
            string between = prefix[(idx + negWord.Length)..];
            if (between.IndexOfAny(NegationBoundary) == -1)
                return true;  // 否定作用域内
        }
        return false;
    }
}
```

> 注意：基础版本无法处理"未见异常，但建议随访"中的"未见异常"否定作用域结束。真正精确的否定检测需要依赖构建或微调的 NER 模型。Phase 2 中 MedBERT 微调后将提供更精确的否定-实体关系分类。

### 3. 关键词粒度缩小

| 当前 keyword | 改为 keywords[] | 级别 |
|-------------|----------------|------|
| `"子宫"` | `["子宫肌瘤", "子宫内膜癌", "子宫腺肌症", "宫腔积液", "宫颈癌"]` | pathology |
| `"乳腺"` | `["乳腺癌", "乳腺结节", "乳腺增生", "乳腺占位"]` | pathology |
| `"附件"` | `["子宫附件", "双侧附件区", "卵巢附件"]` | pathology |
| `"出血"` | `["活动性出血", "大量出血", "急性出血", "出血不止"]` | critical_sign |

### 4-6. 段落归属 + 引用识别 + 级别区分

见 `layered-defense.md` 第二层和第三层的详细设计。
