# 测试体系设计

> TDD 是强制工作流，不是可选实践。`soul.md` 核心原则第 5 条："测试先于代码"。

---

## 1. TDD 强制规范

### 1.1 铁律

```
没有先失败过的测试 = 不算测试 → 没有实现
```

每次写生产代码之前，必须先：
1. 写测试
2. 运行测试 → 确认 FAIL（失败原因是功能缺失而非笔误）
3. 写最简实现
4. 运行测试 → 确认 PASS
5. 重构 → 保持 GREEN

### 1.2 违规处理

| 发现场景 | 处理 |
|----------|------|
| 提交中有实现代码但没有测试 | 退回，补 TDD 循环 |
| 测试写在了实现之后（test-after） | 删掉实现，按测试先于代码重来 |
| 测试没看到 FAIL 就直接通过了 | 无效测试，调试验证逻辑再重来 |
| 测试名不描述行为（如 `test1`） | 重命名，按行为命名 |
| 测试同时测多个行为 | 拆分：一条测试只测一个行为 |

### 1.3 完成验证门禁

每次声称"完成"前必须：
```bash
# 1. 全量测试
dotnet test tests/Agent_QC.Tests/ --configuration Release
# 确认输出：N passed, 0 failed

# 2. 覆盖率
coverlet ... --threshold 80
# 确认输出：threshold 80% met

# 3. Lint
dotnet format src/Agent_QC/ --verify-no-changes
# 确认输出：0 changes needed
```

"应该能通过" = 没通过。必须有新鲜命令输出。

---

## 2. 测试金字塔

```
         ┌─────────┐
         │   E2E    │  10 条：全流程验证
         │ ~30min   │
        ┌┴─────────┴┐
        │ Integration│  30 条：API + GPU + vLLM 集成
        │  ~10min    │
       ┌┴───────────┴┐
       │    Unit      │  200+ 条：每个规则 / Skill / 方法
       │   ~2min      │
       └─────────────┘
```

---

## 3. 单元测试 (xUnit + Moq)

### 覆盖要求

