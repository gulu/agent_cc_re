# QCService + ReportQC 合并方案

> 将当前独立的 QCService（端口 5200）和 ReportQC（端口 5100）合并为单一 **Agent_QC** 服务。
> 简化部署架构、统一日志管理、消除不必要的进程间通信开销。

---

## 1. 当前架构 vs 合并后架构

### 当前（两个独立服务）

```
QCClient ──OCR请求──▶ QCService:5200 ──Tesseract──▶ OCR结果
    │                      │
    │                 Oracle查询
    │                      │
    └──质控请求──▶ ReportQC:5100 ──4层管线──▶ QC结果
                       │
                   SQLite
```

### 合并后（单一服务）

```
QCClient ──质控请求──▶ Agent_QC:5100
                          │
                    ┌─────┼─────┐
                    │     │     │
                 OCR模块  Oracle  4层管线
              (Tesseract) 查询    │
                    │     │     │
                    └─────┼─────┘
                          │
                      SQLite
```

---

## 2. 合并内容

### 2.1 OCR 模块合入

| 原 QCService 文件 | 合并到 Agent_QC |
|-------------------|----------------|
| `Services/OcrService.cs` | `Services/OcrService.cs`（保持，使用独立 try-catch 隔离） |
| `Controllers/OcrController.cs` | `Controllers/OcrController.cs`（路由保持） |
| `Models/OcrModels.cs` | `Models/OcrModels.cs` |
| Tesseract 依赖 (NuGet) | 合并到 Agent_QC.csproj |
| chi_sim 语言包 (tessdata/) | 复制到 `tessdata/` |

**Tesseract 隔离策略：**
- OcrService 使用独立的 try-catch 块，Tesseract 异常不传播到质控管线
- OCR 初始化失败 → log Error，但 Agent_QC 服务正常启动，OCR 端点返回 503
- OCR 处理超时（> 30s）→ 取消并返回错误，不阻塞其他请求
- 考虑 Tesseract 内存泄漏风险，每处理 1000 次 OCR 后自动重启 OCR 进程（若未来改为独立进程）

### 2.2 Oracle 查询模块合入

| 原 QCService 文件 | 合并到 Agent_QC |
|-------------------|----------------|
| `Services/ReportQueryService.cs` | `Repository/ReportQueryRepository.cs` |
| `Controllers/ReportQueryController.cs` | 合并到 `Controllers/QcController.cs` 或独立 |
| `Models/ReportModels.cs` | `Models/ReportModels.cs` |

### 2.3 API 路由保持兼容

| 原路由 | 合并后路由 | 说明 |
|--------|-----------|------|
| `POST :5200/api/v1/ocr/recognize` | `POST :5100/api/v1/ocr/recognize` | 端口统一为 5100 |
| `GET :5200/api/v1/qc/query-report` | `GET :5100/api/v1/qc/query-report` | 同上 |
| `POST :5100/api/v1/qc/report` | `POST :5100/api/v1/qc/report` | 不变 |

> QCClient 的 `appsettings.json` 只需将 `QcServiceUrl` 改为指向 5100。

---

## 3. 合并后的项目结构

