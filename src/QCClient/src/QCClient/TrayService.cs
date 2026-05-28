using System;
using System.Drawing;
using System.Windows.Forms;

namespace QCClient
{
    public class TrayService : IDisposable
    {
        private readonly int _port;
        private readonly QcEngine _engine;
        private readonly ConfigHelper _config;
        private readonly MainForm _mainForm;
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _trayMenu;

        private QcResponse _lastResult;

        public TrayService(int port, QcEngine engine, ConfigHelper config, MainForm mainForm)
        {
            _port = port;
            _engine = engine;
            _config = config;
            _mainForm = mainForm;

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("打开质控面板", null, (s, e) => _mainForm.ShowPanel());
            _trayMenu.Items.Add("立即质控", null, (s, e) => { });

            var autoStartItem = new ToolStripMenuItem("开机自启动");
            autoStartItem.Checked = IsAutoStartEnabled();
            autoStartItem.Click += (s, e) =>
            {
                autoStartItem.Checked = !autoStartItem.Checked;
                SetAutoStart(autoStartItem.Checked);
            };
            _trayMenu.Items.Add(autoStartItem);

            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("退出", null, (s, e) =>
            {
                _mainForm.CloseForm();
                Application.Exit();
            });

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "放射科报告质控助手",
                ContextMenuStrip = _trayMenu,
                Visible = true,
            };

            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    _mainForm.ShowPanel();
            };

            _engine.QcCompleted += OnQcResult;
            _engine.ConnectionStatusChanged += OnConnectionChanged;
        }

        private void OnQcResult(QcResponse result)
        {
            _lastResult = result;

            if (_config.Get().Web.EnableNotification)
            {
                string title = $"放射科报告质控助手 - ⭐{result.TotalScore:F0}分 {(result.Passed ? "✅ 通过" : "❌ 未通过")}";
                string text = result.Summary ?? (result.Passed ? "报告质量合格" : "请查看问题列表");

                _trayIcon.ShowBalloonTip(3600, title, text, ToolTipIcon.Info);
            }
        }

        private void OnConnectionChanged(bool online)
        {
            _trayIcon.Text = online
                ? "放射科报告质控助手 - 已连接"
                : "放射科报告质控助手 - 离线";
        }

        #region 开机自启动

        private static bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key?.GetValue("QCClient") != null;
                }
            }
            catch { return false; }
        }

        private static void SetAutoStart(bool enable)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (enable)
                        key?.SetValue("QCClient", Application.ExecutablePath);
                    else
                        key?.DeleteValue("QCClient", false);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Set auto start failed");
            }
        }

        #endregion

        public void Dispose()
        {
            _trayIcon?.Dispose();
            _trayMenu?.Dispose();
        }
    }
}
