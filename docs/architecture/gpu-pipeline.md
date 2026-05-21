# GPU 原生推理管线设计

> 基于 RTX 4090 单卡的 GPU 加速质控管线。替代原有 CPU-only TinyBERT 串行架构。
> 目标：端到端 < 300ms，含 LLM 同步终审。

---

## 1. 架构总览

```
QcRequest
    │
    ▼
┌───────────────────────────┐
│ Level 0: Pre-filter (CPU) │  ~10ms
│ - 段落解析                 │
│ - 否定词作用域标注          │
│ - 实体提取                 │
│ - 快速规则预匹配            │
└───────────┬───────────────┘
            │
┌───────────▼───────────────┐
│ Level 1: BERT Sentinels   │  ~8ms (GPU batch)
│ MedBERT 三任务并行：       │
│ ├─ 错别字序列标注           │
│ ├─ NER 实体识别             │
│ └─ NLI 所见-结论初筛       │
└───────────┬───────────────┘
            │
┌───────────▼───────────────┐
│ 结构化聚合 (CPU)           │  ~5ms
│ - 实体关系图               │
│ - 否定标记合并              │
│ - 异常标记列表              │
└───────────┬───────────────┘
            │
    ┌───────┴───────────┐
    │                   │
┌───▼──────────┐  ┌─────▼──────────────┐
│ 规则引擎 (CPU)│  │ Hermes Skill Squad  │
│ L3 逻辑层    │  │ (GPU, 按需并行)     │
│ L4 临床层    │  │                     │
│ ~10ms        │  │ Qwen2.5-7B GPTQ-INT4│
└───┬──────────┘  │ ~80-120ms           │
    │              └─────┬──────────────┘
    └────────┬───────────┘
             │
┌────────────▼──────────────┐
│ Hermes QA 冲突仲裁 (GPU)   │  ~100ms (仅冲突时)
└────────────┬──────────────┘
             │
┌────────────▼──────────────┐
│ 评分 + 持久化 (CPU)        │  ~10ms
└────────────┬──────────────┘
             │
         QcResponse
```

---

## 2. 延迟核算（单路，无排队）

| 阶段 | P50（典型） | P95 | P99 | 备注 |
|------|:------:|:------:|:------:|------|
| Pre-filter + NLP | 10ms | 18ms | 25ms | 含分词、否定检测、段落解析 |
| BERT Sentinels (GPU) | 12ms | 20ms | 30ms | 含 CPU→GPU 传输 + tokenization + batch 推理 |
| 结构化聚合 | 5ms | 8ms | 12ms | 实体关系图构建 |
| 规则引擎 | 8ms | 18ms | 25ms | YAML 规则加载 + Regex 匹配 |
| Skill Squad (GPU，按需) | 100ms | 180ms | 300ms | 含 HTTP 往返 + vLLM 排队 + TTFT + 生成 200 tokens |
| QA 仲裁（仅冲突时） | 120ms | 200ms | 350ms | 多 Skill 结果综合判断 |
| 评分+持久化 | 8ms | 15ms | 25ms | SQLite WAL 写入 |
| **端到端（无 Skill）** | **~35ms** | **~55ms** | **~90ms** | |
| **端到端（1 Skill）** | **~140ms** | **~210ms** | **~340ms** | |
| **端到端（5 Skills + QA）** | **~240ms** | **~380ms** | **~650ms** | 并行调用，取最慢 Skill + QA |

**并发下的延迟：**
- 5 路并发：P95 增加约 1.3x（GPU batch 效益部分抵消排队）
- 10 路并发：P95 增加约 1.8x，1 Skill 场景 P95 ~380ms，仍在 1 秒预算内
- 超过 10 路：返回 429 限流

> 注：以上估算基于 RTX 4090 + Qwen2.5-7B GPTQ-INT4。实际值需在 Phase 1 基准测试中测量确认。

---

## 3. GPU 模型部署

### 3.1 MedBERT-Chinese ONNX

```bash
# ONNX 导出
optimum-cli export onnx \
  --model CrispWang/MedBERT-Chinese \
  --device cuda \
  --task feature-extraction \
  models/medbert_chinese/

# .NET 端加载
var session = new InferenceSession("models/medbert_chinese/model.onnx",
    SessionOptions.MakeSessionOptionWithCudaProvider());
```

### 3.2 Qwen2.5-7B vLLM

```bash
# 启动 vLLM 服务
python -m vllm.entrypoints.openai.api_server \
  --model Qwen/Qwen2.5-7B-Instruct-GPTQ-Int4 \
  --dtype float16 \
  --max-model-len 4096 \
  --gpu-memory-utilization 0.3 \
  --port 8100

# .NET 端调用
POST http://localhost:8100/v1/chat/completions
{
  "model": "Qwen2.5-7B-Instruct-GPTQ-Int4",
  "messages": [{"role": "user", "content": "<Skill Prompt>"}],
  "max_tokens": 200,
  "temperature": 0.1
}
```

---

## 4. GPU 降级策略

```
ONNX Runtime CUDA EP 不可用 → Warn 日志 → 降级到 CPU EP
vLLM Server 不可用 → Warn 日志 → 跳过 Skill Squad → 仅用 BERT + 规则
连续 3 次 vLLM 心跳失败 → 自动重启 vLLM 进程
恢复后 → Info 日志 → 恢复 Skill Squad
```

---

## 5. VRAM 预算

```
RTX 4090 24GB 分配：

ONNX Runtime CUDA 上下文     ██ 0.4GB (框架开销)
MedBERT-Chinese ONNX FP16    ██ 0.2GB (模型权重)
vLLM CUDA 上下文             ██ 0.5GB (框架开销)
Qwen2.5-7B GPTQ-INT4         █████████████████████ 4.5GB (模型权重，含 I/O 量化开销)
vLLM KV Cache (max-num-seqs=16) ██████████ 2.5GB
运行时临时张量                  ██ 0.4GB (中间激活、attention mask)
────────────────────────────────────
已用                            8.5GB (35%)
空闲                            15.5GB (65%)
  └── 预留给：batch 扩展 / 14B 模型 / 影像模型 / 峰值缓冲
```

> 实测值可能与理论值有 10-15% 偏差，以 `nvidia-smi` 实际数据为准。

---

## 6. 与原有架构的对比

| 维度 | AI_QC-system (CPU) | Agent_QC (GPU) |
|------|-------------------|----------------|
| BERT 推理 | TinyBERT ONNX CPU, 30-80ms | MedBERT ONNX GPU, 2-5ms |
| LLM 终审 | 无 | Qwen2.5-7B, 80-120ms |
| 并行策略 | 伪并行（顺序执行） | 真并行（Task.WhenAll + GPU batch） |
| 否定检测 | 无 | NLP 否定作用域标注 |
| 端到端延迟 | 100-500ms | 25-250ms |
| 降级策略 | TinyBERT 不可用→规则兜底 | GPU 不可用→CPU EP + 跳过 LLM |

---

## 7. 关键设计决策

| 决策点 | 选择 | 理由 |
|--------|------|------|
| BERT 三任务合并 batch | ✅ 一次 kernel launch | 减少 CUDA 调度开销，3 任务从 15ms → 8ms |
| vLLM 同机部署 | ✅ localhost:8100 | 3-5ms 网络延迟可接受，无需跨机 |
| Python 独立进程 | ✅ vLLM | C# 原生 LLM 推理库不成熟 |
| Prompt 短小 | ✅ < 300 tokens/Skill | 减少 prefill 时间，加快生成 |
| Continuous Batching | ✅ vLLM 默认 | 多 Skill 并行推理共享 GPU 算力 |
