// AI_QC-system 后端入口
// 配置源（优先级）：环境变量 > appsettings.json > 默认值

using FreeSql;
using FreeSql.Internal;
using ReportQC.Services;

var builder = WebApplication.CreateBuilder(args);

// ── 基础设施注册 ──────────────────────────────────

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── 数据库配置（从 appsettings.json 读取）────────

var dbConfig = builder.Configuration.GetSection("Database");
var dbTypeStr = dbConfig["Type"] ?? "SQLite";

// 字符串 → FreeSql DataType
var dataType = dbTypeStr.ToUpperInvariant() switch
{
    "POSTGRESQL"  => DataType.PostgreSQL,
    "ORACLE"      => DataType.Oracle,
    "SQLSERVER"   => DataType.SqlServer,
    "MYSQL"       => DataType.MySql,
    _             => DataType.Sqlite
};

// 连接字符串：环境变量 > appsettings.json > 默认
var rawConnStr = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                 ?? dbConfig["ConnectionString"]
                 ?? "Data Source=Data/qc.db";

// 确保 Data 目录存在于程序基目录下
var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
Directory.CreateDirectory(dataDir);

// SQLite 使用绝对路径防止工作目录歧义；其他数据库直用原串
var connStr = dataType == DataType.Sqlite
    ? $"Data Source={Path.Combine(dataDir, "qc.db")}"
    : rawConnStr;

var freeSql = new FreeSqlBuilder()
    .UseConnectionString(dataType, connStr)
    .UseAutoSyncStructure(true)
    .UseNameConvert(NameConvertType.PascalCaseToUnderscore)
    .Build();

builder.Services.AddSingleton(freeSql);

// 日志系统初始化（文件 + 数据库）
JSBaseLogs.Initialize(freeSql);

// 种子数据初始化（仅首次运行）
DbSeed.Initialize(freeSql);

// 尝试加载 TinyBERT 模型（文件不存在时静默跳过，走规则兜底）
TinyBertService.TryLoad();

// CORS
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

// ── 管道配置 ──────────────────────────────────────

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// wwwroot 静态文件（首页展示产品介绍）
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = new[] { "product.html", "index.html" }
});
app.UseStaticFiles();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// 启动后自动打开浏览器
app.Lifetime.ApplicationStarted.Register(() =>
{
    var url = "http://localhost:5100";
    try
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }
    catch
    {
        // 部分环境不支持自动打开浏览器
    }
});

app.Run();
