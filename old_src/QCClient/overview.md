# QCClient 重构完成

## 架构变更

| 项目 | 旧架构 | 新架构 |
|------|--------|--------|
| 框架 | .NET 8.0-windows | .NET Framework 4.8 |
| Web 服务器 | Kestrel (ASP.NET Core) | HttpListener (系统内置) |
| JSON | System.Text.Json | Newtonsoft.Json |
| 项目模板 | SDK Web | SDK WinForms (WinExe) |
| 分层架构 | Controllers/Services/Models/Utils | 5 个扁平化源文件 |
| 依赖管理 | NuGet PackageReference | lib/ 本地 DLL 引用 |
| UI | 浏览器侧边栏 | WinForms 浮动面板 + 浏览器降级 |

## 新项目结构

```
QCClient/
├── Program.cs          # 入口 + HttpListener 路由
├── MainForm.cs         # 浮动面板 (底部右侧)
├── TrayService.cs      # 系统托盘 + 气泡通知
├── QcEngine.cs         # 质控引擎 (截图 + API + 轮询 + SSE)
├── ConfigHelper.cs     # 配置读写
├── wwwroot/            # 前端 (HTML/CSS/JS)
├── appsettings.json
└── QCClient.csproj
```

## 构建状态

- ✅ **0 错误 / 0 警告**
- 输出: `bin\Debug\net48\QCClient.exe` (74KB)
- 构建命令: `dotnet build --no-restore`
