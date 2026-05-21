# 设计哲学与编码规范 (soul.md)

> Agent_QC 项目的灵魂文件。所有 Agent 必须遵守。
> 当规范与其他文件冲突时，**以此文件为准**。

---

## 一、核心原则

1. **患者安全第一** — 危急值相关规则：宁可误报，不可漏报（漏报危及患者安全）。常规质控规则：宁可少报，不可误报（误报降低可信度）。二者优先级不同，代码中必须区分对待。
2. **1 秒内响应** — 端到端延迟 < 1 秒，P95 < 300ms，P99 < 500ms。生产级硬性要求。
3. **简洁优先** — 一个类只做一件事。避免过度抽象。优先使用 .NET 内置能力，谨慎引入第三方库。
4. **可测试性** — 所有模块使用依赖注入 + 接口，确保可独立 mock 测试。难以测试 = 设计有问题。
5. **测试先于代码** — TDD 是强制工作流。写 Test → 看它 FAIL → 写最小实现 → 看它 PASS → 重构。没有失败过的测试不算测试，写了实现但没有先写测试 = 删掉重写。
6. **验证后完成** — 任何完成声明必须附带新鲜验证证据。`dotnet test` 输出、构建日志、`git diff` 结果——输出和断言缺一不可。"应该能通过"不代表通过。
7. **一致性** — 同类问题同类解法，不引入不必要的风格变化。
8. **数据安全** — 软删除、参数化查询、患者信息脱敏。所有数据访问记录审计日志（等保三级要求）。

---

## 二、架构原则

```
分层架构：Controller → Service → Repository
前后端分离（纯后端 API，前端由 RIS/HIS 集成）
API First：先定接口 DTO，再实现
```

### 质控管线架构（GPU 原生）

```
QcRequest → Pre-filter(CPU) → BERT Sentinels(GPU,并行) → Skill Squad(GPU,按需)
    → QA Arbiter(GPU) → Scoring(CPU) → Persistence(CPU) → QcResponse
```

---

## 三、开发工作流（Superpowers 规范）

### 3.1 前置准备

每次开始功能开发前，必须按以下顺序执行：

1. **工作空间隔离** — 在 worktree 中开发，不允许在 main/develop 分支直接修改
2. **基线确认** — 在 worktree 中先跑一次全量测试，确认基线通过
3. **规划先行** — 复杂任务先写实现计划到 `docs/superpowers/plans/`，获批准后执行

### 3.2 TDD 循环（不可绕过）

```
RED     → 写一个测试，描述期望行为
VERIFY  → 运行测试，确认它 FAIL（因功能缺失而非笔误）
GREEN   → 写最简代码让测试通过
VERIFY  → 运行测试，确认 PASS + 其他测试不退化
REFACTOR→ 清理代码，保持 GREEN
```

规则：
- 没有测试的提交不进入 develop
- 没有先看到 FAIL 的测试不算有效测试
- 测试名描述行为而非实现：`report_empty_impression_returns_warning` ✅ / `test1` ❌
- 一条测试只测一个行为（测试名中有"and"就拆分）

### 3.3 完成门禁

| 门禁 | 要求 |
|------|------|
| 代码审查 | 每个任务完成后请求 code review，Critical/Important 问题修复后才可继续 |
| 验证证据 | 所有测试通过的终端输出、覆盖率报告、lint 结果截图或日志 |
| 回归验证 | 重构时确保原有 200+ 回归测试集不退化 |
| 更新 ADR | 架构决策变更必须在 `memory.md` 记录 |

### 3.4 计划存放

- 实现计划路径：`docs/superpowers/plans/YYYY-MM-DD-<feature-name>.md`
- 计划格式：标题 → 目标 → 架构 → 分步任务（每步含完整代码 + 命令 + 期望输出）

---

## 三、命名规范

### 3.1 C# 后端

| 类别 | 规则 | 示例 |
|------|------|------|
| 命名空间 | `AgentQC.Module` | `AgentQC.Controllers` |
| 类 | PascalCase | `QcController` |
| 接口 | `I` 前缀 + PascalCase | `IQcService` |
| 方法参数 | camelCase | `examId`, `patientName` |
| 局部变量 | camelCase | `var result = ...` |
| 私有字段 | `_camelCase` | `_repository`, `_logger` |
| 常量 | PascalCase | `MaxRetryCount` |
| 异步方法 | `Async` 后缀 | `ExecuteQcAsync()` |
| API 路由 | `/api/v{version}/[controller]` | `/api/v1/qc/report` |

### 3.2 数据库实体

| 类别 | 规则 | 示例 |
|------|------|------|
| 表名 | 小写 + 下划线 | `qc_report`, `qc_issue` |
| 列名 | 小写 + 下划线 | `report_id`, `patient_gender` |
| 实体类 | PascalCase | `QcReport` |
| 属性 | PascalCase | `ReportId`, `PatientGender` |
| FreeSql 命名转换 | `PascalCaseToUnderscoreWithUpper` | 自动转换 |
| 主键 | `[Column(IsPrimary = true)]` + 雪花 ID 或 GUID | 不使用自增整数 |

### 3.3 模型文件

| 类别 | 规则 | 示例 |
|------|------|------|
| ONNX 模型 | 小写 + 下划线 | `medbert_chinese.onnx` |
| GGUF/量化模型 | 模型名_量化方式.gguf | `qwen2.5-7b_q4.gguf` |

---

## 四、代码风格

