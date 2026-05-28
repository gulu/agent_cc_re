@echo off
REM QCClient 构建脚本 (需 .NET Framework 4.8 SDK 或 Visual Studio 2022)
REM 生成发布目录: publish\

echo === QCClient 构建 ===

where msbuild >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] 未找到 MSBuild, 请安装 Visual Studio 2022 或 .NET Framework 4.8 SDK
    pause
    exit /b 1
)

if exist publish\ rmdir /s /q publish
mkdir publish

echo [1/3] 还原 NuGet 包...
nuget restore ..\..\QCClient.sln 2>nul || dotnet restore ..\..\QCClient.sln 2>nul

echo [2/3] 编译 Release...
msbuild src\QCClient\QCClient.csproj /p:Configuration=Release /p:Platform="Any CPU" /t:Rebuild

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] 编译失败
    pause
    exit /b 1
)

echo [3/3] 收集发布文件...
copy src\QCClient\bin\Release\*.exe publish\ >nul 2>&1
copy src\QCClient\bin\Release\*.dll publish\ >nul 2>&1
copy src\QCClient\bin\Release\*.json publish\ >nul 2>&1
copy src\QCClient\bin\Release\*.config publish\ >nul 2>&1
xcopy /e /i /y src\QCClient\bin\Release\wwwroot publish\wwwroot >nul 2>&1

echo === 构建完成 ===
echo 发布目录: %CD%\publish\
echo.
echo 运行: publish\QCClient.exe
echo 依赖: WebView2 Runtime (https://go.microsoft.com/fwlink/p/?LinkId=2124703)
pause
