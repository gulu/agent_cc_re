/* === QCClient 前端交互 === */

// 状态
let lastResult = null;
let currentStatus = 'waiting';

// DOM
const statusDot = document.getElementById('statusDot');
const progressText = document.getElementById('progressText');
const loading = document.getElementById('loading');
const loadingText = document.getElementById('loadingText');
const emptyState = document.getElementById('emptyState');
const accessInput = document.getElementById('accessNumber');
const debugLog = document.getElementById('debugLog');
const debugPanel = document.getElementById('debugPanel');

// === DEBUG 日志 ===
let _dlogLines = [];
let _debugVisible = false;
function dlog(msg) {
  const ts = new Date().toLocaleTimeString();
  _dlogLines.push('[' + ts + '] ' + msg);
  if (_dlogLines.length > 20) _dlogLines.shift();
  if (debugLog && _debugVisible) debugLog.textContent = _dlogLines.join('\n');
}
function setDebugVisible(show) {
  _debugVisible = show;
  if (debugPanel) debugPanel.style.display = show ? 'block' : 'none';
  if (show && debugLog) debugLog.textContent = _dlogLines.join('\n');
}
function toggleDebug() {
  setDebugVisible(!_debugVisible);
  // 同步保存到配置文件
  fetch('/api/config/web', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ShowDebugLog: _debugVisible })
  }).catch(function(){});
}
dlog('script.js 已加载');

// === 主题初始化 ===
(async function initTheme() {
  try {
    const params = new URLSearchParams(window.location.search);
    const themeFromUrl = params.get('theme');
    if (themeFromUrl === 'dark' || themeFromUrl === 'light') {
      document.body.className = themeFromUrl;
    }
    const r = await fetch('/api/config');
    const d = await r.json();
    if (d.code === 0 && d.data) {
      const cfg = d.data;
      if (!themeFromUrl) {
        const theme = (cfg.WebSettings && cfg.WebSettings.Theme) ||
                     (cfg.Web && cfg.Web.Theme) || 'light';
        document.body.className = theme;
      }
      // Debug Log 面板配置
      const showDebug = (cfg.WebSettings && cfg.WebSettings.ShowDebugLog !== undefined)
        ? cfg.WebSettings.ShowDebugLog
        : ((cfg.Web && cfg.Web.ShowDebugLog !== undefined) ? cfg.Web.ShowDebugLog : true);
      setDebugVisible(showDebug);
    }
  } catch (e) { /* keep default */ }
})();

// === SSE ===
function connectSse() {
  dlog('开始 SSE 连接...');
  const es = new EventSource('/api/sse');

  es.addEventListener('connected', (e) => { dlog('SSE connected 事件收到'); updateDot('connected'); });

  es.addEventListener('connection', (e) => {
    try {
      const d = JSON.parse(JSON.parse(e.data).data || e.data);
      const info = d.data || d;
      dlog('SSE connection: qcService=' + info.qcService + ', reportQc=' + info.reportQc);
      if (info.qcService && info.reportQc) updateDot('online');
      else if (!info.qcService && !info.reportQc) updateDot('offline');
      else updateDot('partial');
    } catch { dlog('SSE connection 解析失败'); updateDot('connected'); }
  });

  es.addEventListener('qc_result', (e) => {
    try {
      dlog('SSE qc_result 收到, data长度=' + (e.data ? e.data.length : 0));
      const d = JSON.parse(e.data);
      dlog('解析后 type=' + d.type + ', 有data=' + (!!d.data));
      const obj = d.data || d;
      dlog('结果对象 keys=' + Object.keys(obj).join(','));
      dlog('totalScore=' + obj.totalScore + ', passed=' + obj.passed + ', issues=' + (obj.issues ? obj.issues.length : 0) + ', checkItems=' + (obj.checkItems ? obj.checkItems.length : 0));
      renderResult(obj);
      showToast('质控完成');
    } catch (err) { dlog('ERROR: ' + err.message); console.error('Parse error', err); }
  });

  es.addEventListener('status', (e) => {
    try {
      const d = JSON.parse(e.data);
      const info = d.data || d;
      dlog('SSE status: ' + info.status + ' - ' + info.message);
      updateProgress(info.message);
      if (info.status === 'analyzing') showLoading('分析中...');
      else if (info.status === 'idle' || info.status === 'querying' || info.status === 'ocr') showLoading(info.message);
      else if (info.status === 'completed' || info.status === 'failed' || info.status === 'waiting' || info.status === 'offline') hideLoading();
    } catch { dlog('SSE status 解析失败'); }
  });

  es.onerror = () => { dlog('SSE 连接错误, 3秒后重连'); updateDot('connecting'); setTimeout(connectSse, 3000); };
}