- 使用 `var` 当类型从右侧显式可知
- 表达式体方法（`=>`）简化单行方法
- 异步方法使用 `Async` 后缀
- 使用 `record` 定义 DTO
- 字符串拼接使用 `$` 插值
- 使用 `using` 声明（无大括号的简洁形式）
- 禁止 `region` 关键字（用 partial class 或独立文件替代）

---

## 五、错误处理

### 5.1 Service 层

```csharp
public interface IQcService
{
    Task<AjaxResult> ExecuteQcAsync(QcRequest request);
}

public class QcService : IQcService
{
    private readonly IFreeSql _fsql;
    private readonly IGpuInferenceService _gpu;
    private readonly ILogger<QcService> _logger;

    public QcService(IFreeSql fsql, IGpuInferenceService gpu, ILogger<QcService> logger)
    {
        _fsql = fsql;
        _gpu = gpu;
        _logger = logger;
    }

    public async Task<AjaxResult> ExecuteQcAsync(QcRequest request)
    {
        try
        {
            // 业务逻辑
            return AjaxResult.Success(data);
        }
        catch (OnnxRuntimeException ex)
        {
            _logger.LogWarning(ex, "GPU 推理失败，降级到 CPU EP");
            return await ExecuteCpuFallbackAsync(request);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is >= 500)
        {
            _logger.LogWarning(ex, "vLLM 不可用，跳过 Skill Squad");
            return await ExecuteWithoutLlmAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "质控处理未预期异常");
            return AjaxResult.Error("质控服务内部错误，已记录");
        }
    }
}
```

规则：
- Service 使用构造函数注入依赖（`IFreeSql`、`IGpuInferenceService`、`ILogger<T>`），不使用 `public static`
- 所有 public 方法使用 try-catch，异常分类处理
- GPU 推理异常分两类：ONNX Runtime 异常 → 降级 CPU EP；vLLM HTTP 异常 → 跳过 Skill Squad
- 降级时写 Warn 级别日志，并在响应中标记 `degraded: true` 告知调用方
- 未预期异常写 Error 日志，返回通用错误信息（不暴露内部细节给调用方）
- 绝不静默吞异常

### 5.2 Controller 层

- 只做参数校验和路由，不做业务逻辑
- 不 catch 异常（留给全局异常中间件 `GlobalExceptionMiddleware`）
- 返回 `Ok(result)` 让 ASP.NET Core 自动序列化
- 使用 `[ApiController]` 和 `ModelState.IsValid` 自动校验入参
- 通过 `IHttpContextAccessor` 注入当前用户身份，传递到 Service 层用于审计日志

---

## 六、日志规范

### 6.1 业务日志

- 日志级别：Error（异常，需人工介入）、Warn（非预期但可恢复，如降级）、Info（关键操作）、Debug（开发诊断）
- 每个 Service 方法异常必须写 Error 日志
- 关键业务操作写 Info 级别日志（质控请求开始/结束、规则命中、Skill 触发）
- GPU 模型加载状态写 Info 级别日志
- LLM 推理失败写 Warn + 自动降级标记
- 支持数据库日志持久化（`QC_Log` 表）

### 6.2 审计日志（等保三级强制要求）

单独写 `AuditLog` 表，与业务日志分离：

| 字段 | 说明 |
|------|------|
| `Timestamp` | 操作时间（精确到毫秒） |
| `OperatorId` | 操作人 ID |
| `OperatorName` | 操作人姓名 |
| `Action` | 操作类型：`QC_QUERY` / `QC_EXECUTE` / `REPORT_VIEW` / `CONFIG_CHANGE` |
| `TargetId` | 操作对象 ID（如报告编号） |
| `TargetType` | 操作对象类型（如 `QcReport`） |
| `Detail` | JSON 格式的操作摘要（不含患者隐私数据） |
| `ClientIp` | 客户端 IP |
| `Result` | 操作结果：`SUCCESS` / `FAILURE` / `DEGRADED` |

审计日志只追加不修改不删除，保留至少 6 个月。

---

## 七、API 响应格式

### 7.1 统一响应结构

```json
{
    "code": 200,
    "data": {},
    "msg": "success",
    "degraded": false
}
```

`degraded: true` 表示部分功能降级（如 GPU 不可用，仅用规则引擎），调用方应提示用户结果置信度降低。

### 7.2 错误码规范

| code | 含义 | 使用场景 |
|:----:|------|----------|
| 200 | 成功 | 正常返回（含降级模式） |
| 400 | 请求参数错误 | 缺少必填字段、字段格式不正确 |
| 401 | 未认证 | Token 过期或无效 |
| 403 | 无权限 | 无此报告/功能的访问权限 |
| 404 | 资源不存在 | 报告编号不存在 |
| 422 | 请求格式正确但语义错误 | 非 DICOM 文本、报告内容为空 |
| 429 | 请求频率超限 | 超过 API 限流阈值 |
| 500 | 服务内部错误 | 未预期异常（不含降级，降级返回 200 + degraded=true） |
| 503 | 服务不可用 | GPU 和规则引擎均不可用 |

使用 `AjaxResult` 类统一封装。Controller 中使用 `Ok(result)` 或 `StatusCode(code, result)`，利用 ASP.NET Core System.Text.Json 自动序列化。

---

## 八、文档规范

| 规则 | 说明 |
|------|------|
| hermes harness | 启动必读的 5 个文件（soul / project-context / agents / memory / hooks） |
| 架构文档 | `architecture/` 目录，每个专题一个独立 MD |
| 实施文档 | `implementation/` 目录 |
| 参考附录 | `appendix/` 目录，放基线数据和长期参考 |
| 文件所有权 | 跨角色修改 → 升级 Orchestrator 裁决 |
