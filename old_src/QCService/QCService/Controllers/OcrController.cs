// OCR 识别控制器
// POST /api/v1/ocr/recognize — 接收截屏图片，返回 OCR 文字
// POST /api/v1/ocr/batch-recognize — 批量识别

using Microsoft.AspNetCore.Mvc;
using QCService.Models;
using QCService.Services;

namespace QCService.Controllers;

[ApiController]
[Route("api/v1/ocr")]
public class OcrController : ControllerBase
{
    private readonly OcrService _ocrService;
    private readonly ILogger<OcrController> _logger;

    public OcrController(OcrService ocrService, ILogger<OcrController> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
    }

    [HttpPost("recognize")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Recognize([FromForm] string? areaId, [FromForm] string? areaType, IFormFile? image)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (image == null || image.Length == 0)
            return Ok(ApiResult.Error("图片不能为空"));

        if (image.Length > 10 * 1024 * 1024)
            return Ok(ApiResult.Error("图片大小不能超过 10MB"));

        var contentType = image.ContentType?.ToLowerInvariant() ?? "";
        if (!contentType.StartsWith("image/"))
            return Ok(ApiResult.Error("只支持图片文件"));

        areaId = string.IsNullOrWhiteSpace(areaId) ? Guid.NewGuid().ToString("N") : areaId;
        areaType = (areaType ?? "").Trim().ToLowerInvariant();

        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        var imageData = ms.ToArray();

        _logger.LogInformation("[OCR] RECV AreaId={AreaId} Type={Type} Size={Size}bytes ContentType={Ct} FileName={File}",
            areaId, areaType, imageData.Length, contentType, image.FileName ?? "none");

        // 诊断：保存原始截图（便于排查坐标/截取问题）
        try
        {
            var debugDir = Path.Combine(AppContext.BaseDirectory, "ocr_debug");
            Directory.CreateDirectory(debugDir);
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var debugPath = Path.Combine(debugDir, $"{ts}_{areaId}.png");
            await System.IO.File.WriteAllBytesAsync(debugPath, imageData);
            _logger.LogInformation("[OCR] DEBUG saved: {Path}", debugPath);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "[OCR] DEBUG save failed"); }

        var result = await _ocrService.RecognizeAsync(areaId, imageData, areaType);
        sw.Stop();

        _logger.LogInformation("[OCR] OUT AreaId={AreaId} Success={Success} TextLen={Len} Conf={Conf:P2} Time={Time}ms Err={Err}",
            areaId, result.Success, result.Text?.Length ?? 0, result.Confidence, sw.ElapsedMilliseconds, result.ErrorMessage ?? "none");

        return Ok(ApiResult.Success(result));
    }

    [HttpPost("batch-recognize")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> BatchRecognize(List<IFormFile> images)
    {
        if (images == null || images.Count == 0)
            return Ok(ApiResult.Error("未上传图片"));

        if (images.Count > 20)
            return Ok(ApiResult.Error("单次最多识别 20 个区域"));

        var results = new List<OcrRecognizeResponse>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var image in images)
        {
            using var ms = new MemoryStream();
            await image.CopyToAsync(ms);
            var imageData = ms.ToArray();

            var areaId = Path.GetFileNameWithoutExtension(image.FileName);
            if (string.IsNullOrWhiteSpace(areaId))
                areaId = Guid.NewGuid().ToString("N");

            var result = await _ocrService.RecognizeAsync(areaId, imageData);
            results.Add(result);
        }

        sw.Stop();

        return Ok(ApiResult.Success(new BatchOcrResponse
        {
            Results = results,
            TotalProcessTimeMs = (int)sw.ElapsedMilliseconds
        }));
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(ApiResult.Success(new
        {
            engineAvailable = _ocrService.IsEngineAvailable(),
            timestamp = DateTime.Now
        }));
    }
}