// === 状态 ===
function updateDot(state) {
  const c = { online: '#22c55e', offline: '#ef4444', partial: '#f59e0b', connected: '#4a90d9', connecting: '#94a3b8' };
  statusDot.style.color = c[state] || c.connecting;
}

function updateProgress(t) { progressText.textContent = t; }
function showLoading(t) { loading.style.display = 'block'; loadingText.textContent = t || '分析中...'; emptyState.style.display = 'none'; /* 30秒超时自动隐藏 */ clearTimeout(_loadingTimer); _loadingTimer = setTimeout(function(){ if(loading.style.display!=='none'){ hideLoading(); dlog('Loading 超时自动隐藏'); } }, 30000); }
function hideLoading() { loading.style.display = 'none'; clearTimeout(_loadingTimer); }
var _loadingTimer = 0;

// === 质控 ===
async function triggerQc() {
  const an = accessInput.value.trim();
  if (!an) { showToast('请输入影像号'); return; }
  showLoading('正在质控...');
  dlog('手动触发质控: ' + an);
  try {
    const r = await fetch('/api/qc', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({accessNumber:an}) });
    const d = await r.json();
    dlog('API /api/qc 响应: code=' + d.code + ', 有data=' + (!!d.data));
    if (d.code === 0 && d.data) {
      dlog('响应data keys=' + Object.keys(d.data).join(','));
      dlog('totalScore=' + d.data.totalScore + ', passed=' + d.data.passed);
      renderResult(d.data);
    }
    else showToast('质控失败：' + (d.msg || 'ReportQC 可能未启动'));
    hideLoading();
  } catch (err) { dlog('ERROR triggerQc: ' + err.message); showToast('请求失败：' + err.message); hideLoading(); }
}

async function manualQc() {
  const f = document.getElementById('manualFindings').value.trim();
  const i = document.getElementById('manualImpression').value.trim();
  if (!f && !i) { showToast('请输入报告内容'); return; }
  showLoading('正在质控...');
  try {
    const r = await fetch('/api/qc/manual', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({findings:f, impression:i}) });
    const d = await r.json();
    if (d.code === 0 && d.data) renderResult(d.data);
    else showToast('质控失败：' + (d.msg || '未知错误'));
    hideLoading();
  } catch (err) { showToast('请求失败：' + err.message); hideLoading(); }
}

function reQc() { if (accessInput.value.trim()) triggerQc(); else showToast('请输入影像号'); }

// === 收起面板（通知 C# 层） ===
document.getElementById('collapseBtn').addEventListener('click', () => {
  // 使用 hash 导航触发 SourceChanged 事件（C# 层拦截收起）
  window.location.href = window.location.pathname + window.location.search + '#collapse';
});

