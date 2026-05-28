using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace QCClient
{
    public class MainForm : Form
    {
        #region Win32 全局热键

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_Q = 0x51;
        private const int WM_HOTKEY = 0x0312;

        #endregion

        private const int COMPACT_W = 300;
        private const int COMPACT_H = 36;
        private readonly int _expandedW;
        private readonly int _expandedH;

        private bool _isExpanded;

        private readonly int _port;
        private readonly QcEngine _engine;
        private readonly ConfigHelper _config;

        // 紧凑栏控件
        private Panel _titleBar;
        private Label _statusDot;
        private Label _titleLabel;
        private Label _scoreLabel;

        // WebView2
        private WebView2 _webView;
        private bool _webViewReady;
        private bool _webViewInitStarted;
        private bool _allowClose;

        public bool IsWebViewReady => _webViewReady;
        public event Action WebViewReady;

        private static readonly Color ColorGray  = Color.FromArgb(148, 163, 184);
        private static readonly Color ColorGreen = Color.FromArgb(34, 197, 94);
        private static readonly Color ColorRed   = Color.FromArgb(239, 68, 68);
        private static readonly Color BgCompact  = Color.FromArgb(30, 41, 59);
        private static readonly Color BgExpanded = Color.FromArgb(30, 96, 190);

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

            SetCompactMode();
        }

        private void InitializeForm()
        {
            Text = "";
            FormBorderStyle = FormBorderStyle.None;
            ControlBox = false;
            ShowInTaskbar = false;
            TopMost = _config.Get().Web.AlwaysOnTop;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            BackColor = BgCompact;

            FormClosing += (s, e) =>
            {
                if (!_allowClose) { e.Cancel = true; SetCompactMode(); }
            };
        }

        #region 标题栏

        private void BuildTitleBar()
        {
            _titleBar = new Panel
            {
                Height = COMPACT_H,
                Dock = DockStyle.Top,
                BackColor = BgCompact,
                Cursor = Cursors.Hand,
            };

            _statusDot = new Label
            {
                Text = "●",
                Font = new Font("Segoe UI", 8, FontStyle.Regular),
                ForeColor = ColorGray,
                AutoSize = true,
                Location = new Point(12, 10),
                BackColor = Color.Transparent,
            };

            _titleLabel = new Label
            {
                Text = "报告质控助手",
                Font = new Font("Microsoft YaHei", 9, FontStyle.Regular),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(28, 8),
                BackColor = Color.Transparent,
            };

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

            _titleBar.Click += (s, e) =>
            {
                if (!_isExpanded) SetExpandedMode();
            };

            _titleBar.DoubleClick += (s, e) =>
            {
                if (_isExpanded) SetCompactMode(); else SetExpandedMode();
            };

            Controls.Add(_titleBar);
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

                    _webView.SourceChanged += (src, srcArgs) =>
                    {
                        string url = _webView.Source?.OriginalString ?? "";
                        if (url.Contains("#collapse"))
                        {
                            string restoreUrl = url.Replace("#collapse", "");
                            if (restoreUrl != url)
                                _webView.Source = new Uri(restoreUrl);
                            BeginInvoke(new Action(SetCompactMode));
                        }
                    };

                    _webViewReady = true;
                    WebViewReady?.Invoke();
                    Logger.Info("WebView2 initialized");
                }
                else
                {
                    Logger.Error("WebView2 init failed: " + e.InitializationException?.Message);
                }
            };

            Controls.Add(_webView);
        }

        private void EnsureWebView2()
        {
            if (_webViewInitStarted) return;
            _webViewInitStarted = true;

            string theme = _config.Get().Web.Theme ?? "light";
            _webView.Source = new Uri($"http://127.0.0.1:{_port}/index.html?theme={theme}");
        }

        #endregion

        #region 模式切换

        private void SetCompactMode()
        {
            _isExpanded = false;
            Size = new Size(COMPACT_W, COMPACT_H);
            BackColor = BgCompact;
            _titleBar.BackColor = BgCompact;
            _titleBar.Height = COMPACT_H;
            _titleBar.Cursor = Cursors.Hand;
            _webView.Visible = false;
            PositionForm();
            Show();
        }

        private void SetExpandedMode()
        {
            _isExpanded = true;
            EnsureWebView2();

            Size = new Size(_expandedW, _expandedH);
            BackColor = BgExpanded;
            _titleBar.BackColor = BgExpanded;
            _titleBar.Height = 36;
            _titleBar.Cursor = Cursors.Default;
            _webView.Visible = true;
            PositionForm();
            Show();
            Activate();
        }

        public void ToggleMode()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ToggleMode));
                return;
            }

            if (_isExpanded) SetCompactMode();
            else SetExpandedMode();
        }

        #endregion

        #region 公开方法

        public void ShowPanel()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ShowPanel));
                return;
            }
            SetExpandedMode();
        }

        public void NavigateTo(string path)
        {
            if (!_webViewReady || _webView?.CoreWebView2 == null) return;

            if (InvokeRequired)
            {
                Invoke(new Action<string>(NavigateTo), path);
                return;
            }

            string url = $"http://127.0.0.1:{_port}{path}";
            dynamic core = _webView.CoreWebView2;
            core.Navigate(url);
        }

        public Rectangle? ShowScreenPicker()
        {
            if (InvokeRequired)
                return (Rectangle?)Invoke(new Func<Rectangle?>(ShowScreenPicker));

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

            if (!wasCollapsed) SetExpandedMode();
            return result;
        }

        public void CloseForm()
        {
            _allowClose = true;
            if (InvokeRequired)
                Invoke(new Action(CloseForm));
            else
                Close();
        }

        #endregion

        private void PositionForm()
        {
            var screen = Screen.PrimaryScreen.WorkingArea;
            Left = screen.Right - (_isExpanded ? _expandedW : COMPACT_W);
            Top = _isExpanded ? screen.Top : screen.Bottom - COMPACT_H;
        }

        #region 状态更新

        private void OnQcResult(QcResponse result)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<QcResponse>(OnQcResult), result);
                return;
            }

            _statusDot.ForeColor = result.Passed ? ColorGreen : ColorRed;
            _scoreLabel.Text = $"⭐{result.TotalScore:F0} {(result.Passed ? "✅" : "❌")}";
            _scoreLabel.ForeColor = result.Passed ? ColorGreen : ColorRed;
        }

        private void OnConnectionChanged(bool online)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(OnConnectionChanged), online);
                return;
            }

            _statusDot.ForeColor = online ? ColorGreen : ColorGray;
            _titleLabel.ForeColor = online ? Color.White : Color.FromArgb(180, 180, 180);
            _scoreLabel.Text = "";
        }

        #endregion

        #region 全局热键

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
            bool ok = RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL, VK_Q);
            if (!ok)
                Logger.Warning("RegisterHotKey(Ctrl+Q) failed");
            else
                Logger.Info("Global hotkey Ctrl+Q registered");
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotKey(Handle, HOTKEY_ID);
            base.OnHandleDestroyed(e);
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnregisterHotKey(Handle, HOTKEY_ID);
                _webView?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
