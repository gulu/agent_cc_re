/* ═══════════════════════════════════════════════════════
   报告质控助手 — 前端交互
   ═══════════════════════════════════════════════════════ */

let lastResult = null;
let loadingTimer = 0;

// 简写
const $ = id => document.getElementById(id);

/* ── SSE ── */
(function connectSse() {
  const es = new EventSource('/api/sse');

  es.addEventListener('connected', () => setDot('connected'));

  es.addEventListener('connection', (e) => {
    try {
      const raw = JSON.parse(e.data);
      const d = (typeof raw.data === 'string' ? JSON.parse(raw.data) : raw.data) || raw;
      const online = d.online;
      setDot(online ? 'online' : 'offline');
      $('statusDot').title = online ? 'Agent_QC 已连接' : 'Agent_QC 离线';
    } catch (_) {}
  });

  es.addEventListener('qc_result', (e) => {
    try {
      const raw = JSON.parse(e.data);
      const data = raw.data || raw;
      if (data && data.totalScore !== undefined) {
        renderResult(data);
        toast('质控完成');
      }
    } catch (_) {}
  });

  es.addEventListener('status', (e) => {
    try {
      const raw = JSON.parse(e.data);
      const info = raw.data || raw;
      $('statusTip').textContent = info.message || info.status || '';
      const st = info.status;
      if (st === 'analyzing') showLoading(info.message);
      else if (st === 'idle' || st === 'querying' || st === 'ocr') showLoading(info.message);
      else hideLoading();
    } catch (_) {}
  });

  es.onerror = () => { setDot('connecting'); setTimeout(connectSse, 3000); };
})();

function setDot(state) {
  const color = { online: '#4ade80', offline: '#f87171', connected: '#60a5fa', connecting: '#94a3b8' };
  $('statusDot').style.color = color[state] || color.connecting;
}

/* ── 加载状态 ── */
function showLoading(msg) {
  $('loading').style.display = 'block';
  $('loadingText').textContent = msg || '分析中...';
  $('emptyState').style.display = 'none';
  clearTimeout(loadingTimer);
  loadingTimer = setTimeout(hideLoading, 30000);
}
function hideLoading() {
  $('loading').style.display = 'none';
  clearTimeout(loadingTimer);
}

/* ── 手动输入 ── */
function toggleManual() {
  const row = $('manualRow');
  const tip = document.querySelector('#manualToggle');
  const show = row.style.display !== 'flex';
  row.style.display = show ? 'flex' : 'none';
  if (tip) tip.textContent = show ? '− 收起手动输入' : '+ 手动输入报告内容';
}

/* ── 触发质控 ── */
async function triggerQc() {
  const an = $('accessNumber').value.trim();
  if (!an) { toast('请输入影像号'); return; }
  showLoading('正在质控...');
  try {
    const r = await fetch('/api/qc', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ accessNumber: an })
    });
    const d = await r.json();
    if (d.code === 0 && d.data) renderResult(d.data);
    else toast('质控失败：' + (d.msg || '后端未响应'));
  } catch (e) { toast('请求失败：' + e.message); }
  hideLoading();
}

async function manualQc() {
  const f = $('manualFindings').value.trim();
  const i = $('manualImpression').value.trim();
  if (!f && !i) { toast('请输入报告内容'); return; }
  showLoading('正在质控...');
  try {
    const r = await fetch('/api/qc/manual', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ findings: f, impression: i })
    });
    const d = await r.json();
    if (d.code === 0 && d.data) renderResult(d.data);
    else toast('质控失败：' + (d.msg || ''));
  } catch (e) { toast('请求失败：' + e.message); }
  hideLoading();
}

function reQc() {
  const an = $('accessNumber').value.trim();
  if (an) triggerQc();
  else toast('请输入影像号');
}

/* ── 收起面板 ── */
$('collapseBtn').addEventListener('click', () => {
  window.location.href = window.location.search ? '#collapse' : '?#collapse';
});

