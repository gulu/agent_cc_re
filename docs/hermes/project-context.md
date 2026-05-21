# 项目核心约束 (project-context.md)

> Agent_QC 项目的不可变约束。所有 Agent 必须遵守。
> 违反 = 需求变更，需走 ADR 流程记录到 `memory.md`。

---

## 一、技术栈

| 层 | 技术 | 说明 |
|----|------|------|
| 运行时 | .NET 8 + ASP.NET Core | 纯后端 API，端口 5100 |
| ORM | FreeSql CodeFirst（开发）/ Migration（生产） | SQLite WAL 模式（单机部署），命名 `PascalCaseToUnderscore` |
| GPU 推理 (BERT) | ONNX Runtime GPU (CUDA EP) | MedBERT-Chinese ONNX FP16 |
| GPU 推理 (LLM) | vLLM HTTP Server (Python) | Qwen2.5-7B GPTQ-INT4 |
| 规则引擎 | C# + 内存 Dictionary | Level 3-4 逻辑层 |
| 知识库 | YAML 文件（外部化） | 替代硬编码在 C# 源码中 |
| 测试 | xUnit + Moq | 覆盖率 ≥ 80% |
| 日志 | JSBaseLogs | 文件 + 数据库双写 |

---

## 二、硬件约束

| 指标 | 约束 |
|------|------|
| GPU | RTX 4090 × 1（24GB VRAM，~82 TFLOPS FP16） |
| VRAM 预算 | 总使用 < 8GB（预留 16GB 扩展空间） |
| 端到端延迟 | < 1 秒（目标 < 300ms） |
| 并发 | 支持 10 路并发质控请求 |

### 模型 VRAM 分配

| 模型 | 格式 | VRAM |
|------|------|------|
| MedBERT-Chinese | ONNX FP16 | ~0.2GB |
| Qwen2.5-7B GPTQ-INT4 | vLLM | ~5.0GB |
| vLLM KV Cache | — | ~2.0GB |
| **合计** | | **~7.2GB (30%)** |

### 并发与GPU资源模型

| 场景 | GPU 行为 | 延迟影响 |
|------|----------|----------|
| 单路请求 | BERT batch=1，vLLM single sequence | 基准延迟 |
| 3-5 路并发 | BERT batch 合并，vLLM continuous batching | +20-40ms |
| 10 路并发（峰值） | BERT batch 排队，vLLM 队列深度 10 | P95 可达 800ms |
| 超载 | ONNX 排队超时(100ms) → 降级 CPU EP | 不崩溃 |

**并发控制策略：**
- BERT 推理使用 `Channel<T>` 缓冲，每 5ms 窗口内到达的请求合并为一个 batch
- vLLM 使用 `max-num-seqs=16`，10 路并发在 capacity 内
- 超过 10 路并发时返回 `429 Too Many Requests`，由调用方退避重试

### 基础设施与运维

| 组件 | 管理方式 | 健康检查 | 故障恢复 |
|------|----------|----------|----------|
| Agent_QC (.NET) | Windows Service 或 systemd | `GET /api/v1/health` | 自动重启 |
| vLLM (Python) | 由 Agent_QC 在启动时 spawn 子进程 | `GET localhost:8100/health` | 父进程监控，退出自动拉起，最多重试 3 次 |
| ONNX Runtime | 进程内，CUDA EP 初始化 | 启动时 warm-up 推理 | 失败 → CPU EP 降级 |
| SQLite | 本地文件 | 定期 `PRAGMA integrity_check` | WAL 自动恢复 |

### 数据安全与合规（等保三级补充）

- **认证**：JWT Token + 与 HIS/RIS 的 SSO 集成
- **授权**：RBAC，至少区分：放射科医生 / 质控管理员 / 系统管理员
- **审计**：所有质控操作写入 `AuditLog` 表，只追加不修改不删除，保留 ≥ 6 个月
- **限流**：每 IP 每 endpoint 令牌桶限流，默认 20 req/s
- **备份**：SQLite 每日全量备份到 NAS，保留 30 天；知识库 YAML 纳入 Git 版本控制
- **敏感数据**：连接字符串、API Key 通过环境变量或 Secret Manager 注入，不入配置文件

