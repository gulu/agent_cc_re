# CI/CD 流水线设计

---

## 1. 流水线概览

```
Push / PR
    │
    ▼
┌──────────────┐
│ Build        │  dotnet build
├──────────────┤
│ Test         │  dotnet test (unit + integration)
├──────────────┤
│ Coverage     │  coverlet → ≥ 80%
├──────────────┤
│ Lint         │  dotnet format --verify-no-changes
├──────────────┤
│ GPU Test     │  GPU 降级测试（模拟 ONNX Runtime 故障）
├──────────────┤
│ Security     │  dependency scan + secret scan
└──────┬───────┘
       │ (PR merge to main)
       ▼
┌──────────────┐
│ Deploy       │  手动触发 → 发布到 staging
└──────────────┘
```

---

## 2. GitHub Actions 配置

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [develop]
  pull_request:
    branches: [main, develop]

jobs:
  build-and-test:
    runs-on: windows-latest  # Agent_QC 是 .NET 项目，需要 Windows
    # GPU 测试需要在自托管 runner 上运行
    # runs-on: [self-hosted, gpu-rtx4090]

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Restore
        run: dotnet restore src/Agent_QC/

      - name: Build
        run: dotnet build src/Agent_QC/ --no-restore --configuration Release

      - name: Unit Tests
        run: dotnet test tests/Agent_QC.Tests/ --configuration Release --no-build

      - name: Coverage
        run: |
          dotnet tool install -g coverlet.console
          coverlet tests/Agent_QC.Tests/bin/Release/net8.0/Agent_QC.Tests.dll \
            --target dotnet \
            --targetargs "test tests/Agent_QC.Tests/ --no-build" \
            --format opencover \
            --threshold 80

      - name: GPU Degradation Test
        run: dotnet test tests/Agent_QC.Tests/ --filter "Category=GPU"

      - name: Lint
        run: dotnet format src/Agent_QC/ --verify-no-changes

      - name: Security Scan
        uses: github/codeql-action/analyze@v3
```

---

## 3. 部署流水线

```yaml
# .github/workflows/deploy.yml
name: Deploy

on:
  workflow_dispatch:
    inputs:
      environment:
        type: choice
        options: [staging, production]

jobs:
  deploy:
    runs-on: [self-hosted, gpu-rtx4090]

    steps:
      - uses: actions/checkout@v4

      - name: Build Release
        run: dotnet publish src/Agent_QC/ --configuration Release -o publish/

      - name: Backup Current
        run: robocopy /E /COPYALL C:\Agent_QC C:\Agent_QC_backup

      - name: Deploy
        run: |
          Stop-Service AgentQC
          robocopy /E /COPYALL publish\ C:\Agent_QC
          Start-Service AgentQC

      - name: Health Check
        run: |
          Start-Sleep 10
          Invoke-WebRequest http://localhost:5100/api/v1/health | Should-Be "healthy"
```

---

## 4. 环境要求

| 环境 | 硬件 | 软件 |
|------|------|------|
| Staging | RTX 4090 × 1 | Windows Server 2022 或 Ubuntu 22.04, .NET 8, CUDA 12.4, Python 3.11, vLLM |
| Production | RTX 4090 × 1 | 同上 |
| CI Runner | 自托管（云端） | `ubuntu-latest`（Build + Lint + 单元测试） |
| CI Runner | 自托管（GPU） | Ubuntu 22.04 + RTX 4090（GPU 降级测试 + 集成测试） |

> .NET 8 跨平台运行，建议生产环境评估 Ubuntu 22.04（GPU 驱动生态、容器化部署更成熟）。Tesseract 在 Linux 上通过 `libtesseract` 同样可用。

---

## 5. 发布检查清单

| # | 检查项 |
|---|--------|
| 1 | 所有 CI 检查通过 |
| 2 | 覆盖率 ≥ 80% |
| 3 | 性能基准测试通过（延迟 < 300ms） |
| 4 | 500+ 回归报告准确率 > 90% |
| 5 | GPU 降级测试通过 |
| 6 | vLLM 健康检查通过 |
| 7 | Staging 环境验证通过（人工确认） |
| 8 | 备份当前生产版本 |
| 9 | 生产健康检查通过 |
| 10 | 监控面板确认指标正常 |
