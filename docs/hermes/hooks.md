# Hermes 钩子规范 (hooks.md)

> 特定事件触发时自动执行的操作。保证代码质量和流程合规。

---

## 一、Git 提交钩子

| 触发时机 | 操作 | 说明 |
|----------|------|------|
| `pre-commit` | 代码风格检查 | `dotnet format --verify-no-changes` |
| `pre-commit` | 禁止大文件 | 检查暂存区无 > 50MB 文件（模型文件除外） |
| `pre-commit` | 禁止密钥泄露 | 扫描含 `password`/`secret`/`token` 等模式的代码 |
| `commit-msg` | 提交信息格式校验 | 必须符合 Conventional Commits 规范 |

### 提交信息格式

```
<type>(<scope>): <subject>

type: feat | fix | refactor | test | docs | chore | perf
scope: qc-pipeline | bert | skill-squad | rules | scoring | devops
subject: 中文或英文，最多 72 字符

示例：
  feat(qc-pipeline): 添加 Level 0 否定检测预处理层
  fix(skill-squad): 修复 gender-checker 对"未见子宫"的误报
  test(qc-pipeline): 新增性别矛盾检测 12 条测试用例
```

---

## 二、构建与测试钩子

| 触发时机 | 操作 | 说明 |
|----------|------|------|
| `dotnet build` 前 | FreeSql 实体一致性检查 | 核对 Entity 与数据库 Schema |
| `dotnet build` 时 | 编译警告视为错误 | `TreatWarningsAsErrors`（CI 中开启，本地可选） |
| `pre-commit` | 增量单元测试 | 只运行与变更文件相关的测试，< 30s |
| `pre-push` 或 CI | 全量测试 + 覆盖率 | `dotnet test` + `coverlet --threshold 80`，CI 中阻塞合并 |
| CI 中 | GPU 降级测试 | 仅在自托管 GPU runner 上运行 |

---

## 三、测试钩子

| 触发时机 | 操作 |
|----------|------|
| PR 提交 | 运行全量单元测试 + 集成测试 |
| PR 提交 | GPU 降级测试（模拟 ONNX Runtime 故障） |
| 合并到 main | 运行性能基准测试（延迟 < 300ms） |
| 发布前 | 运行 500+ 报告回归测试集，准确率 > 90% |

---

## 四、GPU 健康检查与降级策略

| 触发时机 | 操作 |
|----------|------|
| 服务启动 | 检查 ONNX Runtime CUDA EP 是否可用（warm-up 推理） |
| 服务启动 | 启动 vLLM 子进程，轮询 `/health` 最多 30s |
| 服务启动 | GPU 不可用 → Warn + CPU EP 降级 + `/health` 返回 `degraded` |
| 运行时每 120s | vLLM 心跳检查 |
| 连续 3 次心跳失败 | 告警日志 + 重启 vLLM 子进程（先优雅 shutdown，5s 超时后 SIGKILL） |
| vLLM 重启期间 | 新请求跳过 Skill Squad，运行中请求超时返回 partial 结果 |
| 重启成功后 | Info 日志 + `/health` 恢复 `healthy` |
| ONNX Runtime 异常 | 动态切换 CPU EP，不重启进程 |
| GPU 显存不足 | `CUDA_OUT_OF_MEMORY` → Error 日志 + 降级 CPU，触发告警 |

降级使用指数退避重试：首次等 5s，第二次 10s，第三次 20s，之后告警等待人工介入。

---

## 五、Agent 交互钩子

### 5.1 会话启动

| 顺序 | 文件 | 说明 |
|:---:|------|------|
| 1 | `soul.md` | 设计哲学、TDD 强制、完成验证门禁 |
| 2 | `project-context.md` | 技术栈、硬件约束、并发模型 |
| 3 | `agents.md` | 角色边界、协作协议、工作流步骤 |
| 4 | `memory.md` | ADR 记录、技术债务、上下文锚点 |
| 5 | `hooks.md` | 自动化钩子、superpowers 流程钩子 |

### 5.2 Superpowers 开发流程钩子

| 触发时机 | 操作 |
|----------|------|
| 开始功能开发 | 创建 worktree 隔离工作空间 |
| 开发前 | 确认基线测试通过，否则不开始 |
| 复杂任务 | 编写实现计划到 `docs/superpowers/plans/`，获批准后执行 |
| 修改代码前 | 先写测试 → 验证 FAIL → 再写实现（TDD 强制） |
| 代码修改后 | 验证 PASS + 其他测试不退化 |
| 子任务完成 | 请求 code review，修复 Critical/Important |
| 完成声明前 | 运行验证命令（测试 + lint + 构建），输出作为证据 |
| 合并前 | 回归测试集通过、覆盖率 ≥ 80%、审查通过 |

### 5.3 通用文件检查钩子

| 触发时机 | 操作 |
|----------|------|
| 新建文件 | 检查命名是否符合 `soul.md` 规范 |
| 新建 Service | 检查是否使用依赖注入接口模式（禁止 `public static`） |
| 修改 DTO | 检查是否同步修改了对应的 API 测试 |
| 修改 Skill Prompt | 触发对应 Skill 的评估脚本 |
| 修改知识库 YAML | 运行回归测试集确认无退化 |
| Orchestrator 直接写代码 | 在 `memory.md` 记录原因和范围 |
