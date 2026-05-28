# Agent_QC 完整部署手册

## 系统架构

```
┌─────────────────────┐         ┌─────────────────────┐         ┌─────────────────────┐
│   QCClient          │  HTTP   │   Agent_QC          │  Oracle │   Oracle 11g        │
│   (Windows 桌面端)   │ ←───→ │   (Linux 服务端)     │ ←───→ │   (RIS 数据库)       │
│                     │  API    │                     │         │                      │
│  - OCR 截图识别      │         │  - QC 质控管线       │   vLLM  │  VIEW_QC_REPORT     │
│  - WebView2 UI      │         │  - Skill Squad      │ ←───→ │                      │
│  - 系统托盘          │         │  - Oracle 查询      │  GPU   │                      │
└─────────────────────┘         └─────────────────────┘         └─────────────────────┘
  Port: 动态 (127.0.0.1)        Port: 5263                       Port: 1521
```

## 一、服务端部署 (Agent_QC / Linux x64)

### 1.1 系统要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Ubuntu 22.04+ / WSL2 |
| CPU | 4 核+ |
| 内存 | 8 GB+ |
| 磁盘 | 2 GB |
| GPU | NVIDIA + CUDA 12.x (可选, LLM Skill Squad) |
| .NET | 无需安装 (自包含发布) |

### 1.2 解压部署包

```bash
tar xzf agent_qc_v1.0.0_linux-x64.tar.gz -C /opt/Agent_QC/
cd /opt/Agent_QC
```

### 1.3 修改配置

编辑 `appsettings.json`:

```json
{
  "Database": {
    "Type": "SQLite",
    "OracleConnectionString": "user id=ris;password=ris;data source=//192.168.62.122:1521/orcl;Pooling=true;Min Pool Size=1"
  },
  "Vllm": {
    "Endpoint": "http://localhost:8100",
    "Model": "/data/models/QuantTrio/Qwen3.5-2B-AWQ"
  }
}
```

| 参数 | 说明 |
|------|------|
| `OracleConnectionString` | Oracle 连接串: `user id=用户;password=密码;data source=//主机:端口/服务名;Pooling=true;Min Pool Size=1` |
| `Vllm:Endpoint` | vLLM 地址 (无 GPU 可忽略此节) |
| `Vllm:Model` | 模型完整路径 (须与 vLLM 启动参数一致) |

### 1.4 安装 vLLM (可选, 需 GPU)

```bash
pip install vllm

export VLLM_BUILD_WITH_CUSTOM_OPS=0 或手动安装一次 cuda 
sudo apt update
sudo apt install nvidia-cuda-toolkit

# WSL2 必须设置环境变量
env VLLM_USE_V1=0 vllm serve /data/models/Qwen3.5-2B-AWQ \
  --host 0.0.0.0 \
  --port 8100 \
  --max-model-len 2048 \
  --gpu-memory-utilization 0.8 \
  --enforce-eager
```

### 1.5 启动 Agent_QC

```bash
# ⚠ 必须先清除代理! Oracle 驱动会被 http_proxy 干扰
unset http_proxy https_proxy HTTP_PROXY HTTPS_PROXY

# 前台运行
chmod +x Agent_QC
chmod +x start.sh
./start.sh

# 后台运行
nohup ./start.sh > logs/agent_qc.log 2>&1 &

# 自定义端口
./start.sh 5200
```

### 1.6 验证

```bash
# 健康检查
curl http://localhost:5263/api/v1/qc/query-report?accessNumber=81982632

# 返回示例
{"code":200,"msg":"success","data":{"patientName":"测试",...}}
```

### 1.7 日志关键词

| 日志 | 含义 |
|------|------|
| `[Oracle] FreeSql instance created` | Oracle 连接正常 |
| `[VllmClient] vLLM health check: healthy` | vLLM Skill Squad 可用 |
| `[VllmClient] vLLM health check: unavailable` | 自动降级: 仅规则层运行 |

---

## 二、客户端部署 (QCClient / Windows)

### 2.1 系统要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 / 11 |
| 运行时 | .NET Framework 4.8 |
| 浏览器内核 | WebView2 Runtime |
| 网络 | 可访问 Agent_QC 服务端 (同一局域网) |

### 2.2 安装依赖

**WebView2 Runtime** (Windows 11 已内置, Windows 10 需安装):
```
https://go.microsoft.com/fwlink/p/?LinkId=2124703
```

**.NET Framework 4.8** (Windows 10+ 已内置):
```
https://dotnet.microsoft.com/download/dotnet-framework/net48
```

### 2.3 构建 (开发环境)

**方式一: Visual Studio 2022**

```
1. 打开 src/QCClient/QCClient.sln
2. 选择 Release 配置
3. 菜单: 生成 → 生成解决方案
4. 输出: src/QCClient/src/QCClient/bin/Release/
```

**方式二: 命令行**

```batch
cd src/QCClient
build.bat
```

