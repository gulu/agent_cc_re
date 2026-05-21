# Phase 1: GPU 管线搭建 + 快速止血 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 搭建 `src/` 新项目骨架，合并 QCService + ReportQC 为单一 Agent_QC 服务，导入旧知识库数据，实现否检测 + 段落解析预处理层，通过 200 份基准报告建立延迟基线（P95 < 300ms）。

**Architecture:** 在 `src/Agent_QC/` 下新建 .NET 8 ASP.NET Core 项目，使用 DI + Interface 模式重构所有 Service。旧系统 Entity 模型直接迁移，知识库种子数据导出为 YAML，NLP 预处理层（NegationDetector + SectionParser）作为 Level 0 前置。

**前置条件:**
- RTX 4090 硬件就位，CUDA 12.4+ 驱动安装完成（旧代码无需此环境也可完成前 6 个子任务）
- 旧代码中 `old_src/backend/Data/qc.db` 存在且有完整种子数据
- 从 QcReport 表脱敏导出 ≥ 200 份历史报告作为基线测试集

**可复用资产（来自旧代码）：**
- Entity 模型：QcReport, QcIssue, ScoreResult, KnowledgeBase, TerminologyStandard, RadsStandard
- 知识库种子数据：DbSeed.cs 中的 300+ 术语 + 5 种 RADS + 50+ 部位映射
- Tesseract OCR 管线（QCService/OcrService.cs）
- Oracle 报告查询（QCService/ReportQueryService.cs）
- QCClient API 模型（需保持兼容）

---

### Task 1: 创建新项目骨架 + Entity 迁移

**Files:**
- Create: `src/Agent_QC/src/Agent_QC.csproj`
- Create: `src/Agent_QC/src/Program.cs`
- Create: `src/Agent_QC/src/Entities/QcReport.cs`
- Create: `src/Agent_QC/src/Entities/QcIssue.cs`
- Create: `src/Agent_QC/src/Entities/ScoreResult.cs`
- Create: `src/Agent_QC/src/Entities/ScoreDimension.cs`
- Create: `src/Agent_QC/src/Entities/KnowledgeBase.cs`
- Create: `src/Agent_QC/src/Entities/TerminologyStandard.cs`
- Create: `src/Agent_QC/src/Entities/RadsStandard.cs`
- Create: `src/Agent_QC/src/Entities/QcConfig.cs`
- Create: `src/Agent_QC/tests/Agent_QC.Tests.csproj`
- Create: `src/Agent_QC/Agent_QC.sln`

- [ ] **Step 1: 创建项目目录结构**

```bash
mkdir -p src/Agent_QC/src/Entities
mkdir -p src/Agent_QC/src/Services
mkdir -p src/Agent_QC/src/Controllers
mkdir -p src/Agent_QC/src/Configuration
mkdir -p src/Agent_QC/tests
mkdir -p src/Agent_QC/tests/UnitTests
```

- [ ] **Step 2: 创建解决方案和项目文件**

`src/Agent_QC/Agent_QC.sln`:
```
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Agent_QC", "src/Agent_QC.csproj", "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Agent_QC.Tests", "tests/Agent_QC.Tests.csproj", "{B2C3D4E5-F6A7-8901-BCDE-F12345678901}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Release|Any CPU.Build.0 = Release|Any CPU
		{B2C3D4E5-F6A7-8901-BCDE-F12345678901}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{B2C3D4E5-F6A7-8901-BCDE-F12345678901}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{B2C3D4E5-F6A7-8901-BCDE-F12345678901}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{B2C3D4E5-F6A7-8901-BCDE-F12345678901}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
```

`src/Agent_QC/src/Agent_QC.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Agent_QC</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FreeSql" Version="3.5.309" />
    <PackageReference Include="FreeSql.Provider.Sqlite" Version="3.5.309" />
    <PackageReference Include="FreeSql.Provider.PostgreSQL" Version="3.5.309" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
  </ItemGroup>

</Project>
```

