# 旧代码分析与设计文档交叉验证报告

> 基于 `old_src/` 全部源码分析，验证 `docs/` 设计决策的合理性。

---

## 一、旧系统全景

```
┌─────────────────────────────────────────────────────────────────┐
│                     QCClient (WinForms .NET 4.8)                 │
│  截图 → OCR识别 → 查询报告 → 调用质控 → SSE推送到WebView2界面        │
└──────────────────────┬──────────────────────────┬────────────────┘
                       │ HTTP :5100               │ HTTP :5200
              ┌────────▼────────┐         ┌───────▼──────────┐
              │   ReportQC      │         │    QCService      │
              │  /api/v1/qc/    │         │  /api/v1/ocr/     │
              │  .NET 8 + FS    │         │  .NET 8 + FS      │
              │  TinyBERT CPU   │         │  Tesseract OCR    │
              │  SQLite/PG/Ora  │         │  Oracle DB查询    │
              └─────────────────┘         └───────────────────┘
```

| 服务 | 端口 | 代码量 | 角色 | 状态 |
|------|------|--------|------|------|
| ReportQC | 5100 | ~3000 行 (3个Service + Controllers + Entities) | 质控分析引擎 | 推倒重建 |
| QCService | 5200 | ~600 行 (OCR + Oracle查询) | OCR识别 + 报告查询 | 重构合并 |
| QCClient | — | ~2000 行 (WinForms) | 桌面客户端（截图+展示） | 保留（API适配） |

---

## 二、核心缺陷验证

### 缺陷 1: 40% 误报率的根因已确认

```csharp
// QcService.cs 第215行起 — Level 3/4 全部规则:
content.Contains(keyword)  // ← 9个规则组都这样写
```

**严重程度:** P0 — 系统核心问题
**影响:** gender_conflict, age_conflict, direction_conflict, device_conflict, scan_enhance_conflict, critical_sign, rads_missing 全部基于 `Contains()`

**典型误报路径:**
```
患者性别=男, 报告=“子宫未见显示（男性，正常骨盆）”
→ Contains("子宫") = true
→ 判为 critical 误报
```

**文档对应:** `docs/quality/context-blindness.md` — 五种语境盲区全部在旧代码中得到验证。

### 缺陷 2: 零测试覆盖

```
旧代码中无任何测试项目，无 tests/ 目录，无 xUnit 引用
覆盖率: ≈ 0%
```

**严重程度:** P0 — 任何重构必须从 TDD 开始
**文档对应:** `docs/devops/testing-strategy.md` — TDD 强制规范完全正确。

### 缺陷 3: 静态类架构 — 不可测试

```csharp
public static class QcService {
    public static AjaxResult ExecuteQc(IFreeSql fsql, QcRequest request) { ... }
}
public static class Level1QcService {
    public static List<QcIssueItem> Run(string content, QcRequestInfo? patientInfo) { ... }
}
public static class Level2QcService { ... }
```

所有 Service 都是 `public static`，无法 mock，无法单元测试，无法 DI 替换。

### 缺陷 4: 规则硬编码

| 文件 | 硬编码内容 | 行数 |
|------|-----------|:----:|
| `DbSeed.cs` | 种子数据（术语、排除词、部位映射等） | 476 |
| `Level1QcService.cs` | 50+ 错别字映射 Dictionary | 50+ |
| `Level1QcService.cs` | 30+ 重复字符硬编码 | 30+ |
| `Level2QcService.cs` | 50+ 口语化术语映射 | 50+ |
| `Level2QcService.cs` | 80+ 解剖术语映射 | 80+ |
| `QcService.cs` | 7 维度计算权重、通过阈值 | 全部 |

**核心问题:** 数据在 DB（knowledge_base 表），规则在 C# 代码。修改规则需要重新编译部署。

### 缺陷 5: 生产环境 CodeFirst 自动同步

```csharp
.UseAutoSyncStructure(true)  // Program.cs 第53行
```

生成环境自动改表结构 → 灾难隐患。

### 缺陷 6: 无认证、无审计、无异常中间件

