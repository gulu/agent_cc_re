// OCR 识别服务 — 基于 Tesseract 的生产级文字识别引擎
// 接收上传的截屏图片，预处理后执行 OCR 文字识别
// 生产系统：引擎加载失败或识别失败时直接返回错误，不做任何模拟降级

using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using QCService.Models;

namespace QCService.Services;

public class OcrService : IDisposable
{
    private readonly Tesseract.TesseractEngine _engine;
    private readonly ILogger<OcrService> _logger;
    private readonly string _globalWhitelist;
    private bool _disposed;

    public OcrService(IConfiguration configuration, ILogger<OcrService> logger)
    {
        _logger = logger;
        var tessDataPath = configuration.GetValue<string>("Ocr:TessDataPath")
            ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
        var lang = configuration.GetValue<string>("Ocr:Language") ?? "chi_sim";

        _logger.LogInformation("[OCR] INIT Lang={Lang} DataPath={Path} Exists={Exists}",
            lang, tessDataPath, Directory.Exists(tessDataPath));

        _engine = new Tesseract.TesseractEngine(tessDataPath, lang, Tesseract.EngineMode.Default);
        _globalWhitelist = configuration.GetValue<string>("Ocr:CharWhitelist") ?? "";
        if (!string.IsNullOrEmpty(_globalWhitelist))
            _engine.SetVariable("tessedit_char_whitelist", _globalWhitelist);

        _logger.LogInformation("[OCR] READY Lang={Lang} GlobalWhitelist='{Whitelist}'", lang, _globalWhitelist);
    }

    public async Task<OcrRecognizeResponse> RecognizeAsync(string areaId, byte[] imageData, string areaType = "")
    {
        var totalSw = Stopwatch.StartNew();
        var stepSw = new Stopwatch();

        // 类型感知：access_number 使用数字专用配置
        bool isAccessNumber = areaType == "access_number";

        try
        {
            // 步骤1: 图片预处理
            stepSw.Restart();
            byte[] processedImage;
            try
            {
                processedImage = await PreprocessImageAsync(imageData, isAccessNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OCR] PREPROCESS failed AreaId={AreaId} Bytes={Bytes}", areaId, imageData.Length);
                return FailResponse(areaId, "图片预处理失败: " + ex.Message, totalSw.ElapsedMilliseconds);
            }
            var prepMs = stepSw.ElapsedMilliseconds;
            _logger.LogDebug("[OCR] PREPROCESS ok AreaId={AreaId} Type={Type} In={InLen}bytes Out={OutLen}bytes Time={Time}ms",
                areaId, areaType, imageData.Length, processedImage.Length, prepMs);

            // 步骤2: Tesseract OCR — 类型感知 PSM + 字符白名单
            stepSw.Restart();
            string text;
            double confidence;
            try
            {
                using var pix = Tesseract.Pix.LoadFromMemory(processedImage);

                if (isAccessNumber)
                {
                    // ★ 影像号专用：数字+字母白名单，单行模式
                    _engine.SetVariable("tessedit_char_whitelist", "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");
                    using var page = _engine.Process(pix, Tesseract.PageSegMode.SingleLine);
                    text = page.GetText()?.Trim() ?? string.Empty;
                    confidence = page.GetMeanConfidence();
                    _engine.SetVariable("tessedit_char_whitelist", ""); // 恢复默认，不影响其他类型
                }
                else
                {
                    // 中文报告内容：恢复全局白名单配置
                    _engine.SetVariable("tessedit_char_whitelist", _globalWhitelist);
                    using var page = _engine.Process(pix, Tesseract.PageSegMode.Auto);
                    text = page.GetText()?.Trim() ?? string.Empty;
                    confidence = page.GetMeanConfidence();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OCR] TESSERACT failed AreaId={AreaId} ProcessedBytes={Bytes}", areaId, processedImage.Length);
                return FailResponse(areaId, "Tesseract 识别失败: " + ex.Message, totalSw.ElapsedMilliseconds);
            }
            var ocrMs = stepSw.ElapsedMilliseconds;

            // 步骤3: 后处理清理
            if (isAccessNumber && !string.IsNullOrEmpty(text))
            {
                var raw = text;
                text = CleanAccessNumber(text);
                if (text != raw)
                    _logger.LogInformation("[OCR] POSTPROCESS AreaId={AreaId} Raw='{Raw}' Cleaned='{Cleaned}'",
                        areaId, raw, text);
            }

            totalSw.Stop();

            _logger.LogInformation("[OCR] OK AreaId={AreaId} Type={Type} Text='{Text}' Conf={Conf:P2} Prep={Prep}ms OCR={Ocr}ms Total={Total}ms Len={Len}",
                areaId, areaType, text.Length > 80 ? text[..80] + "..." : text, confidence, prepMs, ocrMs, totalSw.ElapsedMilliseconds, text.Length);

            return new OcrRecognizeResponse
            {
                AreaId = areaId,
                Text = text,
                Success = true,
                Confidence = confidence,
                ProcessTimeMs = (int)totalSw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            totalSw.Stop();
            _logger.LogError(ex, "[OCR] FATAL AreaId={AreaId} Time={Time}ms", areaId, totalSw.ElapsedMilliseconds);

            return FailResponse(areaId, ex.Message, totalSw.ElapsedMilliseconds);
        }
    }

    private static OcrRecognizeResponse FailResponse(string areaId, string error, long timeMs)
    {
        return new OcrRecognizeResponse
        {
            AreaId = areaId,
            Text = string.Empty,
            Success = false,
            Confidence = 0,
            ProcessTimeMs = (int)timeMs,
            ErrorMessage = error
        };
    }

    private static async Task<byte[]> PreprocessImageAsync(byte[] imageData, bool isAccessNumber = false)
    {
        return await Task.Run(() =>
        {
            using var image = Image.Load(imageData);

            // 灰度化
            image.Mutate(x => x.Grayscale());

            if (isAccessNumber)
            {
                // ★ 影像号专用：放大 2 倍提高识别率 + 温和对比度增强
                image.Mutate(x => x.Resize(image.Width * 2, image.Height * 2, KnownResamplers.Lanczos3)
                                  .Contrast(1.05f));
                // 不做 BinaryThreshold，保留抗锯齿像素信息供 Tesseract 分析
            }
            else
            {
                // 中文报告：对比度增强 + 二值化
                image.Mutate(x => x.Contrast(1.2f).BinaryThreshold(0.5f));
            }

            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        });
    }

    public bool IsEngineAvailable() => !_disposed;

    /// <summary>清理影像号识别结果：去除非数字字母字符，修正常见误判</summary>
    private static string CleanAccessNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // 1. 移除空白、标点、特殊符号
        var sb = new System.Text.StringBuilder();
        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }

        var cleaned = sb.ToString();

        // 2. 如果只剩太少的字符，返回原始结果
        if (cleaned.Length < 2) return text.Trim();

        return cleaned;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _engine.Dispose();
            _disposed = true;
        }
    }
}
