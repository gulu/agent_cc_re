@echo off
REM ============================================
REM QCClient 构建与发布脚本
REM 放射科报告质控客户端
REM 技术栈：.NET Framework 4.8 + WinForms + HttpListener
REM ============================================

setlocal enabledelayedexpansion

SET SOLUTION_DIR=%~dp0
SET PROJECT_DIR=%SOLUTION_DIR%QCClient
SET PROJECT_FILE=%PROJECT_DIR%QCClient.csproj

echo.
echo ════════════════════════════════════════
echo  报告质控客户端 - QCClient 构建脚本
echo ════════════════════════════════════════
echo.

REM 检查 .NET SDK
echo [检查] .NET SDK...
dotnet --version >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    echo [错误] 未找到 .NET SDK
    pause
    exit /b 1
)
echo [OK] .NET SDK 已安装

REM 构建项目
echo [步骤 1/2] 构建项目...
dotnet build "%PROJECT_FILE%" -c Debug --no-restore
IF %ERRORLEVEL% NEQ 0 (
    echo [错误] 构建失败
    pause
    exit /b 1
)
echo [OK] 构建成功
echo      输出: %PROJECT_DIR%\bin\Debug\net48\QCClient.exe

REM Release 构建
echo [步骤 2/2] Release 构建...
dotnet build "%PROJECT_FILE%" -c Release --no-restore
IF %ERRORLEVEL% NEQ 0 (
    echo [警告] Release 构建失败
) ELSE (
    echo [OK] Release 构建成功
    echo      输出: %PROJECT_DIR%\bin\Release\net48\QCClient.exe
)

echo.
echo ════════════════════════════════════════
echo  构建完成！
echo ════════════════════════════════════════
echo.

pause
