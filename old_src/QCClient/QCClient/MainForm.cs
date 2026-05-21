using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace QCClient
{
    /// <summary>
    /// 主浮动面板 — 双模式设计：
    ///   紧凑模式 (默认)：300×36px 原生 WinForms 状态栏，右下角，零打扰
    ///   展开模式：320×屏幕高度 WebView2 完整质控面板
    /// Ctrl+Q 全局热键切换，无标题栏，始终置顶
    /// </summary>
    public class MainForm : Form
    {
        #region Win32 — 全局热键
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_Q = 0x51;
        private const int WM_HOTKEY = 0x0312;
        #endregion

        #region 尺寸常量
        private const int COMPACT_W = 300;
        private const int COMPACT_H = 36;
        private readonly int _expandedW;
        private readonly int _expandedH;

        private bool _isExpanded = false;
        #endregion

        private readonly int _port;
        private readonly QcEngine _engine;
        private readonly ConfigHelper _config;

        // ── 原生紧凑栏控件 ──
        private Panel _titleBar;
        private Label _statusDot;
        private Label _titleLabel;
        private Label _scoreLabel;

        // ── WebView2 ──
        private WebView2 _webView;
        private bool _webViewReady = false;
        private bool _webViewInitStarted = false;
        private bool _allowClose = false;

        public bool IsWebViewReady => _webViewReady;
        public event Action WebViewReady;

        // ── 配色 ──
        private static readonly Color ColorGray    = Color.FromArgb(148, 163, 184);
        private static readonly Color ColorBlue    = Color.FromArgb(74,  144, 217);
        private static readonly Color ColorGreen   = Color.FromArgb(34,  197, 94);
        private static readonly Color ColorRed     = Color.FromArgb(239, 68,  68);
        private static readonly Color BgCompact    = Color.FromArgb(30,  41,  59);
        private static readonly Color BgExpanded   = Color.FromArgb(30,  96,  190);

        public MainForm(int port, QcEngine engine, ConfigHelper config)
        {
            _port = port;
            _engine = engine;
            _config = config;

            var screen = Screen.PrimaryScreen.WorkingArea;
            var cfg = _config.Get();
            _expandedW = Math.Max(cfg.Web.SidebarWidth, 320);
            _expandedH = screen.Height;

            InitializeForm();
            BuildTitleBar();
            BuildWebView();

            _engine.QcCompleted += OnQcResult;
            _engine.ConnectionStatusChanged += OnConnectionChanged;

            // 初始紧凑模式
            SetCompactMode();
        }

        #region 窗体初始化

        private void InitializeForm()
        {
            this.Text = "";
            this.FormBorderStyle = FormBorderStyle.None;
            this.ControlBox = false;
            this.ShowInTaskbar = false;
            this.TopMost = _config.Get().Web.AlwaysOnTop;
            this.StartPosition = FormStartPosition.Manual;
            this.KeyPreview = true;
            this.BackColor = BgCompact;

            this.FormClosing += (s, e) =>
            {
                if (!_allowClose) { e.Cancel = true; SetCompactMode(); }
            };
        }

        #endregion

        #region 原生标题栏（紧凑栏）

        private void BuildTitleBar()
        {
            _titleBar = new Panel
            {
                Height = COMPACT_H,
                Dock = DockStyle.Top,
                BackColor = BgCompact,
                Cursor = Cursors.Hand,
            };

            // 状态圆点
            _statusDot = new Label
            {
                Text = "●",
                Font = new Font("Segoe UI", 8, FontStyle.Regular),
                ForeColor = ColorGray,
                AutoSize = true,
                Location = new Point(12, 10),
                BackColor = Color.Transparent,
            };

            // 标题文字
            _titleLabel = new Label
            {
                Text = "报告质控助手",
                Font = new Font("Microsoft YaHei", 9, FontStyle.Regular),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(28, 8),
                BackColor = Color.Transparent,
            };

            // 评分文字（质控完成后显示）
            _scoreLabel = new Label
            {
                Text = "",
                Font = new Font("Microsoft YaHei", 9, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(150, 8),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
            };

            _titleBar.Controls.Add(_statusDot);
            _titleBar.Controls.Add(_titleLabel);
            _titleBar.Controls.Add(_scoreLabel);

            // 点击紧凑栏 → 展开
            _titleBar.Click += (s, e) =>
            {
                if (!_isExpanded) SetExpandedMode();
            };

            // 双击标题栏 → 切换展开/收起（双向）
            _titleBar.DoubleClick += (s, e) =>
            {
                if (_isExpanded) SetCompactMode(); else SetExpandedMode();
            };

            this.Controls.Add(_titleBar);
        }

        #endregion

        #region WebView2

        private void BuildWebView()
        {
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                Visible = false,
            };

            _webView.CoreWebView2InitializationCompleted += (s, e) =>
            {
                if (e.IsSuccess)
                {
                    dynamic core = _webView.CoreWebView2;
                    core.Settings.AreDevToolsEnabled = false;
                    core.Settings.IsScriptEnabled = true;
                    core.Settings.AreHostObjectsAllowed = false;

                    // SourceChanged 事件：拦截 HTML 收起按钮的 #collapse hash 导航
                    // 使用 WebView2 控件自带的 SourceChanged（WinForms 事件），避免反射类型转换
                    _webView.SourceChanged += (src, srcArgs) =>
                    {
                        string url = _webView.Source?.OriginalString ?? "";
                        if (url.Contains("#collapse"))
                        {
                            // 恢复原 URL 并切换为紧凑模式
                            string restoreUrl = url.Replace("#collapse", "");
                            if (restoreUrl != url)
                            {
                                _webView.Source = new Uri(restoreUrl);
                            }
                            this.BeginInvoke(new Action(SetCompactMode));
                        }
                    };

                    _webViewReady = true;
                    WebViewReady?.Invoke();
                    Logger.Info("WebView2 initialized at http://127.0.0.1:" + _port + "/index.html");
                }
                else
                {
                    Logger.Error("WebView2 init failed: " + e.InitializationException?.Message);
                }
            };

            this.Controls.Add(_webView);
        }

        private void EnsureWebView2()
        {
            if (_webViewInitStarted) return;
            _webViewInitStarted = true;

            // 首次展开时加载页面（延迟初始化，节省资源）
            string theme = _config.Get().Web.Theme ?? "light";
            _webView.Source = new Uri($"http://127.0.0.1:{_port}/index.html?theme={theme}");
        }

        #endregion

        #region 模式切换

        private void SetCompactMode()
        {
            _isExpanded = false;
            this.Size = new Size(COMPACT_W, COMPACT_H);
            this.BackColor = BgCompact;
            _titleBar.BackColor = BgCompact;
            _titleBar.Height = COMPACT_H;
            _titleBar.Cursor = Cursors.Hand;
            _webView.Visible = false;
            PositionForm();
            this.Show();
        }

        private void SetExpandedMode()
        {
            _isExpanded = true;

            // 确保 WebView2 已初始化
            EnsureWebView2();

            this.Size = new Size(_expandedW, _expandedH);
            this.BackColor = BgExpanded;
            _titleBar.BackColor = BgExpanded;
            _titleBar.Height = 36;
            _titleBar.Cursor = Cursors.Default;
            _webView.Visible = true;
            PositionForm();
            this.Show();
            this.Activate();
        }

        public void ToggleMode()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(ToggleMode));
                return;
            }

            if (_isExpanded)
                SetCompactMode();
            else
                SetExpandedMode();
        }

        #endregion

        #region 公开方法（供 TrayService 调用）

        public void ShowPanel()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(ShowPanel));
                return;
            }

            // 托盘菜单打开 → 直接展开完整面板
            SetExpandedMode();
        }

        public void NavigateTo(string path)
        {
            if (!_webViewReady || _webView?.CoreWebView2 == null) return;

            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(NavigateTo), path);
                return;
            }

            string url = $"http://127.0.0.1:{_port}{path}";
            dynamic core = _webView.CoreWebView2;
            core.Navigate(url);
            Logger.Info("WebView2 navigated to: " + path);
        }

        /// <summary>显示全屏屏幕框选蒙层，返回选中矩形坐标</summary>
        public Rectangle? ShowScreenPicker()
        {
            if (this.InvokeRequired)
                return (Rectangle?)this.Invoke(new Func<Rectangle?>(ShowScreenPicker));

            // 隐藏当前窗口避免遮挡
            bool wasCollapsed = !_webViewReady || !_webView.Visible;
            if (!wasCollapsed) SetCompactMode();

            Rectangle? result = null;
            try
            {
                using (var picker = new ScreenPickerForm())
                {
                    if (picker.ShowDialog(this) == DialogResult.OK)
                        result = picker.SelectedRect;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Screen picker failed");
            }

            // 恢复展开状态
            if (!wasCollapsed)
                SetExpandedMode();

            return result;
        }

        public void CloseForm()
        {
            _allowClose = true;
            if (this.InvokeRequired)
                this.Invoke(new Action(CloseForm));
            else
                this.Close();
        }

        #endregion

        #region 定位

        private void PositionForm()
        {
            var screen = Screen.PrimaryScreen.WorkingArea;
            this.Left = screen.Right - (_isExpanded ? _expandedW : COMPACT_W);
            this.Top = _isExpanded ? screen.Top : screen.Bottom - COMPACT_H;
        }

        #endregion

        #region 状态更新（紧凑栏颜色）

        private void OnQcResult(QcResponse result)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<QcResponse>(OnQcResult), result);
                return;
            }

            _statusDot.ForeColor = result.Passed ? ColorGreen : ColorRed;
            _scoreLabel.Text = $"⭐{result.TotalScore:F0} {(result.Passed ? "✅" : "❌")}";
            _scoreLabel.ForeColor = result.Passed ? ColorGreen : ColorRed;
        }

        private void OnConnectionChanged(bool qcOnline, bool rqcOnline)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<bool, bool>(OnConnectionChanged), qcOnline, rqcOnline);
                return;
            }

            if (qcOnline && rqcOnline)
            {
                _statusDot.ForeColor = ColorBlue;
                _titleLabel.ForeColor = Color.White;
            }
            else
            {
                _statusDot.ForeColor = ColorGray;
                _titleLabel.ForeColor = Color.FromArgb(180, 180, 180);
            }
            _scoreLabel.Text = "";
        }

        #endregion

        #region 全局热键 (Ctrl+Q)

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                ToggleMode();
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            bool ok = RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL, VK_Q);
            if (!ok)
                Logger.Warning("RegisterHotKey(Ctrl+Q) failed — may conflict with another app");
            else
                Logger.Info("Global hotkey Ctrl+Q registered");
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            base.OnHandleDestroyed(e);
        }

        #endregion

        #region 清理

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnregisterHotKey(this.Handle, HOTKEY_ID);
                _webView?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