### 2.4 安装部署

```
1. 复制 build.bat 输出目录 (publish\) 到目标电脑，如 C:\Program Files\QCClient\
2. 修改 appsettings.json 中的 BackendSettings:Url 指向 Agent_QC 服务端
3. 创建桌面快捷方式: C:\Program Files\QCClient\QCClient.exe
```

### 2.5 配置 OCR 截图区域

QCClient 通过 OCR 自动识别屏幕上的影像号区域:

1. 打开 PACS 系统，显示一份报告
2. 打开 QCClient 网页界面
3. 设置 → OCR 区域配置 → 框选影像号区域
4. 保存配置

### 2.6 配置文件说明

`appsettings.json`:

```json
{
  "BackendSettings": {
    "Url": "http://192.168.x.x:5263",   // ← Agent_QC 服务端地址 ★
    "ApiTimeoutSeconds": 30
  },
  "OcrSettings": {
    "DefaultIntervalSeconds": 5,
    "IdleSeconds": 2,
    "Areas": []                         // ← OCR 截图区域 (通过 UI 配置)
  },
  "WebSettings": {
    "EnableNotification": true,
    "SidebarWidth": 320,
    "AlwaysOnTop": true,                // 窗口置顶
    "EnableSound": true,                // 提示音
    "Theme": "light"
  }
}
```

| 参数 | 说明 |
|------|------|
| `BackendSettings:Url` | **必改** — Agent_QC 服务端地址 |
| `WebSettings:AlwaysOnTop` | QC 分析窗口置顶显示 |
| `WebSettings:EnableSound` | 质控完成播放提示音 |

---

## 三、端到端验证

### 3.1 服务端验证

```bash
# 1. Oracle 查询
curl http://{服务端IP}:5263/api/v1/qc/query-report?accessNumber=81982632

# 2. QC 分析
curl -X POST http://{服务端IP}:5263/api/v1/qc/report \
  -H "Content-Type: application/json" \
  -d '{"reportId":"test","findings":"双肺纹理增多。","impression":"双肺纹理增多","reportType":"CT"}'
```

### 3.2 客户端验证

1. 启动 QCClient.exe → 系统托盘出现图标
2. 浏览器自动打开 `http://127.0.0.1:{端口}/`
3. 界面显示 "服务在线" (绿色)
4. 点击 "手动输入" → 输入报告内容 → 验证分析结果

---

## 四、故障排查

### Oracle 连接失败 (ORA-50201)

**症状**: 日志显示 `ORA-50201: Oracle Communication`  
**原因**: 代理环境变量干扰 Oracle Managed Driver  
**解决**: `unset http_proxy https_proxy HTTP_PROXY HTTPS_PROXY` 后重启

### vLLM Skill Squad 无输出

**症状**: QC 结果没有 LLM 标记的问题  
**原因**: vLLM 不可用 (自动降级)  
**检查**: 
- vLLM 是否运行: `curl http://localhost:8100/health`
- 模型路径与配置文件一致
- GPU 显存: `nvidia-smi`

### QCClient 连接不上服务端

**症状**: 界面显示 "后端离线"  
**检查**:
- Agent_QC 已启动: `curl http://{服务端IP}:5263/api/v1/health`
- 防火墙放行 5263 端口
- `appsettings.json` 中 BackendSettings:Url 正确

### OCient 启动崩溃

**症状**: 闪退或报错  
**检查**:
- 已安装 WebView2 Runtime
- 已安装 .NET Framework 4.8
- 查看日志: `logs/qcclient-*.log`

---

## 五、文件清单

### Agent_QC (服务端)

```
/opt/Agent_QC/
├── Agent_QC               # 主程序
├── start.sh               # 启动脚本
├── appsettings.json       # 配置文件 ★
├── DEPLOY.md              # 部署手册
├── knowledge/
│   ├── skills/            # 8 个 Skill Prompt 模板
│   ├── terminology.yaml   # 术语词典
│   ├── rads-standards.yaml
│   └── knowledge-base.yaml
├── Data/qc.db             # SQLite 数据库 (自动生成)
└── logs/                  # 日志目录 (自动生成)
```

### QCClient (客户端)

```
C:\Program Files\QCClient\
├── QCClient.exe            # 主程序
├── appsettings.json        # 配置文件 ★
├── Newtonsoft.Json.dll
├── Serilog.dll
├── Microsoft.Web.WebView2.*.dll
└── wwwroot/
    ├── index.html          # Web UI
    ├── app.js
    └── style.css
```

---

## 六、安全建议

- 生产环境修改数据库密码（`appsettings.json` 中的 Oracle 连接串）
- 建议使用 `DB_CONNECTION_STRING` 环境变量替代明文密码
- 服务端端口不要暴露到公网，仅内网访问
- QCClient 仅监听 `127.0.0.1`，不对外暴露
- 患者数据传输中注意脱敏
