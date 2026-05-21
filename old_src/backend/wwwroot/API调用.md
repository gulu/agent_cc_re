# 报告质控 API 调用说明

> **接口**：`POST /api/v1/qc/report`  
> **功能**：对医技检查报告执行全流程智能质控（4 层管线：文本层 → 语义层 → 逻辑层 → 临床层）  
> **Content-Type**：`application/json`

---

## 请求参数

### 完整请求体结构

```json
{
  "reportId": "RIS-20260507-001",
  "findings": "双肺纹理清晰，未见明确肿块影。心脏大小正常。",
  "impression": "右肺上叶可见结节影，建议随访。",
  "reportType": "CT",

  "patientName": "张三",
  "patientGender": "男",
  "patientAge": 55,
  "patientIdNo": "****1234",
  "outpatientNo": "OP-20260507-001",
  "patientPhone": "138****5678",

  "clinicalDiagnosis": "咳嗽待查",
  "symptoms": "咳嗽、咳痰2周",
  "medicalHistory": "高血压史10年",
  "requestDepartment": "呼吸内科",
  "requestDoctor": "李医生",

  "examPart": "胸部",
  "examDevice": "CT",
  "examMethod": "平扫+增强",
  "requestNo": "REQ-20260507-001",
  "accessionNo": "ACC-20260507-001",
  "examDate": "2026-05-07",
  "reportDate": "2026-05-07"
}
```

### 字段说明

| 分组 | 字段 | 类型 | 必填 | 说明 |
|------|------|------|:----:|------|
| **报告信息** | `reportId` | string | ✅ | 报告唯一标识，最长 64 字符 |
| | `findings` | string | ✅ | 影像所见描述 |
| | `impression` | string | ✅ | 诊断结论/印象 |
| | `reportType` | string | | CT / MRI / DR / 超声 / 内镜 / 钼靶 / DSA |
| **患者信息** | `patientName` | string | | 患者姓名 |
| | `patientGender` | string | | 男 / 女 |
| | `patientAge` | int | | 年龄（岁） |
| | `patientIdNo` | string | | 身份证号（建议脱敏） |
| | `outpatientNo` | string | | 门诊号 |
| | `patientPhone` | string | | 电话（建议脱敏） |
| **临床信息** | `clinicalDiagnosis` | string | | 临床诊断 |
| | `symptoms` | string | | 临床症状 |
| | `medicalHistory` | string | | 病史摘要 |
| | `requestDepartment` | string | | 申请科室 |
| | `requestDoctor` | string | | 申请医生 |
| **检查信息** | `examPart` | string | | 检查部位 |
| | `examDevice` | string | | 检查设备 |
| | `examMethod` | string | | 检查方法（平扫/增强/彩超等） |
| | `requestNo` | string | | 申请单号 |
| | `accessionNo` | string | | 检查号 |
| | `examDate` | string | | 检查日期（yyyy-MM-dd） |
| | `reportDate` | string | | 报告日期（yyyy-MM-dd） |

---

## 响应格式

### 成功响应

```json
{
  "Code": 200,
  "Data": {
    "reportId": "RIS-20260507-001",
    "totalScore": 89.75,
    "passScore": 90.0,
    "passed": false,
    "qcLevel": "初级质控",
    "checkItems": [
      {
        "dimensionCode": "normative",
        "dimensionName": "规范性",
        "passed": true,
        "score": 95.0,
        "weight": 30.0
      },
      {
        "dimensionCode": "completeness",
        "dimensionName": "全面性",
        "passed": true,
        "score": 100.0,
        "weight": 30.0
      },
      {
        "dimensionCode": "logic",
        "dimensionName": "逻辑性",
        "passed": true,
        "score": 65.0,
        "weight": 25.0
      },
      {
        "dimensionCode": "timeliness",
        "dimensionName": "及时性",
        "passed": true,
        "score": 100.0,
        "weight": 15.0
      }
    ],
    "issues": [
      {
        "issueType": "semantic_conflict",
        "subType": "negation_positive_conflict",
        "description": "所见中描述「未见」但结论中出现「可见」，阴阳性矛盾",
        "severity": "critical",
        "location": "whole",
        "originalText": "",
        "suggestedText": "",
        "suggestion": "请检查所见与结论的阴阳性描述是否一致"
      }
    ],
    "summary": "评分 89.75 分，未通过。共 3 项质控提示（1 项严重问题、1 项错误、1 项警告）。",
    "processTimeMs": 205
  },
  "Msg": "success"
}
```