| 模块 | 最低覆盖率 | 优先级 |
|------|:------:|:----:|
| Services/QcService | 85% | P0 |
| Services/Level1QcService | 90% | P0 |
| Services/Level2QcService | 90% | P0 |
| Hermes/NegationDetector | 95% | P0 |
| Hermes/SectionParser | 90% | P1 |
| Hermes/Orchestrator | 85% | P1 |
| Controllers/* | 80% | P1 |
| Repository/* | 80% | P1 |
| Models/DTOs | 90% | P1 |

### 每个规则至少 3 个测试用例

| 用例类型 | 示例 |
|----------|------|
| 正常（应触发） | 男性患者 + "子宫肌瘤" → 应报 critical |
| 异常（应跳过） | 男性患者 + "子宫未见显示" → 应跳过 |
| 边界（难判断） | 男性患者 + "子宫切除术后复查" → 应跳过 |

### 否定检测测试用例（≥ 20 条）

```
"未见明确结节影"              → 结节[否定]
"无明显强化"                  → 强化[否定]
"未见子宫及附件异常"          → 子宫[否定] 附件[否定]
"子宫未见显示（男性，正常）"    → 子宫[否定]
"未见异常，但建议随访"         → 异常[否定] 随访[非否定]
"前列腺未见占位，膀胱正常"     → 占位[否定] 膀胱[非否定]
```

### 段落解析测试用例（≥ 10 条）

验证能正确识别 ClinicalHistory / Findings / Impression / Recommendation 段落边界。

---

## 4. 集成测试

### API 测试（每个端点 ≥ 2 条）

```csharp
[Fact]
public async Task PostQcReport_NormalReport_ReturnsPassed()
{
    var response = await client.PostAsJsonAsync("/api/v1/qc/report", new QcRequest
    {
        ReportId = "TEST-001",
        Findings = "双肺纹理清晰，未见明确肿块影。",
        Impression = "未见明显异常。",
        PatientGender = "男",
        PatientAge = 45,
        ExamPart = "胸部",
        ExamDevice = "CT"
    });

    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<AjaxResult>();
    Assert.Equal(200, result.Code);  // 应该是成功的
}
```

### GPU 降级测试

```csharp
[Fact]
[Category("GPU")]
public async Task GpuUnavailable_FallsBackToCpu()
{
    // 模拟 ONNX Runtime CUDA EP 不可用
    Environment.SetEnvironmentVariable("ORT_DISABLE_CUDA", "1");
    
    var response = await client.PostAsJsonAsync("/api/v1/qc/report", validRequest);
    
    response.EnsureSuccessStatusCode();
    // 验证自动降级到 CPU，不报 500
}
```

### 健康检查测试

```csharp
[Fact]
public async Task HealthCheck_AllComponentsOk()
{
    var response = await client.GetFromJsonAsync<HealthResponse>("/api/v1/health");
    
    Assert.Equal("healthy", response.Status);
    Assert.Equal("ok", response.Checks["database"]);
    Assert.Equal("ok", response.Checks["tesseract"]);
}
```

---

## 5. 回归测试集

### 报告来源与标注流程

| 来源 | 数量 | 用途 |
|------|:--:|------|
| 历史质控数据（QcReport 表脱敏） | 300+ | 真实报告分布 |
| 人工构造边界 case | 100+ | 覆盖五种语境盲区 |
| 标注的误报/漏报历史 | 100+ | 重点验证修复效果 |

**标注数据管理：**
- 标注格式：JSONL，每行一条报告 + 标注结果
- 标注内容：期望质控结果（每个规则的期望判定：passed / warning / error / critical）
- 双人独立标注，κ ≥ 0.85 方可纳入测试集
- 标注文件路径：`tests/test_data/annotated/`，纳入 Git 版本管理（脱敏后）
- 标注工具：推荐使用 Label Studio 或 doccano

### 回归测试结构

```csharp
// tests/Agent_QC.Tests/Regression/
//   ├── GenderConflictTests.cs      (50 条)
//   ├── NegationTests.cs            (40 条)
//   ├── CriticalSignTests.cs        (30 条)
//   ├── SiteConsistencyTests.cs     (30 条)
//   ├── DeviceMethodTests.cs        (25 条)
//   ├── MeasurementTests.cs         (20 条)
//   ├── RadsComplianceTests.cs      (20 条)
//   └── TerminologyTests.cs         (15 条)
```

### 评估指标

| 指标 | 公式 | 目标 |
|------|------|:--:|
| 精确率 | TP / (TP + FP) | > 0.90 |
| 召回率 | TP / (TP + FN) | > 0.85 |
| F1 | 2 × P × R / (P + R) | > 0.87 |
| 误报率 | FP / (FP + TN) | < 5% |

---

## 6. 性能测试

### 延迟基准

```bash
# Bombardier 压力测试（单路基准）
bombardier -c 1 -n 200 -m POST \
  -H "Content-Type: application/json" \
  -f test_report.json \
  http://localhost:5100/api/v1/qc/report

# 期望（简单正常报告，无 Skill 触发）：
# P50 < 40ms, P95 < 60ms, P99 < 100ms

# 并发压力测试
bombardier -c 10 -n 1000 -m POST \
  -H "Content-Type: application/json" \
  -f test_report.json \
  http://localhost:5100/api/v1/qc/report

# 期望（混合负载，含 Skill 触发）：
# P50 < 200ms, P95 < 600ms, P99 < 1000ms
# 错误率 < 0.1%
# degraded 率（GPU 不可用）< 0.01%
```

### 并发测试

- 10 并发 × 100 请求 → 零 500 错误
- vLLM concurrent batching 验证 → GPU 利用率 < 80%

---

## 7. 测试数据管理

| 规则 | 说明 |
|------|------|
| 测试数据脱敏 | 所有测试数据使用虚构患者信息 |
| 测试数据库 | 独立 SQLite `tests/test_data/qc_test.db` |
| 种子数据 | 每个测试类 `Initialize` 中独立 seed |
| 清理 | `Dispose` 中清理测试数据 |
| CI 中 GPU 测试 | 仅在自托管 GPU runner 上运行 |
