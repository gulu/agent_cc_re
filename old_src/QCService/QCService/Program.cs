// QCService 入口
// 独立后端服务 — 提供 OCR 识别 + Oracle 报告查询接口
// 不与 ReportQC（报告质控后端）共享任何代码，独立运行

using FreeSql;
using FreeSql.Internal;
using QCService.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog 日志 ──
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "qcservice-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ── 控制器 ──
// ⚠ 使用默认 PascalCase 序列化（不做 camelCase 转换）
// QCClient (Newtonsoft.Json) 默认大小写敏感，必须保持 PascalCase 以匹配其模型属性名
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // 不设置 PropertyNamingPolicy → 保持 PascalCase（Code/Msg/Data/AreaId/Text 等）
        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── FreeSQL + Oracle 数据库配置 ──
var oracleConnStr = builder.Configuration.GetConnectionString("Oracle")
    ?? throw new InvalidOperationException("Oracle 连接字符串未配置");

var freeSql = new FreeSqlBuilder()
    .UseConnectionString(DataType.Oracle, oracleConnStr)
    .UseNameConvert(FreeSql.Internal.NameConvertType.PascalCaseToUnderscoreWithUpper)
    .Build();

builder.Services.AddSingleton(freeSql);

// ── 注册服务 ──
builder.Services.AddSingleton<OcrService>();
builder.Services.AddScoped<ReportQueryService>();

// ── CORS ──
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ── 中间件管道 ──
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// 启动时记录端口
var urls = app.Urls.FirstOrDefault() ?? "http://localhost:5200";
Log.Information("QCService 启动: {Urls}", urls);
Log.Information("OCR 引擎已加载（构造失败则服务无法启动）");

// 启动时检查 Oracle 连接
try
{
    using var scope = app.Services.CreateScope();
    var queryService = scope.ServiceProvider.GetRequiredService<ReportQueryService>();
    var connected = await queryService.CheckConnectionAsync();
    Log.Information("Oracle 数据库连接: {Status}", connected ? "正常" : "失败");
}
catch (Exception ex)
{
    Log.Warning("数据库连接检查失败（首次启动正常）: {Msg}", ex.Message);
}

app.Run();