`src/Agent_QC/tests/Agent_QC.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Moq" Version="4.20.72" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\src\Agent_QC.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: 迁移 Entity 模型（从 old_src/backend/Entities/）**

将旧代码的 Entity 模型按以下原则迁移到 `src/Agent_QC/src/Entities/`:

- QcReport.cs — 保留全部 41 字段，去掉 `JSBaseDBEntity` 基类继承
- QcIssue.cs — 保留字段，去掉 `JSBaseDBEntity`
- ScoreResult.cs — 保留字段，去掉 `JSBaseDBEntity`
- ScoreDimension.cs — 保留字段，去掉 `JSBaseDBEntity`
- KnowledgeBase.cs — 保留字段
- TerminologyStandard.cs — 保留字段
- RadsStandard.cs — 保留字段
- QcConfig.cs — 从旧代码筛选保留的配置字段

- [ ] **Step 4: 创建 Program.cs（骨架，不含业务逻辑）**

```csharp
using FreeSql;
using FreeSql.Internal;

var builder = WebApplication.CreateBuilder(args);

// ── 数据库 ──
var freeSql = new FreeSqlBuilder()
    .UseConnectionString(DataType.Sqlite, "Data Source=Data/qc.db")
    .UseNameConvert(NameConvertType.PascalCaseToUnderscore)
    .Build();

builder.Services.AddSingleton(freeSql);

// ── 控制器 ──
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
```

**关键变更（对比旧代码）：**
- 移除 `UseAutoSyncStructure(true)` — 改为手动 Migration
- 移除 `JSBaseLogs.Initialize` — 改用 Serilog（后续 Task 添加）
- 移除 `DbSeed.Initialize` — 改为 YAML 导入（Task 5）
- 移除 `TinyBertService.TryLoad` — GPU 推理在后续 Phase 添加
- 移除自动打开浏览器逻辑

- [ ] **Step 5: 验证构建**

Run: `dotnet build src/Agent_QC/Agent_QC.sln`
Expected: Build succeeded, 0 warnings

- [ ] **Step 6: 验证测试项目可运行**

Run: `dotnet test src/Agent_QC/Agent_QC.sln`
Expected: 0 tests, 0 failed (骨架阶段，无测试正常)

- [ ] **Step 7: 提交**

```bash
git add src/Agent_QC/
git commit -m "feat(project): 创建 Agent_QC 项目骨架，迁移 Entity 模型"
```

---

### Task 2: QcController + 统一响应模型 + 健康检查

**Files:**
- Create: `src/Agent_QC/src/Models/AjaxResult.cs`
- Create: `src/Agent_QC/src/Controllers/QcController.cs`
- Create: `src/Agent_QC/src/Controllers/HealthController.cs`
- Test: `src/Agent_QC/tests/UnitTests/Controllers/QcControllerTests.cs`

- [ ] **Step 1: 写 AjaxResult 测试（TDD RED）**

`src/Agent_QC/tests/UnitTests/Models/AjaxResultTests.cs`:
```csharp
using Xunit;
using Agent_QC.Models;

namespace Agent_QC.Tests.UnitTests.Models;

public class AjaxResultTests
{
    [Fact]
    public void Success_ReturnsCode200()
    {
        var result = AjaxResult.Success(new { msg = "ok" });
        Assert.Equal(200, result.Code);
    }

    [Fact]
    public void Error_ReturnsCode500()
    {
        var result = AjaxResult.Error("出错了");
        Assert.Equal(500, result.Code);
        Assert.Equal("出错了", result.Msg);
    }

    [Fact]
    public void Success_ContainsData()
    {
        var data = new { foo = "bar" };
        var result = AjaxResult.Success(data);
        Assert.Equal(data, result.Data);
    }
}
```

Run: `dotnet test src/Agent_QC/tests/Agent_QC.Tests.csproj`
Expected: FAIL — AjaxResult 不存在

- [ ] **Step 2: 实现 AjaxResult（GREEN）**

`src/Agent_QC/src/Models/AjaxResult.cs`:
```csharp
namespace Agent_QC.Models;

public class AjaxResult
{
    public int Code { get; set; }
    public string Msg { get; set; } = "success";
    public object? Data { get; set; }

    public static AjaxResult Success(object? data = null, string msg = "success")
        => new() { Code = 200, Msg = msg, Data = data };

