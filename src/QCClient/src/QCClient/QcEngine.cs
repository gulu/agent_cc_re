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

        public static double GetIdleSeconds() => GetIdleMilliseconds() / 1000.0;
    }

    #endregion

    #region API 模型

    public class ApiResponse<T>
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public T Data { get; set; }
        public bool IsOk => Code == 200;
    }

    public class QcRequestDto
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

        [JsonIgnore] public string Message => Description ?? IssueType ?? "";
    }

    public class OcrRecognizeResponse
    {
        public string AreaId { get; set; }
        public string Text { get; set; }
        public bool Success { get; set; }
        public double Confidence { get; set; }
        public int ProcessTimeMs { get; set; }
        public string ErrorMessage { get; set; }
    }

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

    public class QcEngine : IDisposable
    {
        private readonly ConfigHelper _config;
        private readonly HttpClient _http;
        private Timer _pollTimer;
        private bool _polling;
        private int _pollIntervalMs = 5000;

        private readonly List<SseClient> _sseClients = new();
        private readonly object _sseLock = new();

        private string _lastAccessNumber = "";
        private DateTime _lastQcTime = DateTime.MinValue;

        private bool _backendOnline;
        private QcResponse _lastResult;

        public event Action<QcResponse> QcCompleted;
        public event Action<bool> ConnectionStatusChanged;

        public QcEngine(ConfigHelper config)
        {
            _config = config;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(config.Get().Backend.ApiTimeoutSeconds) };
        }

        public bool BackendOnline => _backendOnline;
        public QcResponse LastResult => _lastResult;
        public string LastAccessNumber => _lastAccessNumber;

        #region 连接检测

        public async Task CheckConnectionAsync()
        {
            var url = _config.Get().Backend.Url;
            bool ok = await PingAsync(url);
            if (ok != _backendOnline)
            {
                _backendOnline = ok;
                PushConnectionStatus();
                ConnectionStatusChanged?.Invoke(ok);
            }
        }

        private async Task<bool> PingAsync(string baseUrl)
        {
            try
            {
                var resp = await _http.GetAsync($"{baseUrl}/api/v1/health");
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

        public string CaptureAreaToBase64(int x, int y, int width, int height)
        {
            using (var bmp = CaptureArea(x, y, width, height))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        public async Task<object> TestOcrAreaAsync(OcrAreaConfig area)
        {
            var result = new Dictionary<string, object>
            {
                ["areaName"] = area.Name,
                ["x"] = area.X, ["y"] = area.Y,
                ["width"] = area.Width, ["height"] = area.Height,
                ["imageBase64"] = CaptureAreaToBase64(area.X, area.Y, area.Width, area.Height)
            };

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

        public async Task<List<OcrRecognizeResponse>> RecognizeAsync(List<OcrAreaConfig> areas)
        {
            var results = new List<OcrRecognizeResponse>();
            var cfg = _config.Get();

            foreach (var area in areas.Where(a => a.Enabled))
            {
                try
                {
                    using (var bmp = CaptureArea(area.X, area.Y, area.Width, area.Height))
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        ms.Seek(0, SeekOrigin.Begin);

                        var form = new MultipartFormDataContent();
                        var imageContent = new StreamContent(ms);
                        imageContent.Headers.ContentType =
                            new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                        form.Add(imageContent, "image", $"{area.Name}.png");
                        form.Add(new StringContent(area.Name), "areaId");
                        form.Add(new StringContent(area.Type ?? ""), "areaType");

                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        var resp = await _http.PostAsync(
                            $"{cfg.Backend.Url}/api/v1/ocr/recognize", form, cts.Token);
                        if (resp.IsSuccessStatusCode)
                        {
                            var json = await resp.Content.ReadAsStringAsync();
                            var apiResp = JsonConvert.DeserializeObject<ApiResponse<OcrRecognizeResponse>>(json);
                            if (apiResp?.IsOk == true && apiResp.Data != null)
                            {
                                apiResp.Data.AreaId = area.Name;
                                results.Add(apiResp.Data);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Logger.Warning(ex, "OCR failed for " + area.Name); }
            }
            return results;
        }

        public async Task<ReportInfo> QueryReportAsync(string accessNumber)
        {
            try
            {
                var cfg = _config.Get();
                var resp = await _http.GetAsync(
                    $"{cfg.Backend.Url}/api/v1/qc/query-report?accessNumber={Uri.EscapeDataString(accessNumber)}");
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

        public async Task<QcResponse> ExecuteQcAsync(QcRequestDto request)
        {
            try
            {
                var cfg = _config.Get();
                var payload = JsonConvert.SerializeObject(request);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync($"{cfg.Backend.Url}/api/v1/qc/report", content);
                if (!resp.IsSuccessStatusCode) return null;

                var json = await resp.Content.ReadAsStringAsync();

                // AjaxResult {code, data, msg} — Agent_QC 输出 camelCase
                var jObj = JObject.Parse(json);
                var code = jObj["code"]?.Value<int>() ?? 0;
                if (code != 200) return null;

                var dataToken = jObj["data"];
                if (dataToken == null || dataToken.Type == JTokenType.Null) return null;

                return dataToken.ToObject<QcResponse>();
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

                int idleSec = cfg.Ocr.IdleSeconds;
                if (idleSec > 0)
                {
                    double idle = User32.GetIdleSeconds();
                    if (idle < idleSec) return;
                    if ((DateTime.Now - _lastOcrTriggerTime).TotalSeconds < idleSec * 2) return;
                }

                await CheckConnectionAsync();
                if (!_backendOnline) { PushStatus("offline", "后端离线"); return; }

                var accessAreas = cfg.Ocr.Areas.Where(a => a.Type == "access_number" && a.Enabled).ToList();
                if (accessAreas.Count == 0) return;

                if (idleSec > 0) PushStatus("idle", "检测到空闲，正在 OCR 识别...");

                _lastOcrTriggerTime = DateTime.Now;

                var results = await RecognizeAsync(accessAreas);
                if (results.Count == 0)
                {
                    PushStatus("waiting", "OCR 未识别到有效影像号");
                    return;
                }

                var ocrText = (results[0].Text ?? "").Trim();
                if (!IsValidAccessNumber(ocrText)) return;

                if (ocrText == _lastAccessNumber && (DateTime.Now - _lastQcTime).TotalMinutes < 5)
                    return;

                _lastAccessNumber = ocrText;
                _lastOcrAccessNumber = ocrText;

                await RunFullQcAsync(ocrText);
            }
            catch (Exception ex) { Logger.Error(ex, "Auto QC error"); }
            finally { _polling = false; }
        }

        private async Task<QcResponse> RunFullQcAsync(string accessNumber,
            string manualFindings = null, string manualImpression = null)
        {
            PushStatus("querying", "正在查询报告数据...");
            var report = await QueryReportAsync(accessNumber);
            if (report == null) { PushStatus("failed", "报告查询失败"); return null; }

            string findings = manualFindings ?? report.ReportContent ?? "";
            string impression = manualImpression ?? report.ReportDiagnosis ?? "";

            if (string.IsNullOrWhiteSpace(findings) || string.IsNullOrWhiteSpace(impression))
            {
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
            }

            PushStatus("analyzing", "正在进行分析...");
            var qcRequest = new QcRequestDto
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

            if (!_backendOnline) { PushStatus("failed", "Agent_QC 后端未启动"); return null; }

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

        private static bool IsValidAccessNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 4 || text.Length > 32) return false;
            foreach (char c in text) if (!char.IsLetterOrDigit(c) && c != '-' && c != '_') return false;
            return true;
        }

        public async Task<QcResponse> ManualTriggerAsync(string accessNumber)
        {
            if (!IsValidAccessNumber(accessNumber)) { PushStatus("failed", "无效的影像号"); return null; }
            _lastAccessNumber = accessNumber;
            return await RunFullQcAsync(accessNumber);
        }

        public async Task<QcResponse> ManualInputAsync(string findings, string impression)
        {
            PushStatus("analyzing", "正在进行分析...");
            var qcRequest = new QcRequestDto
            {
                ReportId = "manual-" + DateTime.Now.Ticks,
                Findings = findings ?? "",
                Impression = impression ?? ""
            };
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
            PushSse("qc_result", json);
        }

        public void PushConnectionStatus()
        {
            PushSse("connection", JsonConvert.SerializeObject(
                new { type = "connection", online = _backendOnline }));
        }

        public void PushStatus(string status, string message)
        {
            PushSse("status", JsonConvert.SerializeObject(
                new { type = "status", status, message }));
        }

        #endregion

        public void Dispose()
        {
            _pollTimer?.Dispose();
            _http?.Dispose();
        }
    }
}