// === 结果渲染（兼容 PascalCase / camelCase）===
function renderResult(result) {
  dlog('renderResult 被调用, result类型=' + typeof result + ', result=' + (result ? '有值' : 'null/undefined'));
  if (!result) { dlog('WARN: result is null/undefined!'); return; }

  lastResult = result;
  hideLoading();
  emptyState.style.display = 'none';

  function norm(o) {
    if (!o || typeof o !== 'object') return o;
    if (Array.isArray(o)) return o.map(norm);
    const r = {};
    for (const k in o) { const ck = k[0].toLowerCase() + k.slice(1); r[ck] = (typeof o[k] === 'object' && o[k] !== null && !Array.isArray(o[k])) ? norm(o[k]) : (Array.isArray(o[k]) ? o[k].map(norm) : o[k]); }
    return r;
  }
  const d = norm(result);
  dlog('norm后: totalScore=' + d.totalScore + ', passed=' + d.passed + ', checkItems长度=' + (d.checkItems ? d.checkItems.length : 0) + ', issues长度=' + (d.issues ? d.issues.length : 0));
  const passed = d.passed;
  const score = Math.round(d.totalScore || 0);

  document.getElementById('scoreSection').style.display = 'block';
  document.getElementById('scoreValue').textContent = score;
  const ss = document.getElementById('scoreStatus');
  ss.textContent = passed ? '✅ 通过' : '❌ 未通过';
  ss.style.color = passed ? '#22c55e' : '#ef4444';

  const circle = document.getElementById('scoreCircle');
  circle.style.background = score>=90 ? 'linear-gradient(135deg,#22c55e,#16a34a)' : score>=70 ? 'linear-gradient(135deg,#f59e0b,#d97706)' : 'linear-gradient(135deg,#ef4444,#dc2626)';

  if (d.checkItems && d.checkItems.length) {
    document.getElementById('dimensions').style.display = 'block';
    document.getElementById('dimensionGrid').innerHTML = d.checkItems.map(x =>
      `<div class="dimension-item"><div class="dimension-name">${x.dimensionName||''}</div><div class="dimension-bar"><div class="dimension-fill" style="width:${x.score||0}%;background:${(x.score||0)>=90?'#22c55e':(x.score||0)>=70?'#f59e0b':'#ef4444'}"></div></div><div class="dimension-score">${Math.round(x.score||0)}分</div></div>`
    ).join('');
  }

  if (d.issues && d.issues.length) {
    document.getElementById('issues').style.display = 'block';
    document.getElementById('issueList').innerHTML = d.issues.map(x => {
      const cls = x.severity || 'warning';
      const sev = {critical:'🔴 严重',error:'🟠 错误',warning:'🟡 警告'};
      const msg = x.description || x.message || x.issueType || '';
      return `<div class="issue-item ${cls}"><div class="issue-severity">${sev[cls]||cls}</div><div class="issue-message">${esc(msg)}</div>${x.originalText?'<div class="issue-original">原文：'+esc(x.originalText)+'</div>':''}${x.suggestion?'<div class="issue-suggestion">💡 '+esc(x.suggestion)+'</div>':''}</div>`;
    }).join('');
  }

  if (d.summary) {
    document.getElementById('summary').style.display = 'block';
    document.getElementById('summaryText').textContent = d.summary;
  }
}

// === 复制 ===
function copySuggestions() {
  if (!lastResult) { showToast('暂无质控结果'); return; }
  const d = JSON.parse(JSON.stringify(lastResult));
  const norm = o => { if(!o||typeof o!=='object')return o; if(Array.isArray(o))return o.map(norm); const r={}; for(const k in o){const ck=k[0].toLowerCase()+k.slice(1);r[ck]=typeof o[k]==='object'&&o[k]!==null?!Array.isArray(o[k])?norm(o[k]):o[k].map(norm):o[k];}return r; };
  const r = norm(d);
  let t = `质控评分：${Math.round(r.totalScore||0)}分 ${r.passed?'通过':'未通过'}\n`;
  if (r.issues && r.issues.length) {
    t += '\n存在问题：\n';
    r.issues.forEach((x,i) => {
      t += `${i+1}. ${x.description||x.message||x.issueType||''}\n`;
      if (x.suggestion) t += `   建议：${x.suggestion}\n`;
    });
  }
  if (r.summary) t += `\n修正建议：\n${r.summary}\n`;
  navigator.clipboard.writeText(t).then(() => showToast('已复制'));
}

// === 工具 ===
function showToast(msg) {
  const t = document.getElementById('toast');
  t.textContent = msg; t.classList.add('show');
  setTimeout(() => t.classList.remove('show'), 2500);
}
const esc = s => { const d = document.createElement('div'); d.textContent = s; return d.innerHTML; };

// === 输入 ===
accessInput.addEventListener('keydown', e => { if (e.key === 'Enter') triggerQc(); });

inputSection.addEventListener('click', e => {
  if (e.target.tagName !== 'TEXTAREA' && e.target.tagName !== 'BUTTON' && e.target !== accessInput)
    inputSection.classList.toggle('expanded');
});

// === 启动 ===
connectSse();
setInterval(async () => {
  try {
    const r = await fetch('/api/status');
    const d = await r.json();
    if (d.code === 0) {
      const s = d.data;
      if (s.qcService && s.reportQc) updateDot('online');
      else if (!s.qcService && !s.reportQc) updateDot('offline');
      else updateDot('partial');
    }
  } catch { updateDot('offline'); }
}, 15000);