    public static AjaxResult Error(string msg)
        => new() { Code = 500, Msg = msg };

    public static AjaxResult Error(int code, string msg)
        => new() { Code = code, Msg = msg };
}
```

Run: `dotnet test src/Agent_QC/tests/Agent_QC.Tests.csproj --filter AjaxResultTests`
Expected: PASS (3/3)

- [ ] **Step 3: 写 QcController 测试（TDD RED）**

`src/Agent_QC/tests/UnitTests/Controllers/QcControllerTests.cs`:
```csharp
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Agent_QC.Controllers;
using Agent_QC.Models;
using Moq;

public class QcControllerTests
{
    [Fact]
    public async Task PostQcReport_MissingReportId_ReturnsError()
    {
        var controller = new QcController(Mock.Of<IQcService>());
        var request = new QcRequest { ReportId = "", Findings = "test", Impression = "test" };

        var result = await controller.PostQcReport(request) as ObjectResult;
        var ajax = result?.Value as AjaxResult;

        Assert.Equal(400, ajax?.Code);
    }

    [Fact]
    public async Task PostQcReport_MissingContent_ReturnsError()
    {
        var controller = new QcController(Mock.Of<IQcService>());
        var request = new QcRequest { ReportId = "R001", Findings = "", Impression = "" };

        var result = await controller.PostQcReport(request) as ObjectResult;
        var ajax = result?.Value as AjaxResult;

        Assert.Equal(400, ajax?.Code);
    }
}
```

Run: `dotnet test src/Agent_QC/tests/Agent_QC.Tests.csproj --filter QcControllerTests`
Expected: FAIL — QcController/IQcService 不存在

- [ ] **Step 4: 实现 IQcService 接口 + QcController**

`src/Agent_QC/src/Services/IQcService.cs`:
```csharp
using Agent_QC.Models;

namespace Agent_QC.Services;

public interface IQcService
{
    Task<AjaxResult> ExecuteQcAsync(QcRequest request);
}
```

`src/Agent_QC/src/Controllers/QcController.cs`:
```csharp
using Microsoft.AspNetCore.Mvc;
using Agent_QC.Models;
using Agent_QC.Services;

namespace Agent_QC.Controllers;

[ApiController]
[Route("api/v1/qc")]
public class QcController : ControllerBase
{
    private readonly IQcService _qcService;

    public QcController(IQcService qcService)
    {
        _qcService = qcService;
    }

    [HttpPost("report")]
    public async Task<IActionResult> PostQcReport([FromBody] QcRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReportId))
            return Ok(AjaxResult.Error(400, "ReportId 不能为空"));

        if (string.IsNullOrWhiteSpace(request.Findings) && string.IsNullOrWhiteSpace(request.Impression))
            return Ok(AjaxResult.Error(400, "报告内容不能为空"));

        var result = await _qcService.ExecuteQcAsync(request);
        return Ok(result);
    }
}
```

- [ ] **Step 5: 写健康检查控制器**

`src/Agent_QC/src/Controllers/HealthController.cs`:
```csharp
using Microsoft.AspNetCore.Mvc;
using Agent_QC.Models;

namespace Agent_QC.Controllers;

[ApiController]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow
        });
    }
}
```

- [ ] **Step 6: 验证测试通过 + 完整构建**

Run: `dotnet test src/Agent_QC/tests/Agent_QC.Tests.csproj`
Expected: 5 passed, 0 failed

Run: `dotnet build src/Agent_QC/src/Agent_QC.csproj`
Expected: Build succeeded

- [ ] **Step 7: 提交**

```bash
git add src/Agent_QC/src/Models/ src/Agent_QC/src/Controllers/ src/Agent_QC/tests/
git commit -m "feat(qc-pipeline): 添加 QcController + 健康检查 + 统一响应模型"
```

---

### Task 3: 知识库 YAML 外部化（从旧 DbSeed 导出）

**Files:**
- Create: `knowledge/knowledge-base.yaml`
- Create: `knowledge/terminology.yaml`
- Create: `knowledge/rads-standards.yaml`
- Create: `scripts/export-dbseed-to-yaml.py`
- Create: `src/Agent_QC/src/Infrastructure/KnowledgeBaseLoader.cs`
- Test: `src/Agent_QC/tests/UnitTests/Infrastructure/KnowledgeBaseLoaderTests.cs`

- [ ] **Step 1: 编写 YAML 导出脚本**

`scripts/export-dbseed-to-yaml.py`:
```python
"""
从 DbSeed.cs 提取种子数据并导出为 YAML。
在旧代码上运行一次即可。
"""
import re, yaml, json

