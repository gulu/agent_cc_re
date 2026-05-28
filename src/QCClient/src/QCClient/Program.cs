using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace QCClient
{
    static class Program
    {
        private static HttpListener _listener;
        private static int _port;
        private static ConfigHelper _config;
        private static QcEngine _engine;
        private static TrayService _tray;
        private static MainForm _mainForm;

        [STAThread]
        static void Main()
        {
            Console.Title = "QCClient - 放射科报告质控助手";
            Console.WriteLine("═══ QCClient 启动中... ═══");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var mutex = new Mutex(true, "QCClient-SingleInstance", out bool created))
            {
                if (!created)
                {
                    MessageBox.Show("QCClient 已在运行中", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    Start();
                    Console.WriteLine($"═══ QCClient 就绪！访问 http://127.0.0.1:{_port}/ ═══");
                    Application.Run();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "QCClient startup failed");
                    MessageBox.Show($"启动失败: {ex.Message}\n\n{ex.GetType().Name}",
                        "QCClient 错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Cleanup();
                }
            }
        }

        static void Start()
        {
            Console.WriteLine("[1/5] 加载配置...");
            _config = new ConfigHelper();
            var cfg = _config.Get();
            Console.WriteLine($"       Agent_QC: {cfg.Backend.Url}");

            Console.WriteLine("[2/5] 初始化日志...");
            SetupLogging(cfg);
            Logger.Info("=== QCClient 启动 ===");

            Console.WriteLine("[3/5] 启动 HttpListener...");
            StartHttpListener();
            Console.WriteLine($"       端口: {_port}");

            Console.WriteLine("[4/5] 创建质控引擎...");
            _engine = new QcEngine(_config);

            Console.WriteLine("[5/5] 启动 WinForms 消息循环...");
            _mainForm = new MainForm(_port, _engine, _config);
            _tray = new TrayService(_port, _engine, _config, _mainForm);

            _engine.StartPolling();

            Logger.Info("QCClient ready at http://127.0.0.1:" + _port + "/");
        }

        #region 日志

        static void SetupLogging(AppConfig cfg)
        {
            string logPath = cfg.Logging.File.Path;
            Logger.Init(logPath);
            Logger.Info("=== QCClient 启动 ===");
        }

        #endregion

        #region HttpListener

        static void StartHttpListener()
        {
            _port = FindFreePort();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();
            Logger.Info("HttpListener started on port " + _port);

            Task.Run(async () =>
            {
                try
                {
                    while (_listener.IsListening)
                    {
                        var ctx = await _listener.GetContextAsync();
                        _ = HandleRequestAsync(ctx);
                    }
                }
                catch (HttpListenerException) when (!_listener.IsListening) { }
                catch (ObjectDisposedException) { }
                catch (Exception ex) { Logger.Error(ex, "HttpListener error"); }
            });
        }

        static async Task HandleRequestAsync(HttpListenerContext ctx)
        {
            var request = ctx.Request;
            var response = ctx.Response;
            string path = request.Url.AbsolutePath.TrimEnd('/');
            string method = request.HttpMethod.ToUpperInvariant();

            try
            {
                if (path == "/api/sse" && method == "GET")
                {
                    await HandleSseAsync(ctx);
                    return;
                }

                if (path.StartsWith("/api/"))
                {
                    string json = await HandleApiAsync(method, path, request);
                    WriteJsonResponse(response, json);
                    return;
                }

                await ServeStaticFileAsync(path, response);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Request error: " + method + " " + path);
                if (response.OutputStream.CanWrite)
                    WriteJsonResponse(response, JsonConvert.SerializeObject(
                        new { code = 500, msg = "Internal error: " + ex.Message }), 500);
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        #endregion

        #region API 路由

        static async Task<string> HandleApiAsync(string method, string path, HttpListenerRequest request)
        {
            switch (path)
            {
                case "/api/status":
                    return JsonConvert.SerializeObject(new
                    {
                        code = 0,
                        data = new
                        {
                            online = _engine.BackendOnline,
                            lastAccessNumber = _engine.LastAccessNumber ?? ""
                        }
                    });

                case "/api/qc":
                    if (method == "POST")
                    {
                        string body = await ReadBodyAsync(request);
                        var req = JsonConvert.DeserializeAnonymousType(body, new { accessNumber = "" });
                        if (req == null || string.IsNullOrWhiteSpace(req.accessNumber))
                            return JsonConvert.SerializeObject(new { code = 400, msg = "accessNumber required" });

                        var result = await _engine.ManualTriggerAsync(req.accessNumber);
                        if (result != null)
                            return JsonConvert.SerializeObject(new { code = 0, data = result });
                        else
                            return JsonConvert.SerializeObject(new { code = 500, msg = "质控失败" });
                    }
                    return JsonConvert.SerializeObject(new { code = 405, msg = "Method not allowed" });

                case "/api/qc/manual":
                    if (method == "POST")
                    {
                        string body = await ReadBodyAsync(request);
                        var req = JsonConvert.DeserializeAnonymousType(body,
                            new { findings = "", impression = "" });
                        if (req == null)
                            return JsonConvert.SerializeObject(new { code = 400, msg = "Invalid body" });

                        var result = await _engine.ManualInputAsync(req.findings, req.impression);
                        if (result != null)
                            return JsonConvert.SerializeObject(new { code = 0, data = result });
                        else
                            return JsonConvert.SerializeObject(new { code = 500, msg = "质控分析失败" });
                    }
                    return JsonConvert.SerializeObject(new { code = 405, msg = "Method not allowed" });

                case "/api/config":
                    if (method == "GET")
                        return JsonConvert.SerializeObject(new { code = 0, data = _config.Get() });
                    return JsonConvert.SerializeObject(new { code = 405, msg = "Method not allowed" });

                case "/api/config/ocr-areas":
                    if (method == "GET")
                        return JsonConvert.SerializeObject(new { code = 0, data = _config.Get().Ocr.Areas });
                    if (method == "POST")
                    {
                        string body = await ReadBodyAsync(request);
                        var areas = JsonConvert.DeserializeObject<OcrAreaConfig[]>(body);
                        if (areas == null)
                            return JsonConvert.SerializeObject(new { code = 400, msg = "Invalid areas" });

                        var cfg = _config.Get();
                        cfg.Ocr.Areas = areas;
                        _config.Save(cfg);
                        return JsonConvert.SerializeObject(new { code = 0, msg = "OK" });
                    }
                    return JsonConvert.SerializeObject(new { code = 405, msg = "Method not allowed" });

                case "/api/ocr/test-area":
                    if (method == "POST")
                    {
                        string body = await ReadBodyAsync(request);
                        var area = JsonConvert.DeserializeAnonymousType(body,
                            new { Name = "", Type = "", X = 0, Y = 0, Width = 100, Height = 30 });
                        if (area == null || area.Width <= 0 || area.Height <= 0)
                            return JsonConvert.SerializeObject(new { code = 400, msg = "Invalid area params" });

                        var ocrArea = new OcrAreaConfig
                        {
                            Name = area.Name ?? "test",
                            Type = area.Type ?? "unknown",
                            X = area.X, Y = area.Y,
                            Width = area.Width, Height = area.Height,
                            Enabled = true
                        };

                        var testResult = await _engine.TestOcrAreaAsync(ocrArea);
                        return JsonConvert.SerializeObject(new { code = 0, data = testResult });
                    }
                    return JsonConvert.SerializeObject(new { code = 405, msg = "Method not allowed" });

                case "/api/ocr/screen-pick":
                    if (method == "POST")
                    {
                        var rect = _mainForm.ShowScreenPicker();
                        if (rect.HasValue)
                            return JsonConvert.SerializeObject(new
                            {
                                code = 0,
                                data = new { x = rect.Value.X, y = rect.Value.Y, width = rect.Value.Width, height = rect.Value.Height }
                            });
                        return JsonConvert.SerializeObject(new { code = 1, msg = "用户取消框选" });
                    }
                    return JsonConvert.SerializeObject(new { code = 405, msg = "Method not allowed" });

                case "/api/config/web":
                    if (method == "GET")
                        return JsonConvert.SerializeObject(new { code = 0, data = _config.Get().Web });
                    if (method == "POST")
                    {
                        string body = await ReadBodyAsync(request);
                        var web = JsonConvert.DeserializeObject<WebSettings>(body);
                        if (web == null)
                            return JsonConvert.SerializeObject(new { code = 400, msg = "Invalid web settings" });

                        var cfg = _config.Get();
                        if (!string.IsNullOrEmpty(web.Theme)) cfg.Web.Theme = web.Theme;
                        cfg.Web.SidebarWidth = web.SidebarWidth > 0 ? web.SidebarWidth : cfg.Web.SidebarWidth;
                        cfg.Web.EnableNotification = web.EnableNotification;
                        cfg.Web.AlwaysOnTop = web.AlwaysOnTop;
                        cfg.Web.EnableSound = web.EnableSound;
                        cfg.Web.ShowDebugLog = web.ShowDebugLog;
                        _config.Save(cfg);
                        return JsonConvert.SerializeObject(new { code = 0, msg = "OK" });
                    }
                    return JsonConvert.SerializeObject(new { code = 405, msg = "Method not allowed" });

                case "/api/config/reset":
                    if (method == "POST")
                    {
                        _config.Save(new AppConfig());
                        return JsonConvert.SerializeObject(new { code = 0, msg = "Reset OK" });
                    }
                    return JsonConvert.SerializeObject(new { code = 405, msg = "Method not allowed" });

                default:
                    return JsonConvert.SerializeObject(new { code = 404, msg = "Not found" });
            }
        }

        #endregion

        #region SSE

        static async Task HandleSseAsync(HttpListenerContext ctx)
        {
            var response = ctx.Response;
            response.ContentType = "text/event-stream; charset=utf-8";
            response.ContentEncoding = Encoding.UTF8;
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");
            response.Headers.Add("Access-Control-Allow-Origin", "*");

            var writer = new StreamWriter(response.OutputStream, Encoding.UTF8) { AutoFlush = true };
            var client = _engine.AddSseClient(writer);

            await writer.WriteLineAsync($"event: connected");
            await writer.WriteLineAsync($"data: {{\"port\":{_port}}}");
            await writer.WriteLineAsync("");
            await writer.FlushAsync();

            _engine.PushConnectionStatus();

            try
            {
                var buffer = new byte[1024];
                while (client.Connected && _listener.IsListening)
                {
                    await Task.Delay(30000);
                    if (client.Connected)
                    {
                        await writer.WriteLineAsync(": heartbeat");
                        await writer.FlushAsync();
                    }
                }
            }
            catch { }
            finally
            {
                _engine.RemoveSseClient(client);
                try { response.Close(); } catch { }
            }
        }

        #endregion

        #region 静态文件

        static async Task ServeStaticFileAsync(string path, HttpListenerResponse response)
        {
            string wwwroot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
            if (!Directory.Exists(wwwroot))
                wwwroot = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "wwwroot"));

            if (string.IsNullOrEmpty(path) || path == "/")
                path = "/index.html";

            string filePath = Path.Combine(wwwroot, path.TrimStart('/'));

            if (!File.Exists(filePath))
            {
                WriteJsonResponse(response, JsonConvert.SerializeObject(
                    new { code = 404, msg = "File not found" }), 404);
                return;
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string contentType = ext switch
            {
                ".html" => "text/html; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                _ => "application/octet-stream",
            };

            response.ContentType = contentType;
            response.ContentEncoding = Encoding.UTF8;
            response.Headers.Add("Cache-Control", "no-cache");

            byte[] data = File.ReadAllBytes(filePath);
            // 去除 UTF-8 BOM (EF BB BF)，防止 WebView2 在文档开头渲染意外字符
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                data = data.AsSpan(3).ToArray();

            response.ContentLength64 = data.Length;
            await response.OutputStream.WriteAsync(data, 0, data.Length);
        }

        #endregion

        #region 工具方法

        static async Task<string> ReadBodyAsync(HttpListenerRequest request)
        {
            if (!request.HasEntityBody) return "";
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                return await reader.ReadToEndAsync();
        }

        static void WriteJsonResponse(HttpListenerResponse response, string json, int statusCode = 200)
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = data.Length;
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.OutputStream.Write(data, 0, data.Length);
        }

        static int FindFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        #endregion

        #region 清理

        static void Cleanup()
        {
            Logger.Info("=== QCClient 关闭 ===");
            try { _engine?.Dispose(); } catch { }
            try { _tray?.Dispose(); } catch { }
            try { _mainForm?.CloseForm(); } catch { }

            try
            {
                if (_listener != null && _listener.IsListening)
                {
                    _listener.Stop();
                    _listener.Close();
                }
            }
            catch { }
        }

        #endregion
    }
}
