# 模型选型与部署方案

---

## 1. 模型矩阵

| 模型 | 参数量 | 用途 | 格式 | VRAM | 单次推理 |
|------|------|------|------|:---:|:------:|
| **MedBERT-Chinese** | 110M | 错别字序列标注 / NER / NLI | ONNX FP16 (CUDA EP) | 0.2GB | 2-5ms |
| **Qwen2.5-7B-Instruct** | 7B | Hermes Skill Squad 终审 | GPTQ-Int4 (vLLM) | 5.0GB | 100-250ms |

---

## 2. MedBERT-Chinese

### 来源

- HuggingFace: `CrispWang/MedBERT-Chinese`（中文医学预训练 BERT）
- 备选：`miemie/PCL-MedBERT`、`alibaba-research/ChineseBERT`
- 如上述不可用：`bert-base-chinese` + 放射科报告 DAPT 自训练

### 微调任务

| 任务 | Head | 输入 | 输出 | 训练数据 |
|------|------|------|------|----------|
| 错别字检测 | Token Classification (BIO) | 报告句子 | O/B-ERR/I-ERR | 500+ 标注句 |
| NER 实体识别 | Token Classification (BIO) | 报告句子 | O/B-ANATOMY/I-ANATOMY 等 | 500+ 标注句 |
| NLI 一致性 | Sentence Pair Classification | (所见, 结论) | entailment/contradiction/neutral | 2000+ 标注对 |

### ONNX 导出

```bash
# 安装 optimum
pip install optimum[onnxruntime-gpu]

# 导出 ONNX（FP16 GPU）
optimum-cli export onnx \
  --model medbert-chinese-finetuned \
  --task feature-extraction \
  --device cuda \
  --fp16 \
  models/medbert_chinese/

# 三任务共享同一个 backbone，各自添加分类头
# 分类头在 C# 端实现（ONNX 输出 last_hidden_state，C# 端 pooling）
```

### C# 加载

```csharp
var sessionOptions = new SessionOptions();
sessionOptions.AppendExecutionProvider_CUDA(0);  // GPU 0 (RTX 4090)

var session = new InferenceSession("models/medbert_chinese/model.onnx", sessionOptions);

// 三任务 batch 合并（一次 kernel launch）
var inputs = new List<NamedOnnxValue>
{
    NamedOnnxValue.CreateFromTensor("input_ids", batchInputIds),
    NamedOnnxValue.CreateFromTensor("attention_mask", batchAttentionMask),
    NamedOnnxValue.CreateFromTensor("token_type_ids", batchTokenTypeIds),
};
var results = session.Run(inputs);
```

---

## 3. Qwen2.5-7B-Instruct

### 来源

- HuggingFace: `Qwen/Qwen2.5-7B-Instruct-GPTQ-Int4`
- 备选：`Qwen2.5-3B-Instruct-AWQ`、`Qwen2.5-14B-Instruct-GPTQ-Int4`（如需要更高质量）

### vLLM 部署

```bash
# 启动 vLLM OpenAI-compatible API
python -m vllm.entrypoints.openai.api_server \
  --model Qwen/Qwen2.5-7B-Instruct-GPTQ-Int4 \
  --dtype float16 \
  --max-model-len 4096 \
  --gpu-memory-utilization 0.3 \
  --max-num-seqs 16 \
  --port 8100
```

### C# 调用

```csharp
// vLLM HTTP Client
var request = new
{
    model = "Qwen2.5-7B-Instruct-GPTQ-Int4",
    messages = new[]
    {
        new { role = "system", content = skill.SystemPrompt },
        new { role = "user", content = skill.BuildUserPrompt(reportInput, bertResults) }
    },
    max_tokens = 200,
    temperature = 0.1,
    response_format = new { type = "json_object" }  // 强制 JSON 输出
};

var response = await httpClient.PostAsJsonAsync("http://localhost:8100/v1/chat/completions", request);
```

---

## 4. VRAM 使用明细

| 组件 | VRAM | 备注 |
|------|:---:|------|
| ONNX Runtime CUDA 上下文 | 0.4GB | 框架开销 |
| MedBERT-Chinese ONNX FP16 | 0.2GB | 模型权重 |
| vLLM CUDA 上下文 | 0.5GB | 框架开销 |
| Qwen2.5-7B GPTQ-Int4 | 4.5GB | 模型权重 |
| vLLM KV Cache | 2.5GB | max-num-seqs=16 |
| 运行时临时张量 | 0.4GB | 中间激活 |
| **合计** | **~8.5GB** | **RTX 4090 的 35%** |
| 空闲 | ~15.5GB | 预留扩容 |

---

## 5. 备选方案

| 组件 | 首选 | 备选 1 | 备选 2 |
|------|------|--------|--------|
| BERT 模型 | MedBERT-Chinese | PCL-MedBERT | bert-base-chinese + 放射科 DAPT |
| LLM 模型 | Qwen2.5-7B GPTQ | Qwen2.5-3B AWQ | Qwen2.5-14B GPTQ-Int4 |
| LLM 部署 | vLLM | llama.cpp server | Ollama |
| LLM 调用 | HTTP API | — | — |
| BERT 部署 | ONNX GPU | ONNX CPU (降级) | — |

## 6. 医学领域验证计划

在模型最终选定前，用 50 份典型放射科报告验证：

| 验证项 | 方法 | 通过标准 |
|--------|------|:------:|
| BERT NER 实体识别 | 与医生标注对比 | F1 ≥ 0.85 |
| BERT NLI 所见-结论 | 与医生标注对比 | 准确率 ≥ 0.80 |
| LLM 性别矛盾判断 | 20 对构造 case（10 正 + 10 负） | 准确率 ≥ 0.90 |
| LLM 危急值识别 | 20 对构造 case | 召回率 ≥ 0.95（宁可误报不可漏报） |
| LLM 术语理解 | 常见放射科缩写/术语识别 | 准确率 ≥ 0.90 |

验证脚本放在 `scripts/evaluation/` 下，每次模型变更后重新运行。