def parse_dbseed(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    categories = {}

    # 提取 Add("category", ...) 调用
    pattern = r'Add\("(\w+)",\s*"([^"]*)",\s*J\(new\[\]\{(.*?)\}\),?\s*(?:"([^"]*)")?\s*,?\s*"(warning|error|critical|info)"?\s*,?\s*(\d+)?\s*\)'
    matches = re.findall(pattern, content, re.DOTALL)

    for cat, key, vals_str, desc, sev, order in matches:
        vals = re.findall(r'"([^"]*)"', vals_str)
        if cat not in categories:
            categories[cat] = []
        categories[cat].append({
            'key': key,
            'values': vals,
            'severity': sev,
            'sort_order': int(order) if order else 0,
        })

    return categories

if __name__ == '__main__':
    import sys
    path = sys.argv[1] if len(sys.argv) > 1 else 'old_src/backend/Services/DbSeed.cs'
    cats = parse_dbseed(path)

    with open('knowledge/knowledge-base.yaml', 'w') as f:
        for cat, items in cats.items():
            f.write(f'# {cat}\n')
            for item in items:
                f.write(f'- category: {cat}\n')
                f.write(f'  key: "{item["key"]}"\n')
                f.write(f'  values: {json.dumps(item["values"], ensure_ascii=False)}\n')
                f.write(f'  severity: {item["severity"]}\n')
                f.write(f'\n')
```

- [ ] **Step 2: 写 KnowledgeBaseLoader 测试**

```csharp
public class KnowledgeBaseLoaderTests
{
    [Fact]
    public void LoadKnowledgeBase_ValidYaml_ReturnsCategories()
    {
        var yaml = File.ReadAllText("../../../knowledge/knowledge-base.yaml");
        var loader = new KnowledgeBaseLoader();

        var result = loader.Load(yaml);

        Assert.NotEmpty(result);
        Assert.Contains(result, r => r.CategoryCode == "gender_exclude_female");
    }
}
```

RED → 实现 `KnowledgeBaseLoader` → GREEN

- [ ] **Step 3: 提交**

```bash
git add knowledge/ scripts/ src/Agent_QC/src/Infrastructure/
git commit -m "feat(rules): 知识库 YAML 外部化 + 导入脚本"
```

---

### Task 4: NLP 预处理层 — NegationDetector

**Files:**
- Create: `src/Agent_QC/src/Services/NegationDetector.cs`
- Test: `src/Agent_QC/tests/UnitTests/Services/NegationDetectorTests.cs`

完全按照 `docs/quality/context-blindness.md` 和 `docs/quality/quick-fixes.md` 中的设计实现。

否定词 + 作用域窗口 + 白名单排除：

| 否定词 | 作用域（向前） |
|--------|:----------:|
| 未见 | 4 tokens |
| 无明显 | 3 tokens |
| 未显示 | 3 tokens |
| 未及 | 3 tokens |
| 排除 | 3 tokens |
| 除外 | 2 tokens |

排除白名单：`{ "建议", "复查", "随访", "进一步", "可能", "需", "必要时" }`

作用域边界 token：`{ "，", "。", "；", "、", "但", "；", "\n" }`

每条测试先 FAIL 再实现，包含：
- 正常否定：`"未见明确结节影"` → 结节被否定
- 跨作用域否定：`"未见明确结节影，但建议随访"` → 随访不被否定
- 白名单跳过：`"未见明确占位，需进一步检查"` → 检查不被否定
- 边界 token 截断：否定词与目标词之间有边界 token → 不被否定

- [ ] **Step 1: 写测试 — 基础否定检测**
- [ ] **Step 2: 实现 NegationDetector**
- [ ] **Step 3: 写测试 — 排除白名单**
- [ ] **Step 4: 写测试 — 作用域边界**
- [ ] **Step 5: 写测试 — 边界 case（子宫/男性等）**
- [ ] **Step 6: 全部测试通过 → 提交**

---

### Task 5: NLP 预处理层 — SectionParser

**Files:**
- Create: `src/Agent_QC/src/Services/SectionParser.cs`
- Test: `src/Agent_QC/tests/UnitTests/Services/SectionParserTests.cs`

从报告全文解析段落边界：ClinicalHistory, Findings, Impression, Recommendation。

- [ ] **Step 1: 写段落解析测试（10 条用例）**
- [ ] **Step 2: 实现 SectionParser**
- [ ] **Step 3: 测试通过 → 提交**

---

### Task 6: 快速止血 6 项

**Files:**
- Modify: `src/Agent_QC/src/Services/QcService.cs`（如有 Level 3/4 规则）
- Create: `src/Agent_QC/src/Services/Rules/GenderConflictRule.cs`
- Create: `src/Agent_QC/src/Services/Rules/AgeConflictRule.cs`
- Test: `src/Agent_QC/tests/UnitTests/Rules/*.cs`

按照 `docs/quality/quick-fixes.md` 的 6 项快速止血措施：

1. NegationDetector 集成到规则引擎前置
2. ExportExcludePatterns 白名单（"未见显示"、"切除术后"等）
3. 引用语境检测（"患者自述"、"病史"中的性别不跳过）
4. Contains() → 精确匹配（部位-解剖术语映射校验）
5. 年龄范围校验逻辑加固（0-12 岁脑梗灶 → 需要确认而非直接报错）
6. 方位词校验证方向/左右匹配

每条规则的要求：3 个测试用例（正常/异常/边界），TDD 循环。

- [ ] **Step 1: GenderConflictRule TDD（3 条测试 + 实现）**
- [ ] **Step 2: AgeConflictRule TDD（3 条测试 + 实现）**
- [ ] **Step 3: DirectionConflictRule TDD（3 条测试 + 实现）**
- [ ] **Step 4: DeviceConflictRule TDD（3 条测试 + 实现）**
- [ ] **Step 5: ScanEnhanceConflictRule TDD（3 条测试 + 实现）**
- [ ] **Step 6: CriticalSignRule TDD（3 条测试 + 实现）**
- [ ] **Step 7: 全部规则通过 + 提交**

---

### Task 7: QcService 核心管线集成

**Files:**
- Create: `src/Agent_QC/src/Services/QcService.cs`
- Create: `src/Agent_QC/src/Models/QcRequest.cs`
- Create: `src/Agent_QC/src/Models/QcResponse.cs`
- Test: `src/Agent_QC/tests/UnitTests/Services/QcServiceTests.cs`

实现 4 层管线骨架（暂时只有 Level 1 + Level 0 预处理，Level 3/4 快速止血规则）：

```
Level 0 (预处理)    → NegationDetector + SectionParser
Level 1 (格式规范)  → 错别字、标点、单位、重复字
Level 2 (语义)      → 暂空，等待 Phase 2 MedBERT
Level 3 (逻辑规则)  → 快速止血 6 项规则
Level 4 (危急值)    → CriticalSignRule
```

- [ ] **Step 1: QcServiceTests（写测试 + 实现）**
- [ ] **Step 2: 集成到 Controller**
- [ ] **Step 3: 验证全部测试通过**
- [ ] **Step 4: 提交**

---

### Task 8: 基线测试 + 验证门禁

**Files:**
- Create: `scripts/run-baseline-tests.py`
- Create: `scripts/benchmark.sh`

- [ ] **Step 1: 从旧 qc.db 导出测试用例**
- [ ] **Step 2: 运行 200 份报告基准测试**
- [ ] **Step 3: 记录基线数据（误报率/精确率/召回率/P95 延迟）**
- [ ] **Step 4: 运行全面验证**

```bash
# 完整验证门禁
dotnet test src/Agent_QC/tests/Agent_QC.Tests.csproj --configuration Release
# 期望: N passed, 0 failed
```

- [ ] **Step 5: 提交基线报告**