---
## 三、质控管线架构（GPU 原生，四层）

```
Level 0: Pre-filter (CPU, ~10ms)
  └── 段落解析 + 否定检测 + 实体提取 + 快速规则匹配

Level 1: BERT Sentinels (GPU, ~8ms, 并行)
  ├── MedBERT 错别字序列标注
  ├── MedBERT NER 医学实体识别
  └── MedBERT NLI 所见-结论一致性初筛

Level 2: Hermes Skill Squad (GPU, ~120ms, 按需并行)
  ├── gender-anatomy-checker      [性别矛盾]
  ├── site-consistency-checker    [部位一致性]
  ├── findings-impression-nli     [所见-结论NLI]
  ├── critical-sign-arbiter       [危急值审查]
  ├── device-method-validator     [设备/方法校验]
  ├── measurement-completeness    [测量完整性]
  ├── rads-compliance-checker     [RADS合规]
  └── terminology-validator       [术语规范]

Level 3: Hermes QA + Scoring (CPU, ~20ms)
  └── 冲突仲裁 + 评分 + 持久化
```

---

## 四、强制要求

### 4.1 服务合并 + 单实例部署

QCService（端口 5200）和 ReportQC（端口 5100）合并为单一 **Agent_QC** 服务（端口 5100）。
- Tesseract OCR 识别模块 → 合入 Agent_QC 进程内
- Oracle v_qc_report 查询 → 合入 Agent_QC 的 Repository 层
- 合并后只有一个启动入口、一个日志文件、一个健康检查端点
- SQLite WAL 模式，单机部署。如需多实例负载均衡，需升级为 PostgreSQL

### 4.2 测试全覆盖

- 单元测试覆盖率 ≥ 80%
- 集成测试覆盖所有 API 接口
- 每个 QC Skill 至少 10 个测试用例
- GPU 推理失败 → 自动降级验证

### 4.3 版本化 API 与数据库变更

- 路由：`/api/v{version}/`
- 数据库变更：开发阶段 FreeSql CodeFirst 自动同步；生产阶段使用 FreeSql Migration 脚本（禁止 `SyncStructureAsync` 自动变更）
- Migration 脚本纳入版本控制，每次变更包含正向脚本 + 回滚脚本

### 4.4 数据安全（等保三级）

| 要求 | 实现 |
|------|------|
| 连接字符串 | 环境变量读取（`AGENTQC_CONNECTION_STRING`） |
| 输入验证 | ASP.NET Core `[ApiController]` 自动校验 + 自定义 `ValidationFilter` |
| 数据库操作 | FreeSql ORM 统一入口，禁止拼接 SQL |
| SQLite 并发 | WAL 模式启用 (`PRAGMA journal_mode=WAL;`)，写入串行化 |
| 事务 | `IFreeSql.Transaction` 包裹所有多表写操作 |
| 删除 | 只做软删除（`IsDeleted` 字段更新），禁止 DELETE/DROP/ALTER |
| 患者信息 | DTO 层自动脱敏（姓名→姓*名、住院号→脱敏ID），日志和审计中禁止记录患者信息原文 |

---

## 五、C# → GPU 调用方案

```
Agent_QC (.NET 8, 主进程)
├── Microsoft.ML.OnnxRuntime.Gpu (CUDA EP)
│   └── MedBERT-Chinese 三任务 batch 推理
│
└── HttpClient → vLLM Server (Python 独立进程, http://localhost:8100)
    └── Qwen2.5-7B GPTQ-INT4 LLM 推理
```

---

## 六、文件所有权映射

| 路径 | 所有者 | 说明 |
|------|--------|------|
| `src/Agent_QC/**` | backend | 后端全部代码 |
| `docs/hermes/**` | orchestrator | 规范文件 |
| `docs/architecture/**` | orchestrator | 架构设计文档 |
| `docs/quality/**` | orchestrator | 质控设计文档 |
| `docs/implementation/**` | orchestrator | 实施文档 |
| `docs/devops/**` | orchestrator | DevOps 文档 |
| `knowledge/**` | orchestrator | 知识库 YAML 文件 |
| `models/**` | orchestrator | ONNX/GGUF 模型文件 |
| `scripts/**` | orchestrator | 构建/部署脚本 |
