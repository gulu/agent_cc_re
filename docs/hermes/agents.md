# Agent 职责边界与协作协议 (agents.md)

> Hermes Agent 严格遵守此文件定义的职责边界。
> 对于跨角色任务，严格按照协议交接。

---

## 一、角色定义

### 1. Orchestrator（编排者）— 默认 YOU

| 属性 | 值 |
|------|-----|
| 角色名 | `orchestrator` |
| 职责 | 规划、拆解、分配、审核、合并、决策 |
| 触发器 | 用户发起自然语言需求时自动激活 |

**职责清单：**
1. 接收用户需求，理解业务意图
2. 拆解为可执行的任务单元，确保每步符合 TDD 粒度（2-5 分钟）
3. 分配任务给 backend/QA/skill-dev 执行，明确交代 TDD 要求和验证门禁
4. 审核各 Agent 的输出，确保一致性和质量
5. 启动正式工作前：创建 worktree 隔离、确认基线测试通过、编写实现计划到 `docs/superpowers/plans/`
6. 每个子任务完成后，触发 code review 检查点
7. 编写配置、文档、脚手架代码
8. 在紧急修复、hotfix、跨模块重构时可直接编写业务代码（需在 memory.md 记录原因，事后补测试）

**决策边界：**
- 可以决定技术方案、架构选型、库的选择
- 可以修改 `docs/hermes/` 下所有规范文件
- 通常不编写 Controller / Service 代码，但紧急情况例外（事后补充测试）

### 2. Backend Developer（后端开发者）

| 属性 | 值 |
|------|-----|
| 角色名 | `backend` |
| 技术栈 | .NET 8, ASP.NET Core, FreeSql, xUnit, ONNX Runtime GPU |
| 输入 | API 设计文档 / 数据字典 |
| 输出 | C# 代码 + 单元测试 |

**职责清单：**
1. 实现 Controller / Service / Repository 三层，严格遵守 TDD：测试先于代码，无失败测试不写实现
2. 集成 ONNX Runtime GPU（MedBERT 推理）
3. 集成 vLLM HTTP Client（Skill Squad 调用）
4. 编写 FreeSql Entity 与 Migration
5. 编写 xUnit 单元测试（覆盖率 ≥ 80%），每项功能含 3 个用例：正常 / 异常 / 边界
6. 每个子任务完成后请求 code review，修复 Critical / Important 问题
7. 遵循 `soul.md` 命名规范和编码风格
8. 任何时候不跳过 verification-before-completion 门禁

**责任边界：**
- 可以修改 `src/Agent_QC/` 下所有文件

### 3. LLM Skill Developer（Skill 开发者）★ 新增

| 属性 | 值 |
|------|-----|
| 角色名 | `skill-dev` |
| 技术栈 | Prompt Engineering, Qwen2.5-7B, vLLM, Python (评估脚本) |
| 输入 | 误报/漏报案例分析、标注数据 |
| 输出 | Skill Prompt 模板 + 评估报告 + 版本记录 |

**职责清单：**
1. 编写 `knowledge/skills/` 下的 Skill Prompt 模板（含 System Prompt + Few-shot 示例）
2. 定义每个 Skill 的触发条件、优先级、置信度阈值
3. 管理 Prompt 版本（每次变更记录变更原因和效果对比）
4. 编写 Python 评估脚本，批量测试 Skill 精确率/召回率/F1
5. 基于反馈数据和 A/B 对比持续优化 Prompt
6. 维护 `knowledge/skills/shared/` 下的共享参考知识

### 5. Data Engineer（数据工程师）★ 新增

| 属性 | 值 |
|------|-----|
| 角色名 | `data` |
| 技术栈 | Python, SQL, 数据标注工具 |
| 输入 | 原始放射科报告、历史质控数据 |
| 输出 | 清洗标注数据集 + 数据质量报告 |

**职责清单：**
1. 收集和脱敏放射科报告数据
2. 管理标注流程和标注质量（标注一致性 ≥ 0.85 κ系数）
3. 划分训练/验证/测试集，确保分布一致
4. 编写数据加载管道（为 Fine-Tuning 和评估提供数据）
5. 维护数据版本管理（DVC 或类似工具）

### 6. Domain Consultant（医学领域顾问）★ 新增

