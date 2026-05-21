# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Agent_QC is a GPU-accelerated (RTX 4090) radiology report quality control platform. It replaces the CPU-only AI_QC-system with a 4-layer GPU-native pipeline backed by the Hermes Agent Skill Squad architecture.

**Tech stack:** .NET 8 + ASP.NET Core, FreeSql CodeFirst + SQLite, ONNX Runtime GPU (CUDA EP) for MedBERT-Chinese, vLLM HTTP API for Qwen2.5-7B GPTQ-INT4, YAML-based knowledge base, xUnit + Moq testing.

## Repository structure

This repository contains only design documents and specifications. The actual C# source code (`src/Agent_QC/`) has not been created yet. All development work is planned through the doc specs.

```
docs/
├── hermes/          # Core specs (load order: soul → project-context → agents → memory → hooks)
├── architecture/    # GPU pipeline, Skill Squad, service merge designs
├── quality/         # Context blindness analysis, layered defense, quick fixes
├── implementation/  # Roadmap, model selection
└── devops/          # Git workflow, CI/CD, testing strategy
```

## Session initialization

Every new session must load the Hermes specification files in order:
1. `docs/hermes/soul.md` — design philosophy, coding style, naming conventions
2. `docs/hermes/project-context.md` — tech stack, hardware/memory constraints, pipeline architecture
3. `docs/hermes/agents.md` — four roles (orchestrator, backend, skill-dev, test) and their boundaries
4. `docs/hermes/memory.md` — ADR decisions (13 recorded), technical debt, iteration kanban
5. `docs/hermes/hooks.md` — pre-commit/build/test GPU health hooks

When specs conflict, `soul.md` takes precedence.

## Architecture

**QC Pipeline (4 levels):**
```
QcRequest → Level 0: Pre-filter (CPU, ~10ms)
         → Level 1: BERT Sentinels (GPU, ~8ms, 3 parallel tasks)
         → Level 2: Hermes Skill Squad (GPU, ~120ms, 8 on-demand Skills via vLLM)
         → Level 3: QA Arbiter + Scoring (CPU, ~20ms)
         → QcResponse
```

- Normal reports: ~25ms | One suspicious point: ~105ms | Complex + arbitration: ~250ms
- Hard latency budget: < 1s (target < 300ms)

**Layered architecture:** Controller → Service → Repository (standard .NET 8 API)
- Single service on port 5100 (QCService + ReportQC merged into Agent_QC)
- OCR (Tesseract) runs in-process
- Oracle query module merged into Repository layer

**GPU model deployment:**
- MedBERT-Chinese ONNX FP16 via `Microsoft.ML.OnnxRuntime.Gpu` (0.2GB VRAM)
- Qwen2.5-7B GPTQ-INT4 via vLLM HTTP API at `localhost:8100` (5.0GB VRAM)
- Total VRAM budget: 7.7GB / 24GB (32%), leaving room for expansion

**Hermes Skill Squad:** 8 specialized LLM reviewers (gender-anatomy-checker, site-consistency-checker, findings-impression-nli, critical-sign-arbiter, device-method-validator, measurement-completeness, rads-compliance-checker, terminology-validator). Orchestrator dispatches them in parallel on demand, only triggering QA Arbiter when conflicts arise. See `docs/architecture/hermes-skill-squad.md`.

**Conflict resolution:** Rules < BERT < Skill Squad < Hermes QA. LLM always has final say in synchronous mode.

## C# coding conventions

- Namespace: `AgentQC.Module` (e.g., `AgentQC.Controllers`)
- API routes: `/api/v{version}/[controller]`
- DTOs as `record` types, use `var` when type is obvious
- Async methods: `Async` suffix
- Private fields: `_camelCase`
- Service methods: `public static`, receive `IFreeSql` from Controller
- Use `AjaxResult` for unified API response format `{code, data, msg}`
- No `region` keyword (use partial class or separate files)
- Service methods wrap in try-catch, log exceptions via `JSBaseLogs.JSLogManager`
- Soft deletes only — no DELETE/DROP/ALTER
- Connection strings from environment variables, patient data must be desensitized

## Build / test / lint commands

```bash
# Restore and build
dotnet restore src/Agent_QC/
dotnet build src/Agent_QC/ --configuration Release

# Run tests
dotnet test tests/Agent_QC.Tests/ --configuration Release

# Run tests with coverage (threshold: 80%)
coverlet tests/Agent_QC.Tests/bin/Release/net8.0/Agent_QC.Tests.dll \
  --target dotnet \
  --targetargs "test tests/Agent_QC.Tests/ --no-build" \
  --format opencover --threshold 80

# Format check
dotnet format src/Agent_QC/ --verify-no-changes

# GPU-specific tests
dotnet test tests/Agent_QC.Tests/ --filter "Category=GPU"
```

## Key constraints

- **VRAM budget:** < 8GB of 24GB on single RTX 4090
- **Test coverage:** ≥ 80% (blocking in CI), each QC Skill ≥ 10 test cases
- **False-positive rate target:** < 5% (current ~40%)
- **API versioning:** `/api/v{version}/`
- **Conventional Commits:** `type(scope): subject` — scopes: `qc-pipeline`, `bert`, `skill-squad`, `rules`, `scoring`, `devops`
- **Branch strategy:** `main` (PR only) ← `develop` ← `feature/*`, `fix/*`, `docs/*`
- **Git hooks:** pre-commit checks format + secret scan + large file check

## Current state

The project is at **Phase 1** (GPU pipeline setup + service merge + quick fixes) pending start. The pre-existing AI_QC-system codebase (v0.2) serves as the baseline. Key technical debt: `string.Contains()` rough matching causing ~40% false positives, knowledge base hardcoded in C#, TinyBERT lack of medical tuning, test coverage < 5%.
