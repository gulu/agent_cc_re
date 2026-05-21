using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QCClient
{
    /// <summary>系统托盘服务</summary>
    public class TrayService : IDisposable
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _menu;
        private readonly int _port;
        private readonly QcEngine _engine;
        private readonly ConfigHelper _config;
        private readonly MainForm _mainForm;

        private bool _autoStart;
        private const string AutoStartKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AutoStartName = "QCClient";

        public TrayService(int port, QcEngine engine, ConfigHelper config, MainForm mainForm)
        {
            _port = port;
            _engine = engine;
            _config = config;
            _mainForm = mainForm;

            _autoStart = IsAutoStartEnabled();

            _menu = new ContextMenuStrip();
            BuildMenu();

            _trayIcon = new NotifyIcon
            {
                Icon = GetAppIcon(),
                Text = "放射科报告质控助手",
                ContextMenuStrip = _menu,
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => OpenPanel();
            _trayIcon.BalloonTipClicked += (s, e) => OpenPanel();

            _engine.QcCompleted += OnQcCompleted;
            Logger.Info("Tray service started on port " + _port);
        }

        private void BuildMenu()
        {
            _menu.Items.Clear();

            _menu.Items.Add("打开质控面板", null, (s, e) => OpenPanel());
            _menu.Items.Add("OCR 区域配置", null, (s, e) => OpenConfig());
            _menu.Items.Add(new ToolStripSeparator());

            var qcItem = _menu.Items.Add("立即质控", null, async (s, e) =>
            {
                if (!string.IsNullOrEmpty(_engine.LastAccessNumber))
                    await _engine.ManualTriggerAsync(_engine.LastAccessNumber);
            });

            _autoStart = IsAutoStartEnabled();
            var autoItem = new ToolStripMenuItem("开机自启动", null, ToggleAutoStart);
            autoItem.Checked = _autoStart;
            _menu.Items.Add(autoItem);

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("关于", null, (s, e) => ShowAbout());
            _menu.Items.Add("退出", null, (s, e) => Exit());
        }

        public void ShowBalloonNotification(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
        {
            var cfg = _config.Get();
            if (!cfg.Web.EnableNotification) return;

            _trayIcon.BalloonTipTitle = title;
            _trayIcon.BalloonTipText = text;
            _trayIcon.BalloonTipIcon = icon;
            _trayIcon.ShowBalloonTip(3600);
        }

        private void OnQcCompleted(QcResponse result)
        {
            string status = result.Passed ? "✅ 通过" : "❌ 未通过";
            string summary = result.Summary ?? "";
            if (summary.Length > 60) summary = summary.Substring(0, 60) + "...";

            _trayIcon.BalloonTipTitle = "放射科报告质控助手";
            _trayIcon.BalloonTipText = $"⭐ 评分: {result.TotalScore:F0}分  {status}\n{summary}";
            _trayIcon.BalloonTipIcon = result.Passed ? ToolTipIcon.Info : ToolTipIcon.Warning;

            if (_config.Get().Web.EnableNotification)
                _trayIcon.ShowBalloonTip(3600);
        }

        /// <summary>打开质控面板 — 显示 WebView2 浮动窗口</summary>
        private void OpenPanel()
        {
            try
            {
                _mainForm.ShowPanel();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to show QC panel");
            }
        }

        /// <summary>打开 OCR 区域配置页面</summary>
        private void OpenConfig()
        {
            try
            {
                _mainForm.ShowPanel();
                _mainForm.NavigateTo("/config.html");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to open config page");
            }
        }

        private void ToggleAutoStart(object sender, EventArgs e)
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey(AutoStartKey, true);
                if (key == null) return;

                if (_autoStart)
                {
                    key.DeleteValue(AutoStartName, false);
                    _autoStart = false;
                }
                else
                {
                    string exePath = Application.ExecutablePath;
                    key.SetValue(AutoStartName, exePath);
                    _autoStart = true;
                }
                key.Close();

                if (sender is ToolStripMenuItem item)
                    item.Checked = _autoStart;

                Logger.Info("Auto-start " + (_autoStart ? "enabled" : "disabled"));
            }
            catch (Exception)
            {
                Logger.Warning("Failed to toggle auto-start");
            }
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey(AutoStartKey, false);
                if (key == null) return false;
                var val = key.GetValue(AutoStartName);
                key.Close();
                return val != null;
            }
            catch { return false; }
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "放射科报告质控助手 v1.0\n\n" +
                "基于 .NET Framework 4.8 + HttpListener + WebView2\n" +
                "自动质控报告质量，提升书写规范",
                "关于",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void Exit()
        {
            _trayIcon.Visible = false;
            Application.ExitThread();
            Environment.Exit(0);
        }

        private Icon GetAppIcon()
        {
            // 尝试加载自定义图标，失败则使用默认图标
            try
            {
                string iconPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (System.IO.File.Exists(iconPath))
                    return new Icon(iconPath);
            }
            catch { }

            // 使用系统图标作为回退
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            _trayIcon?.Dispose();
            _menu?.Dispose();
        }
    }
}
