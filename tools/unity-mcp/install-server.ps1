$ErrorActionPreference = 'Stop'
$taskProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $taskProjectRoot
try {
    python -m venv .local-tools/unity-mcp/venv
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось создать Python venv.' }
    & ./.local-tools/unity-mcp/venv/Scripts/python.exe -m pip install 'mcpforunityserver==10.0.0' 'uv>=0.8,<1'
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось установить MCP server.' }
    Set-Content -LiteralPath .local-tools/unity-mcp/enabled -Value 'Unity MCP 10.0.0 local connection'
} finally { Pop-Location }