### 响应字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| `Code` | int | 200 成功，500 失败 |
| `Msg` | string | 成功为 "success"，失败为错误描述 |
| `Data.totalScore` | decimal | 质控总分（0-100） |
| `Data.passScore` | decimal | 及格分数线（默认 90） |
| `Data.passed` | bool | 是否通过 |
| `Data.qcLevel` | string | 质控等级：高级/中级/初级 |
| `Data.checkItems` | array | 4 维度评分详情 |
| `Data.issues` | array | 质控问题列表 |
| `Data.summary` | string | 质控结论摘要 |
| `Data.processTimeMs` | int | 处理耗时（毫秒） |

### 质控等级判定规则

| 条件 | 等级 | 通过 |
|------|------|:----:|
| 总分 ≥ 90 且无 critical 问题 | 高级质控 | ✅ |
| 总分 60-89 且无 critical 问题 | 中级质控 | ❌ |
| 总分 < 60 或存在 critical 问题 | 初级质控 | ❌ |

### 问题严重级别

| 级别 | 说明 | 扣分 |
|------|------|:----:|
| `critical` | 严重问题，直接不合格 | 每项 -25 分 |
| `error` | 错误 | 每项 -10 分 |
| `warning` | 警告/建议 | 每项 -5 分 |

---

## 各层级检出问题类型

| 层级 | IssueType | 说明 | 默认严重度 |
|:----:|-----------|------|:----------:|
| L1 | `text_error` | 错别字、漏字、重复字、标点缺失 | warning |
| L2 | `terminology_error` | 术语不规范、口语化描述 | warning |
| L2 | `rads_missing` | RADS 分类缺失 | error |
| L2 | `semantic_conflict` | 阴阳性/方位/程度/诊断跳跃 | critical/error |
| L3 | `gender_conflict` | 性别和解剖部位矛盾 | critical |
| L3 | `age_conflict` | 年龄和诊断矛盾 | error |
| L3 | `direction_conflict` | 方位描述矛盾（L2 也有检出） | error |
| L3 | `unit_error` | 尺寸单位格式错误 | warning |
| L4 | `scan_enhance_conflict` | 平扫描述出现强化 | error |
| L4 | `device_conflict` | 设备类型描述冲突 | error |
| L4 | `missing_measurement` | 病灶关键测量缺失 | warning |
| L4 | `critical_sign` | 危急征象提示 | critical |

---

## curl 调用示例

### 基本调用

```bash
curl -X POST "http://localhost:5100/api/v1/qc/report" \
  -H "Content-Type: application/json" \
  -d '{
    "reportId": "TEST-001",
    "findings": "双肺纹理清晰，未见明确肿块影。",
    "impression": "右肺上叶可见结节影，建议随访。",
    "reportType": "CT",
    "patientGender": "男",
    "patientAge": 55
  }'
```

### 完整信息调用

```bash
curl -X POST "http://localhost:5100/api/v1/qc/report" \
  -H "Content-Type: application/json" \
  -d @qc_payload.json
```

其中 `qc_payload.json` 为包含完整报告信息的文件。

---

## 其他相关 API

| 方法 | 路由 | 说明 |
|------|------|------|
| `GET` | `/api/v1/qc/reports` | 历史质控列表（支持分页/筛选） |
| `GET` | `/api/v1/qc/reports/{id}` | 质控记录详情 |
| `GET` | `/api/v1/qc/reports/stats` | 质控统计概览 |
| `POST` | `/api/v1/qc/feedback` | 提交医生反馈 |
| `GET` | `/api/v1/logs` | 系统日志查询 |
| `POST` | `/api/v1/knowledge-base` | 新增质控规则 |
| `POST` | `/api/v1/knowledge-base/reload` | 热更新规则缓存 |

---

> **版本**：V1.0 | **更新日期**：2026-05-07
