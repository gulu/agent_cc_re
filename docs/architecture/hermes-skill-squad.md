# Hermes Agent Skill Squad 架构

> 将单一 LLM Guardian Prompt 拆分为 8 个专业审查员 Skill。每个 Skill 是独立、聚焦、可测试的 Prompt 模板。
> Hermes Orchestrator 按需调度，Skill 并行推理，QA 仲裁冲突。

---

## 1. 架构图

```
                    ┌──────────────────────┐
                    │  Hermes Orchestrator  │
                    │  (C# 调度器)           │
                    │                      │
                    │  接收 BERT 输出       │
                    │  → 按触发条件选择 Skill│
                    │  → 并行调用 vLLM      │
                    │  → 收集结果           │
                    └──────────┬───────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
┌───────▼───────┐    ┌────────▼───────┐    ┌─────────▼──────┐
│ 解剖审查员     │    │ NLI 审查员      │    │ 危急值审查员    │
│ gender-anatomy│    │ findings-nli   │    │ critical-sign  │
│ checker       │    │                │    │ arbiter        │
│               │    │                │    │                │
│ 专长：        │    │ 专长：          │    │ 专长：          │
│ 解剖-性别     │    │ 所见-结论       │    │ 急重症识别      │
│ 对应关系      │    │ 逻辑一致性      │    │ 危急值判断      │
└───────────────┘    └────────────────┘    └────────────────┘
        │                      │                      │
        └──────────────────────┼──────────────────────┘
                               │
                    ┌──────────▼──────────┐
                    │   Hermes QA          │
                    │   (冲突仲裁员)        │
                    │                      │
                    │   仅多 Skill 有冲突   │
                    │   或 Skill vs 规则    │
                    │   意见不一致时触发    │
                    └──────────────────────┘
```

---

## 2. 8 个 Skill 定义

| Skill ID | 触发条件 | 模型 | 典型延迟 | P95 | 优先级 |
|----------|----------|------|:--:|:--:|:----:|
| `gender-anatomy-checker` | BERT 提取到性别相关实体 + 否定未完全排除 | 7B | 100ms | 180ms | 1 |
| `site-consistency-checker` | 检查部位与描述部位不一致 + BERT 不确定 | 7B | 120ms | 200ms | 2 |
| `findings-impression-nli` | BERT NLI neutral/contradiction + 置信度 < 0.9 | 7B | 150ms | 250ms | 1 |
| `critical-sign-arbiter` | 危急关键词命中 + 上下文未完全否定 | 7B | 100ms | 180ms | 1 |
| `device-method-validator` | 设备/方法与描述存在关键词冲突 | 7B | 80ms | 150ms | 3 |
| `measurement-completeness` | BERT 检测到病灶 + 无尺寸实体 | 7B | 80ms | 150ms | 3 |
| `rads-compliance-checker` | RADS 映射匹配 + 结论无 RADS 标注 | 7B | 120ms | 220ms | 2 |
| `terminology-validator` | BERT 检测到非标准术语候选 | 7B | 80ms | 150ms | 4 |

> 延迟差异源于 Prompt 长度和推理复杂度：NLI 需处理两个段落对（所见 vs 结论），RADS 需检索标准对照表。实际值在 Phase 2 测量确认。

---

## 3. Skill 文件结构

```
knowledge/skills/
├── README.md                      # 索引
├── level2-semantic/
│   ├── gender-anatomy-checker.md  # Prompt 模板
│   ├── site-consistency.md
│   ├── findings-impression-nli.md
│   ├── device-method-validator.md
│   └── terminology-validator.md
├── level4-clinical/
│   ├── critical-sign-arbiter.md
│   ├── measurement-completeness.md
│   ├── rads-compliance-checker.md
│   └── scan-enhancement-checker.md
└── shared/
    ├── anatomy-atlas.md           # 解剖知识参考
    ├── rads-standards.md          # RADS 标准参考
    ├── negation-rules.md          # 否定规则参考
    └── report-templates.md        # 报告模板参考
```

