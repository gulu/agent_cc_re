using FreeSql;
using FreeSql.Internal;
using Agent_QC.Services;

var builder = WebApplication.CreateBuilder(args);

// ── 数据库配置 ──────────────────────────────────────
var dbConfig = builder.Configuration.GetSection("Database");
var dbType = (dbConfig["Type"] ?? "SQLite").ToUpperInvariant();

var dataType = dbType switch
{
    "POSTGRESQL" => DataType.PostgreSQL,
    "ORACLE" => DataType.Oracle,
    _ => DataType.Sqlite
};

var connStr = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    ?? dbConfig["ConnectionString"]
    ?? "Data Source=Data/qc.db";

if (dataType == DataType.Sqlite)
{
    var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
    Directory.CreateDirectory(dataDir);
    connStr = $"Data Source={Path.Combine(dataDir, "qc.db")}";
}

var freeSql = new FreeSqlBuilder()
    .UseConnectionString(dataType, connStr)
    .UseNameConvert(NameConvertType.PascalCaseToUnderscore)
    .Build();

builder.Services.AddSingleton(freeSql);

// ── QC 服务 ─────────────────────────────────────────
builder.Services.AddSingleton<IQcService, QcService>();

// ── vLLM + Skill Squad ────────────────────────────
var vllmEndpoint = builder.Configuration["Vllm:Endpoint"] ?? "http://localhost:8100";
var vllmModel = builder.Configuration["Vllm:Model"] ?? "/home/gulu/.cache/modelscope/hub/models/Qwen/Qwen3-4B-AWQ";
var vllmClient = new VllmClient(new HttpClient(), vllmEndpoint, vllmModel);
_ = vllmClient.CheckHealthAsync().ContinueWith(t =>
{
    var status = t.GetAwaiter().GetResult() ? "healthy" : "unavailable";
    Console.WriteLine($"[VllmClient] vLLM health check: {status}");
});
builder.Services.AddSingleton<IVllmClient>(vllmClient);
builder.Services.AddSingleton(new SkillRegistry("knowledge/skills"));
builder.Services.AddSingleton<HermesOrchestrator>();
builder.Services.AddSingleton<QaArbiter>();

// ── 控制器 ─────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── CORS ───────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
