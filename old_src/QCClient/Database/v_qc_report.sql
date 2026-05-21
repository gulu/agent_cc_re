-- v_qc_report.sql — 质控报告查询视图（Oracle）
-- 说明：由医院 DBA 在目标 Oracle 数据库中创建
-- 视图字段必须与后端质控 API 请求参数严格一致
-- 版本：V1.0 | 日期：2026-05-19
-- 
-- 注意事项：
-- 1. 替换下面的表名 RIS_EXAM_MASTER、RIS_REPORT 等为医院实际表名
-- 2. 字段映射关系应根据医院实际数据库结构调整
-- 3. 创建前请确认字段数据类型和长度
-- 4. 创建后需授予 QCClient 后端服务账号查询权限


CREATE OR REPLACE VIEW VIEW_QC_REPORT AS
SELECT
-- 检查流水号 / 影像号（查询条件）
 e.caccno AS ACCESS_NUMBER,

 -- 患者信息
 e.cblkh     AS PATIENT_ID,
 e.cname     AS PATIENT_NAME,
 e.csex      AS PATIENT_SEX,
 e.cage      AS PATIENT_AGE,
 r.dbirthday AS PATIENT_BIRTHDAY,

 -- 检查信息
 e.cmodality  AS EXAM_TYPE,
 e.cbz        AS EXAM_BODY_PART,
 e.dcheckdate AS EXAM_DATE,
 e.dcheckdate AS EXAM_TIME,

 -- 临床信息
 e.clczd AS CLINICAL_DIAGNOSIS,
 e.clcsj AS CLINICAL_HISTORY,

 -- 报告内容
 bg.cbgsj_hl7 AS REPORT_CONTENT,
 bg.cbgzd     AS REPORT_DIAGNOSIS,

 -- 报告信息
 bg.cbgysxm AS REPORT_DOCTOR,
 bg.dbgsj   AS REPORT_DATE,
 bg.cbgzt   AS REPORT_STATUS,
 bg.cshysxm AS AUDIT_DOCTOR,
 bg.dshsj   AS AUDIT_DATE,

 -- 申请信息
 e.csqks AS DEPARTMENT,
 e.cch   AS BED_NO,
 e.czyh  AS INPATIENT_NO,
 e.cblkh AS OUTPATIENT_NO,
 e.ddjsj AS APPLICATION_DATE,
 e.cbrlx AS PATIENT_TYPE

  FROM icnris_EXAM e, icnris_exam_bg bg, icnris_register r
 where e.iid = r.iid(+)
   and e.iid = bg.iexam_iid(+)
   and bg.cflag(+) = 9;


COMMENT ON TABLE v_qc_report IS '质控报告查询视图 - 用于 QCClient 报告质控数据查询';
COMMENT ON COLUMN v_qc_report.ACCESS_NUMBER IS '检查流水号/影像号（查询条件）';
COMMENT ON COLUMN v_qc_report.PATIENT_ID IS '患者 ID';
COMMENT ON COLUMN v_qc_report.PATIENT_NAME IS '患者姓名';
COMMENT ON COLUMN v_qc_report.PATIENT_SEX IS '患者性别：男/女';
COMMENT ON COLUMN v_qc_report.PATIENT_AGE IS '患者年龄';
COMMENT ON COLUMN v_qc_report.PATIENT_BIRTHDAY IS '患者出生日期';
COMMENT ON COLUMN v_qc_report.EXAM_TYPE IS '检查类型：CT/MR/DR/DSA';
COMMENT ON COLUMN v_qc_report.EXAM_BODY_PART IS '检查部位：胸部/头颅/腹部';
COMMENT ON COLUMN v_qc_report.EXAM_DATE IS '检查日期';
COMMENT ON COLUMN v_qc_report.EXAM_TIME IS '检查时间';
COMMENT ON COLUMN v_qc_report.CLINICAL_DIAGNOSIS IS '临床诊断';
COMMENT ON COLUMN v_qc_report.CLINICAL_HISTORY IS '临床病史';
COMMENT ON COLUMN v_qc_report.REPORT_CONTENT IS '报告所见（影像所见）';
COMMENT ON COLUMN v_qc_report.REPORT_DIAGNOSIS IS '影像诊断';
COMMENT ON COLUMN v_qc_report.REPORT_DOCTOR IS '报告医生';
COMMENT ON COLUMN v_qc_report.REPORT_DATE IS '报告书写日期';
COMMENT ON COLUMN v_qc_report.REPORT_STATUS IS '报告状态：草稿/已提交/已审核';
COMMENT ON COLUMN v_qc_report.AUDIT_DOCTOR IS '审核医生（可选）';
COMMENT ON COLUMN v_qc_report.AUDIT_DATE IS '审核日期（可选）';
COMMENT ON COLUMN v_qc_report.DEPARTMENT IS '申请科室';
COMMENT ON COLUMN v_qc_report.BED_NO IS '床号（可选）';
COMMENT ON COLUMN v_qc_report.INPATIENT_NO IS '住院号（可选）';
COMMENT ON COLUMN v_qc_report.OUTPATIENT_NO IS '门诊号（可选）';
COMMENT ON COLUMN v_qc_report.APPLICATION_DATE IS '开单时间';
COMMENT ON COLUMN v_qc_report.PATIENT_TYPE IS '患者类型：1门诊 2住院 3急诊 4其他';
