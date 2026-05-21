#!/bin/bash
# Agent_QC 基线测试脚本
# 运行完整验证门禁：测试 + 构建 + 格式化 + 覆盖率
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SRC_DIR="$PROJECT_DIR/src/Agent_QC"

echo "============================================"
echo " Agent_QC 验证门禁"
echo "============================================"

# 1. 格式化检查
echo ""
echo "[1/4] 格式化检查..."
dotnet format "$SRC_DIR/src/" --verify-no-changes --verbosity quiet
echo "  ✓ 格式化通过"

# 2. Release 构建
echo ""
echo "[2/4] Release 构建..."
dotnet build "$SRC_DIR/src/Agent_QC.csproj" --configuration Release --nologo
echo "  ✓ 构建通过"

# 3. 单元测试
echo ""
echo "[3/4] 单元测试..."
dotnet test "$SRC_DIR/tests/Agent_QC.Tests.csproj" \
    --configuration Release \
    --nologo \
    --verbosity normal
echo "  ✓ 测试通过"

# 4. 覆盖率（如果 coverlet 可用）
echo ""
echo "[4/4] 覆盖率检查..."
if dotnet tool list --global 2>/dev/null | grep -q coverlet; then
    coverlet "$SRC_DIR/tests/bin/Release/net8.0/Agent_QC.Tests.dll" \
        --target dotnet \
        --targetargs "test $SRC_DIR/tests/Agent_QC.Tests.csproj --configuration Release --no-build" \
        --format opencover \
        --threshold 80 \
        --output "$SRC_DIR/coverage/"
    echo "  ✓ 覆盖率 ≥ 80%"
else
    echo "  ⚠ coverlet 未安装（全局），跳过覆盖率"
    echo "  安装: dotnet tool install -g coverlet.console"
fi

echo ""
echo "============================================"
echo " 验证门禁全部通过"
echo "============================================"