---

## 4. Orchestrator 调度逻辑（伪代码）

```csharp
class HermesOrchestrator
{
    async Task<SkillResults> Dispatch(BertResults bert, ReportInput input)
    {
        var skills = new List<Skill>();

        if (HasGenderConflictPotential(input, bert))
            skills.Add(SkillRegistry.Get("gender-anatomy-checker"));
        if (HasSiteMismatch(input, bert))
            skills.Add(SkillRegistry.Get("site-consistency-checker"));
        if (bert.NliConfidence is > 0.3f and < 0.9f)
            skills.Add(SkillRegistry.Get("findings-impression-nli"));
        if (bert.CriticalKeywordHit && !bert.AllNegated)
            skills.Add(SkillRegistry.Get("critical-sign-arbiter"));
        // ... 其他触发条件

        // 并行调用 vLLM（利用 continuous batching）
        var tasks = skills.Select(s => vllmClient.ChatAsync(s.BuildPrompt(input, bert)));
        var results = await Task.WhenAll(tasks);

        // 冲突检测
        if (HasConflicts(results))
            return await QaArbiter.Review(results, input);

        return Aggregate(results);
    }
}
```

---

## 5. 冲突消解矩阵

| 规则引擎 | BERT | Skill Squad | → 最终判定 |
|:------:|:----:|:----------:|----------|
| 矛盾 | 矛盾 | 矛盾 | **矛盾** (critical) |
| 矛盾 | 矛盾 | 不矛盾 | **不矛盾**（Skill 置信度 > 0.7 → 信任 Skill；否则保留规则判定） |
| 矛盾 | 不矛盾 | 不矛盾 | **不矛盾**（Skill 置信度 > 0.7 → 信任 Skill；否则触发 QA 仲裁） |
| 不矛盾 | 不矛盾 | 矛盾 | Skill 置信度 > 0.85 → **矛盾**（标记 `LLM-found`）；否则降级为 warning |
| Skill A vs Skill B 矛盾 | — | — | 触发 Hermes QA 仲裁，QA 给出最终判定 + 置信度 |
| 规则/ BERT 矛盾 + Skill 返回低置信度 (< 0.6) | — | — | 标记为 `uncertain`，建议人工审核 |

**置信度阈值说明：**
- > 0.85：高置信度，直接采纳
- 0.70 - 0.85：中置信度，常规采纳（标注置信度供后续分析）
- 0.60 - 0.70：低置信度，降级为 warning 而非 error
- < 0.60：不可采纳，触发 QA 仲裁或标记 `uncertain`

Hermes QA 自身也需输出置信度，低于 0.6 时标记 `uncertain` 让医生人工判断。

---

## 6. 延迟分析（单路请求）

| 场景 | Skill 数 | P50 | P95 |
|------|:------:|:----:|:----:|
| 完全正常（BERT 全部通过） | 0 | 35ms | 55ms |
| 一个可疑点（触发 NLI） | 1 | 180ms | 280ms |
| 三个可疑点（并行调用） | 3 | 210ms | 340ms |
| 复杂 + QA 仲裁 | 5 + QA | 320ms | 550ms |

> 并行调用时，总 Skill 延迟 = max(各 Skill 延迟)，不是 sum。
> 以上为单路请求无排队情况。并发场景下的延迟见 `gpu-pipeline.md`。

---

## 7. 渐进式实施

| 步骤 | 工时 | 内容 |
|------|:--:|------|
| Step 1 | 1-2 天 | 拆出 3 个核心 Skill（gender / critical / nli） |
| Step 2 | 2-3 天 | Orchestrator 调度逻辑 + 触发条件矩阵 |
| Step 3 | 1-2 天 | QA 仲裁器 |
| Step 4 | 1-2 天 | 200+ 报告平行对比测试（通用 Prompt vs Squad） |
| Step 5 | 持续 | 根据测试结果增加更多 Skill |