/* ── 键盘 ── */
$('accessNumber').addEventListener('keydown', e => {
  if (e.key === 'Enter') triggerQc();
});

/* ══════════════════════════════════════════
   结果渲染
   ══════════════════════════════════════════ */

function renderResult(result) {
  if (!result) return;
  lastResult = result;
  hideLoading();
  $('emptyState').style.display = 'none';

  // 统一驼峰化
  const d = camelize(result);

  const score = Math.round(d.totalScore || 0);
  const passed = d.passed;

  /* ── 评分环 ── */
  $('scoreBlock').style.display = 'block';
  $('scoreNum').textContent = score;
  const ring = $('scoreRing');
  ring.className = 'score-ring ' + (score >= 90 ? 'pass' : score >= 70 ? 'warn' : 'fail');
  $('scoreVerdict').textContent = passed ? '✅ 质控通过' : '❌ 质控未通过';
  $('scoreVerdict').style.color = passed ? 'var(--success)' : 'var(--danger)';
  $('scoreDetail').textContent =
    (d.qcLevel ? d.qcLevel + ' · ' : '') +
    (d.processTimeMs != null ? '耗时 ' + d.processTimeMs + 'ms' : '');

  /* ── 维度条 ── */
  if (d.checkItems && d.checkItems.length) {
    $('dimsBlock').style.display = 'block';
    $('dimList').innerHTML = d.checkItems.map(x => {
      const v = Math.round(x.score || 0);
      const color = v >= 90 ? '#22c55e' : v >= 70 ? '#f59e0b' : '#ef4444';
      return `<div class="dim-row">
        <span class="dim-label">${esc(x.dimensionName || '')}</span>
        <div class="dim-track"><div class="dim-fill" style="width:${v}%;background:${color}"></div></div>
        <span class="dim-val" style="color:${color}">${v}</span>
      </div>`;
    }).join('');
  } else { $('dimsBlock').style.display = 'none'; }

  /* ── 问题分组 ── */
  const issues = d.issues || [];
  if (issues.length) {
    $('issuesBlock').style.display = 'block';
    $('issuesTotal').textContent = `（${issues.length} 项）`;

    // 按严重度分组
    const groups = { critical: [], error: [], warning: [], info: [] };
    issues.forEach(i => {
      const sev = (i.severity || 'warning').toLowerCase();
      (groups[sev] || groups.warning).push(i);
    });

    const sevMeta = {
      critical: { label: '🔴 危急', cls: 'critical' },
      error:    { label: '🟠 错误', cls: 'error' },
      warning:  { label: '🟡 警告', cls: 'warning' },
      info:     { label: '💡 提示', cls: 'warning' },
    };

    let html = '';
    for (const [sev, items] of Object.entries(groups)) {
      if (!items.length) continue;
      const meta = sevMeta[sev];
      html += `<div class="issue-group ${meta.cls}">
        <div class="issue-group-header" onclick="this.parentElement.classList.toggle('collapsed')">
          <span class="arrow">▼</span> ${meta.label}
          <span class="count">${items.length} 项</span>
        </div>
        <div class="issue-group-body">`;

      items.forEach(x => {
        const sub = x.subType ? `<span class="card-type" style="background:var(--${meta.cls === 'critical' ? 'danger' : meta.cls === 'error' ? 'warn' : 'info'}-bg);color:var(--${meta.cls === 'critical' ? 'danger' : meta.cls === 'error' ? 'warn' : 'info'})">${esc(x.subType)}</span>` : '';
        html += `<div class="issue-card">
          ${sub}
          <div class="card-desc">${esc(x.description || x.message || x.issueType || '')}</div>
          ${x.originalText ? `<div class="card-original">原文：${esc(x.originalText)}</div>` : ''}
          ${x.suggestion ? `<div class="card-fix">💡 ${esc(x.suggestion)}</div>` : ''}
          ${x.suggestedText ? `<div class="card-fix">✏️ 建议改为：${esc(x.suggestedText)}</div>` : ''}
        </div>`;
      });

      html += `</div></div>`;
    }
    $('issueGroups').innerHTML = html;
  } else {
    $('issuesBlock').style.display = 'none';
  }

  /* ── 摘要 ── */
  if (d.summary) {
    $('summaryBlock').style.display = 'block';
    $('summaryText').textContent = d.summary;
  } else { $('summaryBlock').style.display = 'none'; }
}

