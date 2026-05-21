# Git 提交规范与分支策略

---

## 1. 分支策略

```
main          ← 生产环境，只接受 PR 合并，禁止直接 push
  │
  ├── develop  ← 开发主线
  │     │
  │     ├── feature/qc-pipeline     # 质控管线功能
  │     ├── feature/bert-gpu        # BERT GPU 推理
  │     ├── feature/skill-squad     # Skill Squad 功能
  │     ├── feature/rules-engine    # 规则引擎增强
  │     ├── feature/oracle-merge    # Oracle 查询合并
  │     ├── fix/gender-false-pos    # 性别矛盾误报修复
  │     └── docs/architecture       # 文档更新
  │
  └── release/v1.0  ← 发布分支
```

### 分支命名

| 类型 | 格式 | 示例 |
|------|------|------|
| 功能分支 | `feature/<short-desc>` | `feature/skill-squad` |
| 修复分支 | `fix/<short-desc>` | `fix/gender-checker-negation` |
| 文档分支 | `docs/<short-desc>` | `docs/hermes-memory` |
| 发布分支 | `release/v<major>.<minor>` | `release/v1.0` |
| 热修复 | `hotfix/<short-desc>` | `hotfix/vllm-timeout` |

---

## 2. 提交信息格式（Conventional Commits）

```
<type>(<scope>): <subject>

type:      feat | fix | refactor | test | docs | chore | perf
scope:     qc-pipeline | bert | skill-squad | rules | scoring | devops | docs
subject:   中文或英文，最多 72 字符，动词开头，不加句号

body:      （可选）详细说明，每行最多 72 字符

footer:    （可选）Closes #123, BREAKING CHANGE
```

### 示例

```
feat(qc-pipeline): 添加 Level 0 否定检测预处理层

新增 NegationDetector 类，支持 7 种否定词的检测与作用域计算。
可消除 ~60% 因否定语境导致的性别矛盾误报。

Closes #42
```

```
fix(skill-squad): gender-checker 对"未见子宫"的误报

修改 exclude_patterns 白名单，新增"未见显示（男性，正常骨盆）"模式。
经 100 份报告验证，此类误报从 12% 降至 0.5%。
```

```
test(qc-pipeline): 新增性别矛盾检测 12 条测试用例

覆盖场景：否定语境 4 条、引用语境 2 条、正常矛盾 3 条、边界 3 条
```

```
perf(bert): MedBERT 三任务 batch 推理优化

将错别字/NER/NLI 三次独立推理合并为一次 CUDA kernel launch。
延迟从 ~15ms 降至 ~8ms。
```

---

## 3. 禁止提交的内容

| 类型 | 示例 |
|------|------|
| 密钥/密码 | 任何含 `password`/`secret`/`token`/`api_key` 的代码 |
| 大文件 | > 10MB 的模型文件（模型不入 Git 仓库，通过 `models/README.md` 记录下载来源） |
| 二进制 | DLL、EXE、SO 等编译产物 |
| 临时文件 | `*.tmp`、`*.bak`、`*~` |
| 敏感数据 | 患者数据、真实报告文本 |
| IDE 配置 | `.vs/`、`.idea/`、`*.user` |

.gitignore 中配置：

```
# 模型文件 — 不进入 Git 仓库（即使用 LFS 也因体积过大而不入库）。
# 模型通过独立存储（NAS / HuggingFace 缓存 / models/README.md 记录下载命令）管理。
*.onnx
*.gguf
*.bin

# 编译产物
bin/
obj/
*.dll
*.exe
*.so

# IDE
.vs/
.idea/
*.user
*.suo

# 敏感数据
*.secret.*
*_test_data/*
```

---

## 4. PR 合并要求

| 检查项 | 要求 | 阻塞级别 | 验证方式 |
|--------|------|:------:|----------|
| CI 通过 | 所有自动化测试通过 | **阻塞** | GitHub Actions 绿标 |
| 代码审查 | Code Review 通过，无 Critical/Important 未修复 | **阻塞** | Review 子 agent 报告 |
| TDD 合规 | 新功能没有先写测试？退回 | **阻塞** | Code Review 检查 |
| 完成验证 | 提交时附带验证命令输出 | **阻塞** | PR 描述中贴测试日志 |
| 覆盖率 | ≥ 80%，不低于合并前 | **阻塞** | coverlet 报告 |
| 提交格式 | 符合 Conventional Commits | 警告 | `commit-msg` 钩子 |
| 无大文件 | 无 > 10MB 模型文件 | **阻塞** | pre-commit 钩子 |
| 无密钥 | 无密钥泄露 | **阻塞** | pre-commit 钩子 + CodeQL |

### 4.1 PR 提交模板

每个 PR 描述必须包含：

```markdown
## 变更内容
[简述改动]

## 验证证据
```
dotnet test tests/Agent_QC.Tests/ --configuration Release
# 粘贴测试输出：N passed, 0 failed
```

## 审查清单
- [ ] 测试先写且先 FAIL 过
- [ ] 实现最小化，无 YAGNI
- [ ] Code Review 已通过
- [ ] 200+ 回归测试无退化
```

---

## 5. Git Hooks（pre-commit）

```bash
#!/bin/bash
# .git/hooks/pre-commit

# 1. 代码格式
dotnet format --verify-no-changes src/Agent_QC/

# 2. 密钥扫描
if git diff --cached | grep -iE "(password|secret|token|api_key)\s*="; then
    echo "❌ 检测到潜在密钥泄露，禁止提交"
    exit 1
fi

# 3. 大文件检查
for file in $(git diff --cached --name-only); do
    if [ -f "$file" ] && [ $(stat -c%s "$file") -gt 50000000 ]; then
        echo "❌ $file 超过 50MB，禁止提交"
        exit 1
    fi
done
```