- CORS `AllowAll`，无 JWT/API Key
- 无操作日志（谁在何时改了规则？）
- 无全局异常处理（异常直接暴露给客户端）

### 缺陷 7: TinyBERT CPU 推理

```xml
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.25.1" />
<!-- 没有 Microsoft.ML.OnnxRuntime.Gpu -->
```

无 GPU 加速。TinyBERT 4L312D 推理时 CPU-bound。

---

## 三、可复用资产

| 资产 | 路径 | 价值 | 复用方式 |
|------|------|------|----------|
| Entity 模型 | `Entities/QcReport.cs` 等 | 字段设计较完整（41 字段） | 迁移到新项目 |
| 知识库种子数据 | `Services/DbSeed.cs` | 300+ 术语、5 种 RADS、50+ 部位映射 | 导出为 YAML |
| 评分维度定义 | `DbSeed.cs:SeedScoreDimensions` | 4 维度 + 权重 | 保留但可配置化 |
| Tesseract OCR 管线 | `QCService/OcrService.cs` | 预处理 + 识别 + 后处理 | 合并到新服务 |
| Oracle 报告查询 | `QCService/ReportQueryService.cs` | FreeSql Oracle 查询 | 合并到新服务 |
| QCClient 模型 | `QCClient/QcEngine.cs` | DTO 定义 | 保持兼容 |
| RADS 标准数据 | `DbSeed.cs:SeedRadsStandards` | 5 种分类全部标准 | 导出为 YAML |

---

## 四、设计决策交叉验证矩阵

| 设计决策（来自 docs） | 旧代码现状 | 验证结论 |
|---------------------|-----------|----------|
| GPU 原生推理管线 | CPU-only ONNX Runtime | **验证通过** — GPU 是大幅提升性能的前提 |
| MedBERT-110M 替换 TinyBERT | TinyBERT 4L312D，语义能力弱 | **验证通过** — 需要更大模型提升 NER/NLI 质量 |
| Hermes Skill Squad (LLM 终审) | `content.Contains()`, 40% 误报 | **验证通过** — LLM 是解决误报的根本手段 |
| YAML 外部化知识库 | 数据在 DB，规则在 C# | **部分验证** — 旧系统已有 DB 数据，需补充规则外部化 |
| DI + 接口模式 | 全部 `public static` | **验证通过** — 零测试覆盖的根因就是静态类 |
| TDD 强制 | 无任何测试 | **验证通过** — 从零开始的正确方式 |
| Four Level 质控管线 | 已有 Level 1-4 结构 | **可借鉴** — 结构合理但实现方式错误 |
| 规则引擎（否定检测） | 无否定检测 | **验证通过** — 旧代码完全未处理否定语境 |
| 并发模型（vLLM batch） | 串行请求 | **验证通过** — 需要并发支持 |
| 服务合并（QCService + ReportQC） | 两个独立服务 | **验证通过** — 增加部署复杂度，合并简化架构 |
| 健康检查 + 降级 | QCService 有 /health，ReportQC 无 | **验证通过** — 需要完整健康检查体系 |
| Auth/RBAC | 无任何认证 | **验证通过** — 生产安全的基本要求 |
| 审计日志 | 无审计 | **验证通过** — 医疗合规的必要条件 |
| 评分体系 | 硬编码 7 维度（实际 4 维度） | **需调整** — docs 中需确认评分维度数量 |

---

## 五、结论

**总体评价:** docs 中的设计决策全部有效，不需要推翻任何核心方案。但需要补充以下几点：

1. **否定检测应提升到 P0** — 旧代码完全没有否定处理，这是 40% 误报的最大来源
2. **评分维度需统一** — DbSeed 定义了 4 维度（normative/completeness/logic/timeliness），但 QcService.CalculateScore 用了 7 维度（冲突间不一致）
3. **知识库迁移策略** — 旧 knowledge_base 表有现成数据。Phase 1 应包含 DB→YAML 导出脚本
4. **QCClient API 兼容** — 新 API 必须保持与 QCClient QcRequest/QcResponse 模型的兼容性
5. **Phase 1 起点** — 不是白纸，而是带着 300+ 术语数据和 5 种 RADS 标准的有基础的重构
