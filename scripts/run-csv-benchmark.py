#!/usr/bin/env python3
"""
Agent_QC CSV 基线测试脚本
读取 src/报告测试数据.csv 前 N 条报告，统计规则命中分布。
用法: python3 scripts/run-csv-benchmark.py [行数] [CSV路径]
"""
import csv
import sys
import json
from collections import Counter

CSV_PATH = "src/报告测试数据.csv"

# ── 规则命中模拟（基于 CSV 字段直接检测） ──

FEMALE_ONLY_TERMS = ["子宫", "卵巢", "输卵管", "宫颈", "乳腺", "阴道", "子宫内膜", "月经"]
MALE_ONLY_TERMS = ["前列腺", "睾丸", "阴茎", "阴囊", "精囊", "附睾"]
LEFT_TERMS = ["左侧", "左叶", "左侧壁", "左肺", "左肾", "左上", "左下", "左前", "左后"]
RIGHT_TERMS = ["右侧", "右叶", "右侧壁", "右肺", "右肾", "右上", "右下", "右前", "右后"]
CRITICAL_SIGNS = ["主动脉夹层", "脑出血", "颅内出血", "硬膜外血肿", "硬膜下血肿",
                  "蛛网膜下腔出血", "脑疝", "急性肺栓塞", "急性心梗", "心肌梗死",
                  "肝破裂", "脾破裂", "消化道穿孔", "肠穿孔", "胃穿孔", "活动性出血"]

AGE_RULES = {
    "骨质疏松": (0, 20), "骨质增生": (0, 18), "脑梗灶": (0, 12),
    "前列腺增生": (0, 30), "恶性肿瘤": (0, 2), "肝硬化": (0, 10),
    "颈椎病": (0, 18), "腰椎间盘突出": (0, 15), "动脉粥样硬化": (0, 15),
}

ENHANCE_WORDS = ["强化", "增强", "明显强化", "明显增强", "不均匀强化",
                 "环形强化", "快进快出", "快进慢出"]

DEVICE_BANNED = {
    "CT": ["MRI示", "MR示", "磁共振示", "T1WI", "T2WI", "DWI", "FLAIR"],
    "MRI": ["CT示", "CT平扫", "CT增强", "X线片", "X光片"],
    "DR": ["CT示", "MRI示", "MR示", "CT增强"],
    "超声": ["CT示", "MRI示", "MR示", "X线片", "CT增强"],
}

PHRASE_TYPOS = ["低密谋灶", "轨道症", "十二脂肠", "肾孟", "骨拆", "破列", "积夜", "颅内血钟"]
COLLOQUIAL_TERMS = ["看起来", "好像", "两边", "比较大", "这个", "那个", "感觉"]
NONSTANDARD_TERMS = ["右上肺", "头颅", "脊椎", "胆部", "血管内部"]


def check_report(row):
    """对单条报告执行所有规则检测，返回命中的 issue_type 列表。"""
    findings = row.get("FINDINGS", "") or ""
    impression = row.get("IMPRESSION", "") or ""
    gender = row.get("PATIENTGENDER", "") or ""
    age_str = row.get("PATIENTAGE", "") or ""
    exam_method = row.get("EXAMMETHOD", "") or ""
    exam_device = row.get("EXAMDEVICE", "") or ""
    report_type = row.get("REPORTTYPE", "") or ""

    full_text = findings + impression
    age = int(float(age_str)) if age_str else None
    hits = []

    # Gender conflict
    if gender == "男":
        for t in FEMALE_ONLY_TERMS:
            if t in full_text:
                hits.append("gender_conflict")
                break
    elif gender == "女":
        for t in MALE_ONLY_TERMS:
            if t in full_text:
                hits.append("gender_conflict")
                break

    # Age conflict
    if age is not None:
        for keyword, (lo, hi) in AGE_RULES.items():
            if keyword in full_text and lo <= age <= hi:
                hits.append("age_conflict")
                break

    # Direction conflict
    f_left = any(t in findings for t in LEFT_TERMS)
    f_right = any(t in findings for t in RIGHT_TERMS)
    i_left = any(t in impression for t in LEFT_TERMS)
    i_right = any(t in impression for t in RIGHT_TERMS)
    if (f_left and i_right and not i_left) or (f_right and i_left and not i_right):
        hits.append("direction_conflict")

    # Critical signs
    for s in CRITICAL_SIGNS:
        if s in full_text:
            hits.append("critical_sign")
            break

    # Scan-enhance conflict
    if "平扫" in (exam_method or ""):
        for w in ENHANCE_WORDS:
            if w in full_text:
                hits.append("scan_enhance_conflict")
                break

    # Device conflict
    for dev, banned in DEVICE_BANNED.items():
        if dev in (exam_device or ""):
            for t in banned:
                if t in full_text:
                    hits.append("device_conflict")
                    break
            break

    # Text errors
    for t in PHRASE_TYPOS:
        if t in full_text:
            hits.append("text_error")
            break
    for t in COLLOQUIAL_TERMS:
        if t in full_text:
            hits.append("colloquial")
            break
    for t in NONSTANDARD_TERMS:
        if t in full_text:
            hits.append("terminology_nonstandard")
            break

    return hits


def main():
    limit = int(sys.argv[1]) if len(sys.argv) > 1 else 200
    csv_path = sys.argv[2] if len(sys.argv) > 2 else CSV_PATH

    print(f"# Agent_QC CSV 基线测试")
    print(f"# CSV: {csv_path}, 最大行数: {limit}")
    print()

    rows = []
    # 尝试多种编码（医院系统通常导出为 GBK/GB18030）
    encoding = "utf-8"
    for enc in ["utf-8", "gb18030", "gbk", "gb2312"]:
        try:
            with open(csv_path, encoding=enc) as test:
                test.read(1024)
            encoding = enc
            break
        except (UnicodeDecodeError, UnicodeError):
            continue

    with open(csv_path, encoding=encoding) as f:
        reader = csv.DictReader(f)
        for i, row in enumerate(reader):
            if i >= limit:
                break
            if not row.get("FINDINGS", "").strip() and not row.get("IMPRESSION", "").strip():
                continue
            rows.append(row)

    print(f"总报告数: {len(rows)}")

    # 统计各规则命中
    hit_counter = Counter()
    per_report = []
    for row in rows:
        hits = check_report(row)
        hit_counter.update(hits)
        per_report.append(hits)

    print()
    print("## 规则命中统计")
    for rule, count in hit_counter.most_common():
        pct = count / len(rows) * 100
        print(f"  {rule}: {count} ({pct:.1f}%)")

    no_issues = sum(1 for h in per_report if not h)
    print(f"  无问题报告: {no_issues} ({no_issues/len(rows)*100:.1f}%)")

    # 每报告问题数分布
    count_dist = Counter(len(h) for h in per_report)
    print()
    print("## 每报告问题数分布")
    for cnt in sorted(count_dist):
        pct = count_dist[cnt] / len(rows) * 100
        print(f"  {cnt}个问题: {count_dist[cnt]}份 ({pct:.1f}%)")

    # 常见误报关键词
    print()
    print("## 命中率最高的规则 (top 5)")
    for rule, count in hit_counter.most_common(5):
        pct = count / len(rows) * 100
        flag = " ⚠ 可能误报率偏高" if pct > 30 else ""
        print(f"  {rule}: {pct:.1f}%{flag}")

    print()
    print("完成。")


if __name__ == "__main__":
    main()