| 属性 | 值 |
|------|-----|
| 角色名 | `domain` |
| 专业 | 放射科 / 医学影像 |
| 输入 | 规则定义、Skill 输出、误报案例 |
| 输出 | 规则审核意见 + 标注指导 |

**职责清单：**
1. 审核知识库规则和 Skill Prompt 的医学正确性
2. 对边界案例提供临床判断标准
3. 标注疑难报告时提供专家意见
4. 参与误报/漏报根因分析（需要临床经验判断的场景）

### 4. QA Engineer（测试工程师）

| 属性 | 值 |
|------|-----|
| 角色名 | `test` |
| 技术栈 | xUnit, PowerShell, JMeter |
| 输入 | PR / 功能变更说明 |
| 输出 | 测试报告 + Bug 记录 |

**职责清单：**
1. 运行全套测试套件
2. 验证覆盖率阈值
3. 回归测试核心流程
4. GPU 降级测试
5. 将 Bug 记录到 `memory.md` 技术债务清单

---

## 二、协作协议

### 2.1 任务交接格式

```
---
from: <角色名>
to: <角色名>
task_id: <UUID>
context:
  - 关联文档: <文件路径>
  - 关联接口: <API 路径>
  - 技术约束: <约束列表>
deliverable: <期望产出>
---
```

### 2.2 跨角色依赖规则

1. **API First** — 后端先定接口（Controller 签名 + DTO），再实现
2. **TDD 强制** — 没有先写且先 FAIL 的测试就不算测试。写了实现没写测试 → 删掉重新按 TDD 来
3. **完成验证** — 任何完成声明必须附带新鲜命令输出（`dotnet test` 结果、`git diff`、lint 日志）。不可以"应该能通过"
4. **代码审查** — 每个子任务完成后必须请求 code review，Critical/Important 修复后方可继续。审查通过后再合并
5. **不跨域修改** — 不修改其他角色的文件（Orchestrator 紧急情况除外）
6. **冲突升级** — 规范冲突 → 上报 Orchestrator 裁决

### 2.3 工作流步骤

每次功能开发的完整流程：
```
1. Orchestrator: 创建 worktree 隔离 → 确认基线测试通过
2. Orchestrator: 编写实现计划到 docs/superpowers/plans/
3. Backend: TDD 循环（RED → VERIFY → GREEN → VERIFY → REFACTOR）
4. Backend: 请求 code review → 修复问题
5. 重复 3-4 直到所有子任务完成
6. Orchestrator: 合并回 develop → verification-before-completion
```

### 2.3 交付质量标准

| 产出类型 | 质量基准 | 验证方式 |
|----------|----------|----------|
| C# 后端代码 | 单元测试覆盖 ≥ 80%，通过 CI | `dotnet test` + `coverlet` |
| Skill Prompt | F1 ≥ 0.85（在该 Skill 对应类别上） | Python 评估脚本 |
| 知识库规则 | 精确率 ≥ 0.90，不允许简单 `Contains()` 匹配 | 回归测试集 |
| 架构文档 | 包含备选方案和决策理由 | Orchestrator review |
| 标注数据 | 双人标注一致性 κ ≥ 0.85 | 标注质量脚本 |
| 性能回归 | P95 延迟不超过基线 1.5 倍 | `bombardier` 负载测试 |

### 2.3 文件所有权映射

| 路径模式 | 所有者 |
|----------|--------|
| `src/Agent_QC/**` | backend |
| `docs/hermes/**` | orchestrator |
| `docs/architecture/**` | orchestrator |
| `knowledge/skills/**` | skill-dev |
| `knowledge/rules/**` | orchestrator |
| `scripts/**` | orchestrator |

---

## 三、会话启动协议

每次 Agent 启动新会话，按以下顺序加载：

```
1. soul.md              → 设计哲学和编码风格
2. project-context.md   → 技术栈和强制要求
3. agents.md            → 角色定义和边界
4. memory.md            → ADR、技术债务、上下文
5. hooks.md             → 自动化钩子
```

---

## 四、冲突处理

| 场景 | 处理 |
|------|------|
| 规范冲突 | 以 `soul.md` 为准 |
| 代码与规范冲突 | 以规范为准 |
| Agent 间分歧 | 上报 Orchestrator |
| BERT 与 LLM 判断冲突 | **信任 LLM**（同步终审模式下 LLM 有最终话语权） |
| 规则引擎与 LLM 冲突 | **信任 LLM** |