```
src/Agent_QC/
├── Controllers/
│   ├── QcController.cs          # 质控入口（合并自 ReportQC）
│   ├── OcrController.cs         # OCR 识别（合并自 QCService）
│   ├── ReportsController.cs     # 历史查询
│   ├── KnowledgeBaseController.cs
│   ├── TerminologyController.cs
│   ├── RadsStandardsController.cs
│   ├── QcFeedbackController.cs
│   └── LogsController.cs
│
├── Services/
│   ├── QcService.cs             # 质控主流程
│   ├── Level1QcService.cs       # 文本层
│   ├── Level2QcService.cs       # 语义层
│   ├── OcrService.cs            # Tesseract OCR（合并自 QCService）
│   ├── GpuInferenceService.cs   # ONNX Runtime GPU（新增）
│   ├── SkillSquadService.cs     # vLLM Skill Squad 调用（新增）
│   ├── TinyBertService.cs       # 保留作为降级方案
│   └── QcKnowledgeCache.cs      # 知识库缓存
│
├── Repository/
│   ├── QcReportRepository.cs
│   ├── ReportQueryRepository.cs # Oracle 查询（合并自 QCService）
│   └── KnowledgeBaseRepository.cs
│
├── Hermes/
│   ├── Orchestrator.cs          # Skill 调度器（新增）
│   ├── NegationDetector.cs      # 否定检测（新增）
│   └── SectionParser.cs         # 段落解析（新增）
│
├── Models/
│   ├── QcModels.cs
│   ├── OcrModels.cs
│   ├── ReportModels.cs
│   └── GpuModels.cs             # GPU 推理 DTO（新增）
│
├── Entities/                    # FreeSql 实体
│   ├── QcReport.cs
│   ├── QcIssue.cs
│   ├── ScoreDimension.cs
│   ├── ScoreResult.cs
│   ├── KnowledgeBase.cs
│   └── ...
│
└── Data/
    └── qc.db                    # SQLite
```

---

## 4. 启动流程

```
Program.cs
    │
    ├── 1. 加载配置 (appsettings.json + 环境变量覆盖)
    ├── 2. 注册 FreeSql (SQLite WAL 模式)
    ├── 3. 运行 DbMigration (开发: CodeFirst, 生产: Migration 脚本)
    ├── 4. 初始化 QcKnowledgeCache (加载 knowledge/*.yaml)
    ├── 5. 初始化 Tesseract (chi_sim，异步，不影响启动)
    │      失败 → Warn + OCR 不可用，但不阻塞
    ├── 6. 加载 ONNX Runtime GPU (MedBERT, warm-up 推理验证)
    │      失败 → Warn + /health 返回 degraded
    ├── 7. 启动 vLLM 子进程 → 轮询 health 最多 30s
    │      失败 → Error + 跳过 Skill Squad + /health 返回 degraded
    │      成功 → Info + 注册 vLLM 心跳监控
    ├── 8. 运行 DbSeed (增量，仅开发环境)
    └── 9. 启动 Kestrel on :5100
        /api/v1/health 反映启动时的组件状态
```

**启动原则：**
- 非关键组件失败不阻止服务启动（Tesseract、vLLM）
- ONNX Runtime 失败 → degraded 但服务可用（CPU EP 降级）
- 数据库失败 → 阻止启动（记录 Critical 日志）

---

## 5. 健康检查端点

```
GET /api/v1/health
{
  "status": "healthy" | "degraded" | "unhealthy",
  "version": "1.0.0",
  "uptime": "2d 5h 13m",
  "checks": {
    "database": {"status": "ok", "latency_ms": 2},
    "bert_gpu": {"status": "ok" | "degraded_cpu", "latency_ms": 12},
    "vllm": {"status": "ok" | "unavailable" | "restarting", "latency_ms": 5},
    "tesseract": {"status": "ok" | "unavailable"},
    "oracle": {"status": "ok" | "disconnected", "latency_ms": 15}
  },
  "metrics": {
    "requests_total": 15234,
    "requests_per_minute": 12.5,
    "p95_latency_ms": 210,
    "degraded_rate": 0.02
  }
}
```

- `healthy`：所有组件正常
- `degraded`：GPU 或 vLLM 不可用，仅运行 CPU 管线（质控能力降级但服务可用）
- `unhealthy`：数据库不可用，服务无法正常处理请求

---

## 6. 收益

| 维度 | 合并前 | 合并后 |
|------|--------|--------|
| 进程数 | 2 | 1 |
| 端口占用 | 2 (5100 + 5200) | 1 (5100) |
| 启动脚本 | 2 个 | 1 个 |
| 日志文件 | 2 套 | 1 套 |
| OCR → 质控延迟 | 跨进程 HTTP (~20ms) | 进程内调用 (~1ms) |
| 监控端点 | 2 套 | 1 套统一 |
| 部署复杂度 | 中 | 低 |
