using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QCClient
{
    #region Win32 空闲检测

    internal struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    internal static class User32
    {
        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        public static uint GetIdleMilliseconds()
        {
            var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            if (!GetLastInputInfo(ref lii)) return 0;
            return (uint)Environment.TickCount - lii.dwTime;
        }

        public static double GetIdleSeconds()
        {
            return GetIdleMilliseconds() / 1000.0;
        }
    }

    #endregion
    #region API 模型 — 与后端 PascalCase 匹配

    /// <summary>统一 API 响应包装（QCService 已改用 PascalCase 输出）</summary>
    public class ApiResponse<T>
    {
        // Newtonsoft.Json 默认大小写敏感，后端必须输出 PascalCase（Code/Msg/Data）
        // QCService Program.cs 已移除 CamelCase → 输出 PascalCase 匹配此模型
        public int Code { get; set; }
        public string Msg { get; set; }
        public T Data { get; set; }
        public bool IsOk => Code == 200;
    }

    public class QcRequest
    {
        [JsonProperty("reportId")] public string ReportId { get; set; }
        [JsonProperty("findings")] public string Findings { get; set; }
        [JsonProperty("impression")] public string Impression { get; set; }
        [JsonProperty("reportType")] public string ReportType { get; set; }
        [JsonProperty("patientName")] public string PatientName { get; set; }
        [JsonProperty("patientGender")] public string PatientGender { get; set; }
        [JsonProperty("patientAge")] public int PatientAge { get; set; }
        [JsonProperty("clinicalDiagnosis")] public string ClinicalDiagnosis { get; set; }
        [JsonProperty("requestDepartment")] public string RequestDepartment { get; set; }
        [JsonProperty("examPart")] public string ExamPart { get; set; }
        [JsonProperty("examMethod")] public string ExamMethod { get; set; }
        [JsonProperty("accessionNo")] public string AccessionNo { get; set; }
    }

    /// <summary>ReportQC 后端 QcResponse（后端输出 camelCase，用 [JsonProperty] 精确匹配）</summary>
    public class QcResponse
    {
        [JsonProperty("reportId")] public string ReportId { get; set; }
        [JsonProperty("totalScore")] public double TotalScore { get; set; }
        [JsonProperty("passScore")] public double PassScore { get; set; } = 90;
        [JsonProperty("passed")] public bool Passed { get; set; }
        [JsonProperty("qcLevel")] public string QcLevel { get; set; }
        [JsonProperty("checkItems")] public List<QcCheckItem> CheckItems { get; set; }
        [JsonProperty("issues")] public List<QcIssueDto> Issues { get; set; }
        [JsonProperty("summary")] public string Summary { get; set; }
        [JsonProperty("processTimeMs")] public int ProcessTimeMs { get; set; }
    }

    public class QcCheckItem
    {
        [JsonProperty("dimensionCode")] public string DimensionCode { get; set; }
        [JsonProperty("dimensionName")] public string DimensionName { get; set; }
        [JsonProperty("passed")] public bool Passed { get; set; }
        [JsonProperty("score")] public double Score { get; set; }
        [JsonProperty("weight")] public double Weight { get; set; }
    }

    /// <summary>ReportQC 后端 QcIssueDto（后端输出 camelCase，用 [JsonProperty] 精确匹配）</summary>
    public class QcIssueDto
    {
        [JsonProperty("issueType")] public string IssueType { get; set; }
        [JsonProperty("subType")] public string SubType { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("severity")] public string Severity { get; set; }
        [JsonProperty("location")] public string Location { get; set; }
        [JsonProperty("originalText")] public string OriginalText { get; set; }
        [JsonProperty("suggestedText")] public string SuggestedText { get; set; }
        [JsonProperty("suggestion")] public string Suggestion { get; set; }

        /// <summary>供前端使用的统一消息字段</summary>
        [JsonIgnore] public string Message => Description ?? IssueType ?? "";
    }

    /// <summary>QCService OCR 响应（QCService 已改为 PascalCase 输出）</summary>
    public class OcrRecognizeResponse
    {
       public string AreaId { get; set; }
        public string Text { get; set; }
         public bool Success { get; set; }
        public double Confidence { get; set; }
        public int ProcessTimeMs { get; set; }
         public string ErrorMessage { get; set; }
    }

    /// <summary>QCService 报告查询结果（QCService 已改为 PascalCase 输出）</summary>
    public class ReportInfo
    {
        public string AccessNumber { get; set; }
        public string PatientName { get; set; }
        public string PatientGender { get; set; }
         public string PatientAgeStr { get; set; }
      public string ExamType { get; set; }
       public string ExamBodyPart { get; set; }
        public string ClinicalHistory { get; set; }
        public string Department { get; set; }
        public string ReportContent { get; set; }
       public string ReportDiagnosis { get; set; }
        [JsonIgnore] public int PatientAge
        {
            get { int.TryParse(PatientAgeStr, out int v); return v; }
        }
    }

    #endregion

    #region SSE

    public class SseClient
    {
        public StreamWriter Writer { get; set; }
        public bool Connected { get; set; } = true;
    }

    #endregion

    /// <summary>质控引擎：截图 + API + 自动轮询 + SSE</summary>
    public class QcEngine : IDisposable
    {
        private readonly ConfigHelper _config;
        private readonly HttpClient _http;
        private Timer _pollTimer;
        private bool _polling = false;
        private int _pollIntervalMs = 5000;

        private readonly List<SseClient> _sseClients = new List<SseClient>();
        private readonly object _sseLock = new object();

        private string _lastAccessNumber = "";
        private DateTime _lastQcTime = DateTime.MinValue;

        private bool _qcServiceOnline = false;
        private bool _reportQcOnline = false;
        private QcResponse _lastResult = null;

        public event Action<QcResponse> QcCompleted;
        public event Action<bool, bool> ConnectionStatusChanged;

        // 屏幕框选结果存储
        private static Rectangle? _screenPickerResult = null;
        public static Rectangle? ScreenPickerResult
        {
            get { var r = _screenPickerResult; _screenPickerResult = null; return r; }
            set { _screenPickerResult = value; }
        }

        public QcEngine(ConfigHelper config)
        {
            _config = config;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(config.Get().Backend.ApiTimeoutSeconds) };
        }

        public bool QcServiceOnline => _qcServiceOnline;
        public bool ReportQcOnline => _reportQcOnline;
        public QcResponse LastResult => _lastResult;
        public string LastAccessNumber => _lastAccessNumber;

        #region 连接检测

        public async Task CheckConnectionAsync()
        {
            var cfg = _config.Get();
            bool qcOk = await PingAsync(cfg.Backend.QCServiceUrl, "/api/v1/ocr/health");
            bool rqcOk = await PingAsync(cfg.Backend.ReportQCUrl, "/api/v1/qc/report");

            if (qcOk != _qcServiceOnline || rqcOk != _reportQcOnline)
            {
                _qcServiceOnline = qcOk;
                _reportQcOnline = rqcOk;
                PushConnectionStatus();
                ConnectionStatusChanged?.Invoke(qcOk, rqcOk);
            }
        }

        private async Task<bool> PingAsync(string baseUrl, string path)
        {
            try
            {
                var resp = await _http.GetAsync($"{baseUrl}{path}");
                // 200 OK、400 BadRequest、405 MethodNotAllowed 都表示服务在线
                return resp.IsSuccessStatusCode
                    || resp.StatusCode == System.Net.HttpStatusCode.BadRequest
                    || resp.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed;
            }
            catch { return false; }
        }

        #endregion

        #region 截图

        public Bitmap CaptureArea(int x, int y, int width, int height)
        {
            var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(x, y, 0, 0, new Size(width, height));
            return bmp;
        }

        /// <summary>截图区域并转换为 Base64 PNG 字符串</summary>
        public string CaptureAreaToBase64(int x, int y, int width, int height)
        {
            using (var bmp = CaptureArea(x, y, width, height))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <summary>测试 OCR 区域：截图 + 识别 + 返回预览结果</summary>
        public async Task<object> TestOcrAreaAsync(OcrAreaConfig area)
        {
            var result = new Dictionary<string, object>
            {
                ["areaName"] = area.Name,
                ["x"] = area.X,
                ["y"] = area.Y,
                ["width"] = area.Width,
                ["height"] = area.Height,
                ["imageBase64"] = CaptureAreaToBase64(area.X, area.Y, area.Width, area.Height)
            };

            // 尝试 OCR 识别
            try
            {
                var ocrResults = await RecognizeAsync(new List<OcrAreaConfig> { area });
                if (ocrResults.Count > 0)
                {
                    var ocr = ocrResults[0];
                    result["ocrText"] = ocr.Text ?? "";
                    result["ocrConfidence"] = Math.Round(ocr.Confidence * 100, 1);
                    result["ocrSuccess"] = ocr.Success;
                }
                else
                {
                    result["ocrText"] = "(识别服务未返回结果)";
                    result["ocrConfidence"] = 0.0;
                    result["ocrSuccess"] = false;
                }
            }
            catch (Exception ex)
            {
                result["ocrText"] = "(识别失败: " + ex.Message + ")";
                result["ocrConfidence"] = 0.0;
                result["ocrSuccess"] = false;
            }

            return result;
        }

        #endregion

        #region API 调用

        /// <summary>OCR 识别 — 上传图片文件流</summary>
        public async Task<List<OcrRecognizeResponse>> RecognizeAsync(List<OcrAreaConfig> areas)
        {
            var results = new List<OcrRecognizeResponse>();
            var cfg = _config.Get();

            foreach (var area in areas)
            {
                if (!area.Enabled) continue;
                try
                {
                    using (var bmp = CaptureArea(area.X, area.Y, area.Width, area.Height))
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        var imgBytes = ms.ToArray();
                        Logger.Info($"[OCR-SEND] area={area.Name}, pos=({area.X},{area.Y}), size={area.Width}x{area.Height}, bytes={imgBytes.Length}");

                        // 保存截图到本地目录（便于核对截取是否正确）
                        // 格式: screenshots/日期/类型_x_y_w_h_随机数.png
                        try
                        {
                            var dateDir = DateTime.Now.ToString("yyyyMMdd");
                            var ssDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots", dateDir);
                            Directory.CreateDirectory(ssDir);
                            var rand = Math.Abs(DateTime.Now.Ticks.GetHashCode()) % 100000;
                            var ssName = $"{area.Type}_{area.X}_{area.Y}_{area.Width}_{area.Height}_{rand:D5}.png";
                            var ssPath = Path.Combine(ssDir, ssName);
                            bmp.Save(ssPath, ImageFormat.Png);
                            Logger.Info($"[OCR-SNAP] saved: {ssPath} (dir={ssDir})");
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning(ex, $"[OCR-SNAP] save failed, dir={AppDomain.CurrentDomain.BaseDirectory}");
                        }

                        ms.Seek(0, SeekOrigin.Begin);

                        var form = new MultipartFormDataContent();
                        var imageContent = new StreamContent(ms);
                        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                        form.Add(imageContent, "image", $"{area.Name}.png");
                        form.Add(new StringContent(area.Name), "areaId");
                        form.Add(new StringContent(area.Type ?? ""), "areaType");

                        // 带超时的 OCR 请求（默认 10 秒）
                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                        {
                            var resp = await _http.PostAsync(
                                $"{cfg.Backend.QCServiceUrl}/api/v1/ocr/recognize", form, cts.Token);
                            if (resp.IsSuccessStatusCode)
                            {
                                var json = await resp.Content.ReadAsStringAsync();
                                var apiResp = JsonConvert.DeserializeObject<ApiResponse<OcrRecognizeResponse>>(json);
                                if (apiResp?.IsOk == true && apiResp.Data != null)
                                {
                                    apiResp.Data.AreaId = area.Name;
                                    results.Add(apiResp.Data);
                                    Logger.Info($"[OCR-RECV] area={area.Name}, success={apiResp.Data.Success}, text='{apiResp.Data.Text}', conf={apiResp.Data.Confidence:P2}, err={apiResp.Data.ErrorMessage ?? "none"}");
                                }
                                else
                                {
                                    Logger.Warning($"[OCR-RECV] area={area.Name}, API returned non-ok: Code={apiResp?.Code}, Msg={apiResp?.Msg}");
                                }
                            }
                            else
                            {
                                Logger.Warning($"[OCR-RECV] area={area.Name}, HTTP {resp.StatusCode}");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.Warning("OCR timeout for " + area.Name);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "OCR failed for " + area.Name);
                }
            }
            return results;
        }

        /// <summary>查询报告</summary>
        public async Task<ReportInfo> QueryReportAsync(string accessNumber)
        {
            try
            {
                var cfg = _config.Get();
                var resp = await _http.GetAsync(
                    $"{cfg.Backend.QCServiceUrl}/api/v1/qc/query-report?accessNumber={Uri.EscapeDataString(accessNumber)}");
                if (!resp.IsSuccessStatusCode) return null;

                var json = await resp.Content.ReadAsStringAsync();
                var apiResp = JsonConvert.DeserializeObject<ApiResponse<ReportInfo>>(json);
                return apiResp?.IsOk == true ? apiResp.Data : null;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Query report failed for " + accessNumber);
                return null;
            }
        }

        /// <summary>执行质控 — 调用 ReportQC</summary>
        public async Task<QcResponse> ExecuteQcAsync(QcRequest request)
        {
            try
            {
                var cfg = _config.Get();
                var payload = JsonConvert.SerializeObject(request);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync($"{cfg.Backend.ReportQCUrl}/api/v1/qc/report", content);

Logger.Info( $"ReportQC content ={content}");

                if (!resp.IsSuccessStatusCode) return null;

                var json = await resp.Content.ReadAsStringAsync();
                Logger.Info("[DEBUG] ReportQC raw response (first 500 chars): " +
                    (json?.Length > 500 ? json.Substring(0, 500) : json));

                // ★ ReportQC 返回 AjaxResult {code/code, msg/Msg, data/Data}
                // 成功路径：ASP.NET Core System.Text.Json → camelCase
                // 错误路径：BaseController Newtonsoft.Json → PascalCase
                // 两阶段解析：先提取 data 字段，再映射到 QcResponse
                var jObj = JObject.Parse(json);

                // 兼容 camelCase(code) 和 PascalCase(Code)
                var code = jObj["code"]?.Value<int>()
                        ?? jObj["Code"]?.Value<int>()
                        ?? 0;
                if (code != 200)
                {
                    var msg = jObj["msg"]?.Value<string>()
                           ?? jObj["Msg"]?.Value<string>()
                           ?? "unknown";
                    Logger.Warning(null, $"ReportQC returned code={code}, msg={msg}");
                    return null;
                }

                // 兼容 camelCase(data) 和 PascalCase(Data)
                var dataToken = jObj["data"] ?? jObj["Data"];
                if (dataToken == null || dataToken.Type == JTokenType.Null)
                {
                    Logger.Warning(null, "ReportQC returned code=200 but data is null");
                    return null;
                }

                // data 内层 QcResponse 是 camelCase (ASP.NET Core) 或 PascalCase (Newtonsoft)
                // QcResponse 的 [JsonProperty("camelCase")] 匹配 camelCase
                // 对于 PascalCase 数据，JObject.ToObject 也会按 CLR 属性名匹配
                // 所以无论哪种大小写都能正确映射
                var result = dataToken.ToObject<QcResponse>();
                Logger.Info($"[DEBUG] Parsed QcResponse: TotalScore={result?.TotalScore}, Passed={result?.Passed}, Issues={result?.Issues?.Count ?? 0}, CheckItems={result?.CheckItems?.Count ?? 0}");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Execute QC failed");
                return null;
            }
        }

        #endregion

        #region 自动轮询

        public void StartPolling(int intervalMs = 500)
        {
            _pollIntervalMs = intervalMs;
            _pollTimer?.Dispose();
            _pollTimer = new Timer(async _ => await PollAsync(), null, 1000, _pollIntervalMs);
        }

        public void StopPolling()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
        }

        private DateTime _lastOcrTriggerTime = DateTime.MinValue;
        private string _lastOcrAccessNumber = "";

        private async Task PollAsync()
        {
            if (_polling) return;
            _polling = true;
            try
            {
                var cfg = _config.Get();

                // 空闲检测：IdleSeconds=0 时使用固定间隔轮询
                int idleSec = cfg.Ocr.IdleSeconds;
                if (idleSec > 0)
                {
                    double idle = User32.GetIdleSeconds();
                    if (idle < idleSec)
                    {
                        // 用户仍在操作，静默等待
                        return;
                    }
                    // 防止短时间内重复触发（至少间隔 idleSec*2 秒）
                    if ((DateTime.Now - _lastOcrTriggerTime).TotalSeconds < idleSec * 2)
                        return;
                }

                await CheckConnectionStatus();
                if (!_qcServiceOnline) { PushStatus("offline", "QCService 离线"); return; }

                var accessAreas = cfg.Ocr.Areas.Where(a => a.Type == "access_number" && a.Enabled).ToList();
                if (accessAreas.Count == 0) return;

                // 空闲后提示
                if (idleSec > 0) PushStatus("idle", $"检测到空闲，正在 OCR 识别...");

                // 标记已触发（防止 OCR 期间重复触发）
                _lastOcrTriggerTime = DateTime.Now;

                var results = await RecognizeAsync(accessAreas);
                if (results.Count == 0)
                {
                    PushStatus("waiting", "OCR 未识别到有效影像号");
                    return;
                }

                var ocrText = (results[0].Text ?? "").Trim();
                if (!IsValidAccessNumber(ocrText))
                {
                    if (idleSec > 0) PushStatus("waiting", "等待有效影像号...");
                    return;
                }

                // 相同影像号 5 分钟内不重复
                if (ocrText == _lastAccessNumber && (DateTime.Now - _lastQcTime).TotalMinutes < 5)
                {
                    if (ocrText != _lastOcrAccessNumber) PushStatus("waiting", $"影像号 {ocrText} 5分钟内已质控");
                    return;
                }

                _lastAccessNumber = ocrText;
                _lastOcrAccessNumber = ocrText;

                await RunFullQcAsync(ocrText);
            }
            catch (Exception ex) { Logger.Error(ex, "Auto QC error"); }
            finally { _polling = false; }
        }

        /// <summary>完整质控流程</summary>
        private async Task<QcResponse> RunFullQcAsync(string accessNumber, string manualFindings = null, string manualImpression = null)
        {
            PushStatus("querying", "正在查询报告数据...");
            var report = await QueryReportAsync(accessNumber);
            if (report == null) { PushStatus("failed", "报告查询失败"); return null; }

            string findings = manualFindings ?? report.ReportContent ?? "";
            string impression = manualImpression ?? report.ReportDiagnosis ?? "";

 
              Logger.Info($"[RunFullQcAsync] accessNumber={accessNumber},findings={findings}, impression={impression}");


            // 报告内容为空时 OCR 截取
            if (string.IsNullOrWhiteSpace(findings) || string.IsNullOrWhiteSpace(impression))
            {
                Logger.Info($"[RunFullQcAsync] accessNumber={accessNumber},正在 OCR 识别报告内容...");

                PushStatus("ocr", "正在 OCR 识别报告内容...");
                var cfg = _config.Get();
                var contentAreas = cfg.Ocr.Areas
                    .Where(a => (a.Type == "report_content" || a.Type == "report_diagnosis") && a.Enabled).ToList();
                if (contentAreas.Count > 0)
                {
                    var contentResults = await RecognizeAsync(contentAreas);
                    foreach (var r in contentResults)
                    {
                        if (r.AreaId == "report_content" && string.IsNullOrWhiteSpace(findings)) findings = r.Text;
                        if (r.AreaId == "report_diagnosis" && string.IsNullOrWhiteSpace(impression)) impression = r.Text;
                    }
                }

                Logger.Info($"[RunFullQcAsync] accessNumber={accessNumber},findings_ocr={findings},impression_ocr={impression}");


            }

            PushStatus("analyzing", "正在进行分析...");
            var qcRequest = new QcRequest
            {
                ReportId = accessNumber,
                Findings = findings ?? "",
                Impression = impression ?? "",
                PatientName = report.PatientName,
                PatientGender = report.PatientGender,
                PatientAge = report.PatientAge,
                ClinicalDiagnosis = report.ClinicalHistory,
                RequestDepartment = report.Department,
                ExamPart = report.ExamBodyPart,
                AccessionNo = report.AccessNumber
            };

            if (!_reportQcOnline) { PushStatus("failed", "ReportQC 服务未启动"); return null; }

            var result = await ExecuteQcAsync(qcRequest);
            if (result != null)
            {
                _lastResult = result;
                _lastQcTime = DateTime.Now;
                PushQcResult(result);
                QcCompleted?.Invoke(result);
                PushStatus("completed", "质控完成");
            }
            else PushStatus("failed", "质控分析失败");
            return result;
        }

        private bool IsValidAccessNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 4 || text.Length > 32) return false;
            foreach (char c in text) if (!char.IsLetterOrDigit(c) && c != '-' && c != '_') return false;
            return true;
        }

        private async Task CheckConnectionStatus()
        {
            await CheckConnectionAsync();
        }

        /// <summary>手动触发质控</summary>
        public async Task<QcResponse> ManualTriggerAsync(string accessNumber)
        {
            if (!IsValidAccessNumber(accessNumber)) { PushStatus("failed", "无效的影像号"); return null; }
            _lastAccessNumber = accessNumber;
            return await RunFullQcAsync(accessNumber);
        }

        /// <summary>手动输入质控</summary>
        public async Task<QcResponse> ManualInputAsync(string findings, string impression)
        {
            PushStatus("analyzing", "正在进行分析...");
            var qcRequest = new QcRequest { ReportId = "manual-" + DateTime.Now.Ticks, Findings = findings ?? "", Impression = impression ?? "" };
            var result = await ExecuteQcAsync(qcRequest);
            if (result != null)
            {
                _lastResult = result;
                _lastQcTime = DateTime.Now;
                PushQcResult(result);
                QcCompleted?.Invoke(result);
                PushStatus("completed", "质控完成");
            }
            else PushStatus("failed", "质控分析失败");
            return result;
        }

        #endregion

        #region SSE

        public SseClient AddSseClient(StreamWriter writer)
        {
            var client = new SseClient { Writer = writer };
            lock (_sseLock) _sseClients.Add(client);
            return client;
        }

        public void RemoveSseClient(SseClient client)
        {
            lock (_sseLock) _sseClients.Remove(client);
        }

        private async void PushSse(string eventType, string data)
        {
            List<SseClient> clients;
            lock (_sseLock) clients = new List<SseClient>(_sseClients);
            foreach (var client in clients)
            {
                try
                {
                    await client.Writer.WriteLineAsync($"event: {eventType}");
                    await client.Writer.WriteLineAsync($"data: {data}");
                    await client.Writer.WriteLineAsync("");
                    await client.Writer.FlushAsync();
                }
                catch { client.Connected = false; RemoveSseClient(client); }
            }
        }

        public void PushQcResult(QcResponse result)
        {
            var json = JsonConvert.SerializeObject(new { type = "qc_result", data = result });
            Logger.Info("[DEBUG] SSE PushQcResult (first 300 chars): " +
                (json?.Length > 300 ? json.Substring(0, 300) : json));
            PushSse("qc_result", json);
        }

        public void PushConnectionStatus()
        {
            PushSse("connection", JsonConvert.SerializeObject(new { type = "connection", qcService = _qcServiceOnline, reportQc = _reportQcOnline }));
        }

        public void PushStatus(string status, string message)
        {
            PushSse("status", JsonConvert.SerializeObject(new { type = "status", status, message }));
        }

        #endregion

        public void Dispose()
        {
            _pollTimer?.Dispose();
            _http?.Dispose();
        }
    }
}
