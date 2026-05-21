// ── 报告质控系统 — 前端逻辑 ──

let eventSource = null;
let currentResult = null;

document.addEventListener('DOMContentLoaded', () => {
  // 建立 SSE 连接
  connectSSE();

  // 检查连接状态
  fetchStatus();

  // 定期刷新
  setInterval(fetchStatus, 30000);
});

// ── SSE 实时推送 ──
function connectSSE() {
  if (eventSource) eventSource.close();

  eventSource = new EventSource('/api/sse');

  eventSource.onmessage = (e) => {
    try {
      const msg = JSON.parse(e.data);
      handleEvent(msg);
    } catch (err) {
      console.warn('SSE parse error:', err);
    }
  };

  eventSource.onerror = () => {
    console.warn('SSE 连接断开，5秒后重连...');
    setTimeout(connectSSE, 5000);
  };
}

// ── 处理 SSE 事件 ──
function handleEvent(msg) {
  const { type, data } = msg;

  switch (type) {
    case 'connection':
      updateConnectionStatus(data);
      break;
    case 'processing':
      showLoading(true);
      break;
    case 'result':
      showLoading(false);
      renderResult(data);
      break;
    case 'error':
      showLoading(false);
      showToast(data?.message || '处理异常', 'error');
      break;
  }
}

// ── 更新连接状态 ──
function updateConnectionStatus(data) {
  const dot = document.querySelector('#connStatus .dot');
  const text = document.querySelector('#connStatus');
  const svcQc = document.getElementById('svcQc');
  const svcRpt = document.getElementById('svcRpt');

  if (data.connected) {
    dot.className = 'dot online';
    text.innerHTML = '<span class="dot online"></span>已连接';
    if (svcQc) {
      svcQc.textContent = data.qcService ? '✅ 在线' : '❌ 离线';
      svcQc.className = 'conn-value ' + (data.qcService ? 'online' : 'offline');
      svcRpt.textContent = data.reportQc ? '✅ 在线' : '❌ 离线';
      svcRpt.className = 'conn-value ' + (data.reportQc ? 'online' : 'offline');
    }
  } else {
    dot.className = 'dot offline';
    text.innerHTML = '<span class="dot offline"></span>断开';
    if (svcQc) {
      svcQc.textContent = '❌ 离线'; svcQc.className = 'conn-value offline';
      svcRpt.textContent = '❌ 离线'; svcRpt.className = 'conn-value offline';
    }
  }
}

// ── 获取状态 ──
async function fetchStatus() {
  try {
    const res = await fetch('/api/status');
    const data = await res.json();

    if (data.connected) {
      document.querySelector('#connStatus').innerHTML = '<span class="dot online"></span>已连接';
    } else {
      document.querySelector('#connStatus').innerHTML = '<span class="dot idle"></span>离线';
    }

    // 更新刚进页面的空状态
    const svcQc = document.getElementById('svcQc');
    if (svcQc && data.connected !== undefined) {
      // 连接详情会在SSE中更新
    }
  } catch (err) {
    console.warn('状态查询失败');
  }
}

// ── 显示/隐藏加载 ──
function showLoading(show) {
  const loading = document.getElementById('loading');
  const btn = document.getElementById('qcBtn');
  loading.style.display = show ? 'flex' : 'none';
  btn.disabled = show;
}

// ── 触发质控 ──
async function triggerQC() {
  const accessNumber = document.getElementById('accessNumber').value.trim();
  if (!accessNumber) {
    showToast('请输入影像号', 'error');
    return;
  }

  showLoading(true);

  try {
    const res = await fetch('/api/qc', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ accessNumber })
    });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    showToast('质控已触发', 'info');
  } catch (err) {
    showLoading(false);
    showToast('触发失败: ' + err.message, 'error');
  }
}

// ── 渲染质控结果 ──
function renderResult(data) {
  // 如果是 SSE 的 result 事件，data 包含完整结果
  // 如果是单独推送的，取 data 内部数据
  const d = data?.totalScore !== undefined ? data : data?.data;
  if (!d || d.totalScore === undefined) return;

  currentResult = d;

  // 显示结果区
  document.getElementById('resultArea').style.display = 'block';
  document.getElementById('emptyState').style.display = 'none';
  document.getElementById('reportInfo').style.display = 'block';

  // Score bar
  const scoreClass = d.passed ? 'pass' : 'fail';
  document.getElementById('scoreBar').innerHTML = `
    <div class="score-circle ${scoreClass}">${d.totalScore}</div>
    <div class="score-meta">
      <div class="status-label ${scoreClass}">
        ${d.passed ? '✅ 通过' : '❌ 未通过'}
      </div>
      <div class="info">${d.qcLevel || ''} · 及格线 90 分</div>
      <div class="info">${d.summary || ''}</div>
      <div class="time">⏱ ${d.processTimeMs || 0}ms</div>
    </div>
  `;

  // Dimensions
  const dims = document.getElementById('dimensions');
  if (d.checkItems && d.checkItems.length) {
    dims.innerHTML = d.checkItems.map(c => `
      <div class="dim-item">
        <div class="name">${c.dimensionName || c.DimensionName || ''}</div>
        <div class="score ${c.passed || c.Passed ? 'pass' : 'fail'}">
          ${c.score || c.Score ?? 0}
        </div>
        <div class="weight">权重 ${c.weight || c.Weight ?? 0}%</div>
      </div>
    `).join('');
  }

  // Issues
  const issues = d.issues || [];
  const list = document.getElementById('issuesList');
  const count = document.getElementById('issueCount');

  if (!issues.length) {
    list.innerHTML = '<div class="empty-state">✅ 未发现问题</div>';
    count.textContent = '0 项提示';
    return;
  }

  count.textContent = `${issues.length} 项提示`;
  list.innerHTML = issues.map(i => {
    const sev = (i.severity || i.Severity || 'warning').toLowerCase();
    const sevLabel = sev === 'critical' ? '严重' : sev === 'error' ? '错误' : '警告';
    const loc = (i.location || i.Location || '');
    const desc = i.description || i.Description || '';
    const orig = i.originalText || i.OriginalText || '';
    const sug = i.suggestedText || i.SuggestedText || '';
    const sugg = i.suggestion || i.Suggestion || '';
    return `
      <div class="issue-item severity-${sev}">
        <div>
          <span class="issue-badge ${sev}">${sevLabel}</span>
          ${loc ? `<div style="font-size:10px;color:var(--text-dim);margin-top:3px">${loc}</div>` : ''}
        </div>
        <div class="issue-body">
          <div>${desc}</div>
          ${orig ? `<div class="orig">原文: ${orig}</div>` : ''}
          ${sug ? `<div class="suggest">建议: ${sug}</div>` : ''}
          ${sugg ? `<div class="suggestion">💡 ${sugg}</div>` : ''}
        </div>
      </div>
    `;
  }).join('');
}

// ── Toast ──
function showToast(msg, type) {
  const t = document.getElementById('toast');
  t.textContent = msg;
  t.className = 'toast ' + type + ' show';
  clearTimeout(t._timer);
  t._timer = setTimeout(() => t.classList.remove('show'), 3000);
}