/* ── 复制（临床可用格式）── */
function copyForClinical() {
  if (!lastResult) { toast('暂无质控结果'); return; }
  const d = camelize(lastResult);
  const score = Math.round(d.totalScore || 0);
  const passed = d.passed;

  let text = `═══════════════════════════════\n`;
  text += `  报告质控结果\n`;
  text += `  评分：${score} 分  ${passed ? '✅ 通过' : '❌ 未通过'}\n`;
  text += `═══════════════════════════════\n`;

  if (d.checkItems && d.checkItems.length) {
    text += `\n【维度评分】\n`;
    d.checkItems.forEach(x => {
      text += `  ${x.dimensionName || ''}：${Math.round(x.score || 0)} 分  (权重 ${Math.round((x.weight || 0) * 100)}%)\n`;
    });
  }

  if (d.issues && d.issues.length) {
    text += `\n【质控问题】共 ${d.issues.length} 项\n`;
    const sevOrder = { critical: 0, error: 1, warning: 2, info: 3 };
    const sorted = [...d.issues].sort((a, b) =>
      (sevOrder[(a.severity || 'warning').toLowerCase()] || 9) -
      (sevOrder[(b.severity || 'warning').toLowerCase()] || 9));

    sorted.forEach((x, i) => {
      const sevLabel = { critical: '危急', error: '错误', warning: '警告' };
      const sev = sevLabel[(x.severity || 'warning').toLowerCase()] || '提示';
      text += `\n  ${i + 1}. [${sev}] ${x.description || x.issueType || ''}\n`;
      if (x.originalText) text += `     原文：${x.originalText}\n`;
      if (x.suggestion) text += `     建议：${x.suggestion}\n`;
      if (x.suggestedText) text += `     修改：${x.suggestedText}\n`;
    });
  }

  if (d.summary) {
    text += `\n【综合建议】\n${d.summary}\n`;
  }

  navigator.clipboard.writeText(text).then(
    () => toast('已复制到剪贴板，可直接粘贴至报告系统'),
    () => toast('复制失败，请手动选择')
  );
}

/* ── 工具 ── */
function toast(msg) {
  const t = $('toast');
  t.textContent = msg;
  t.classList.add('show');
  clearTimeout(t._t);
  t._t = setTimeout(() => t.classList.remove('show'), 2500);
}

function esc(s) {
  const d = document.createElement('div');
  d.textContent = s || '';
  return d.innerHTML;
}

// 递归驼峰化对象键名
function camelize(obj) {
  if (!obj || typeof obj !== 'object') return obj;
  if (Array.isArray(obj)) return obj.map(camelize);
  const out = {};
  for (const k in obj) {
    const ck = k[0].toLowerCase() + k.slice(1);
    const v = obj[k];
    out[ck] = (v && typeof v === 'object' && !Array.isArray(v)) ? camelize(v)
      : (Array.isArray(v) ? v.map(camelize) : v);
  }
  return out;
}

/* ── 启动 ── */
(function init() {
  // 主题
  const params = new URLSearchParams(window.location.search);
  document.body.className = params.get('theme') || 'light';

  // 定时状态轮询
  setInterval(async () => {
    try {
      const r = await fetch('/api/status');
      const d = await r.json();
      if (d.code === 0 && d.data) {
        const online = d.data.online;
        setDot(online ? 'online' : 'offline');
        $('statusDot').title = online ? 'Agent_QC 已连接' : 'Agent_QC 离线';
      }
    } catch (_) { setDot('offline'); }
  }, 15000);
})();
