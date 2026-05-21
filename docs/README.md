# Agent_QC 文档索引

>  Agent_QC — 基于 GPU 加速（RTX 4090）+ Hermes Agent Skill Squad 架构的放射科报告智能质控平台。

## 文档结构

```
docs/
├── README.md                          # ← 本文件
│
├── hermes/                            # Hermes 规范（启动必读，按顺序加载）
│   ├── soul.md                        #   设计哲学与编码风格
│   ├── project-context.md             #   技术栈与强制约束（含 GPU 架构）
│   ├── agents.md                      #   角色定义与协作协议
│   ├── memory.md                      #   架构决策记录 (ADR) + 技术债务
│   └── hooks.md                       #   构建/测试/Git 钩子配置
│
├── architecture/                      # 架构设计专题
│   ├── gpu-pipeline.md                #   GPU 原生推理管线设计
│   ├── hermes-skill-squad.md          #   Hermes Agent 审查员团队架构
│   └── service-merge.md               #   QCService + ReportQC 合并方案
│
├── quality/                           # 质控质量专题
│   ├── context-blindness.md           #   五种语境盲区根因分析
│   ├── layered-defense.md             #   五层防御体系设计
│   └── quick-fixes.md                 #   本周可执行的快速止血清单
│
├── implementation/                    # 实施落地
│   ├── roadmap.md                     #   四阶段实施路线图
│   └── model-selection.md             #   模型选型与部署方案
│
├── devops/                            # 工程化
│   ├── github-workflow.md             #   Git 提交规范与分支策略
│   ├── ci-cd.md                       #   CI/CD 流水线设计
│   └── testing-strategy.md            #   测试体系设计
│
├── superpowers/                       # 开发工作流（Superpowers 规范；含 plans/ 目录）
└── appendix/                          # 附录
    └── feature-inventory.md           #   当前系统完整功能清单（基线）
```

## 会话启动协议（Agent 必读）

每次 Agent 启动新会话，**必须**按以下顺序加载：

```
1. hermes/soul.md              → 设计哲学、TDD 强制、完成验证门禁
2. hermes/project-context.md   → 技术栈、硬件约束、并发模型
3. hermes/agents.md            → 角色边界、协作协议、工作流步骤
4. hermes/memory.md            → ADR、技术债务、上下文锚点
5. hermes/hooks.md             → 自动化钩子、Superpowers 开发流程
```

开发工作流规范见 `soul.md` 第三章（Superpowers 规范）和 `superpowers/` 目录。

## 项目背景

Agent_QC 是 AI_QC-system 的重构升级版本，核心变更：

| 维度 | AI_QC-system (当前) | Agent_QC (目标) |
|------|-------------------|----------------|
| 推理引擎 | TinyBERT 4L312D (CPU) | MedBERT-110M (GPU) |
| LLM 终审 | 无 | Qwen2.5-7B INT4 (GPU) |
| 架构 | CPU 串行管线 | GPU 并行 + Hermes Skill Squad |
| 服务拆分 | ReportQC + QCService 两个独立服务 | 合并为单一 Agent_QC 服务 |
| 响应延迟 | 100-500ms | < 300ms (含 LLM 终审) |
| 误报率 | ~40% | 目标 < 5% |
| 硬件 | CPU only | RTX 4090 单卡 |
| 知识库 | C# 硬编码 | YAML 外部化 |
| 测试覆盖率 | < 5% | ≥ 80% |
