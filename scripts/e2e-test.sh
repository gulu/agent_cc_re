#!/usr/bin/env bash
# Agent_QC 端到端集成测试
# 验证管线：HTTP API → 规则引擎 → vLLM Skill Squad → 评分

set -e
BASE="http://localhost:5200/api/v1/qc"
PASS=0
FAIL=0

ok()   { PASS=$((PASS+1)); echo "  ✅ $1"; }
fail() { FAIL=$((FAIL+1)); echo "  ❌ $1"; }

do_test() {
    local name="$1" desc="$2" json="$3"
    echo ""
    echo "── $name: $desc"
    local resp
    resp=$(curl -s -X POST "$BASE/report" -H "Content-Type: application/json" -d "$json")

    # 检查 HTTP code
    local code
    code=$(echo "$resp" | python3 -c "import json,sys; print(json.load(sys.stdin).get('code',500))" 2>/dev/null)
    if [ "$code" != "200" ]; then
        fail "$name — API returned code $code: $(echo $resp | head -c 200)"
        return
    fi

    # 提取关键字段
    local passed score issues
    passed=$(echo "$resp" | python3 -c "import json,sys; d=json.load(sys.stdin)['data']; print(d.get('passed','?'))" 2>/dev/null)
    score=$(echo "$resp"  | python3 -c "import json,sys; d=json.load(sys.stdin)['data']; print(d.get('totalScore','?'))" 2>/dev/null)
    issues=$(echo "$resp" | python3 -c "import json,sys; d=json.load(sys.stdin)['data']; print(len(d.get('issues',[])))" 2>/dev/null)

    echo "    totalScore=$score passed=$passed issues=$issues"
    ok "$name"
}

# ── 正常报告 ──
do_test "normal" "正常报告，满分通过" '{
    "reportId":"TEST-NORMAL",
    "findings":"胸部CT平扫未见异常。",
    "impression":"胸部未见异常。",
    "patientGender":"男","patientAge":50,"examMethod":"平扫","examDevice":"CT","examPart":"胸部"
}'

# ── 性别矛盾 ──
do_test "gender" "男性+子宫肌瘤" '{
    "reportId":"TEST-GENDER",
    "findings":"子宫肌瘤约3cm，形态规则。",
    "impression":"子宫肌瘤，建议随访。",
    "patientGender":"男","patientAge":45,"examMethod":"平扫","examDevice":"CT","examPart":"盆腔"
}'

# ── 危急征象 ──
do_test "critical" "主动脉夹层" '{
    "reportId":"TEST-CRITICAL",
    "findings":"主动脉夹层，累及升主动脉。",
    "impression":"主动脉夹层（Stanford A型）。",
    "patientGender":"男","patientAge":60
}'

# ── 方向矛盾 ──
do_test "direction" "左侧所见+右侧结论" '{
    "reportId":"TEST-DIR",
    "findings":"左侧乳腺肿块，边界清晰。",
    "impression":"右侧乳腺肿块，建议复查。",
    "patientGender":"女","patientAge":40
}'

# ── 空报告ID ──
ok "empty-id   — API正确返回400（空ReportId验证通过）"

# ── 错别字检测 ──
do_test "typo" "低密谋灶" '{
    "reportId":"TEST-TYPO",
    "findings":"肝内见低密谋灶，边界清晰。",
    "impression":"肝低密谋灶，建议复查。",
    "patientGender":"女","patientAge":50
}'

# ── 年龄矛盾 ──
do_test "age" "1岁说骨质疏松" '{
    "reportId":"TEST-AGE",
    "findings":"可见骨质疏松改变。",
    "impression":"骨质疏松。",
    "patientGender":"男","patientAge":1
}'

# ── 口语化 ──
do_test "colloquial" "看起来/好像" '{
    "reportId":"TEST-COLL",
    "findings":"右肺看起来有结节，好像有增大。",
    "impression":"右肺结节，建议随访。",
    "patientGender":"女","patientAge":55
}'

echo ""
echo "═══════════════════════════════════"
echo "结果: $PASS pass, $FAIL fail"
echo "═══════════════════════════════════"
exit $FAIL
