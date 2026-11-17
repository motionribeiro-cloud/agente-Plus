$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compilerPath)) { throw 'Compilador .NET Framework não encontrado neste Windows.' }
& python (Join-Path $projectRoot 'prepare_payloads.py')
if ($LASTEXITCODE -ne 0) { throw 'Falha na preparação dos pacotes.' }
& python (Join-Path $projectRoot 'test_mcp.py')
if ($LASTEXITCODE -ne 0) { throw 'Testes MCP/Claude Python falharam.' }
& python (Join-Path $projectRoot 'test_knowledge.py')
if ($LASTEXITCODE -ne 0) { throw 'Testes das bibliotecas de conhecimento falharam.' }
& node (Join-Path $projectRoot 'test_claude_health.js')
if ($LASTEXITCODE -ne 0) { throw 'Testes de autenticação JavaScript falharam.' }
$knowledgePack = Join-Path $projectRoot 'complementares\Soma-Conhecimento-1.0.0'
if (!(Test-Path -LiteralPath $knowledgePack)) { $knowledgePack = Join-Path (Split-Path $projectRoot -Parent) 'complementares\Soma-Conhecimento-1.0.0' }
if (!(Test-Path -LiteralPath $knowledgePack)) { $knowledgePack = Join-Path (Split-Path $projectRoot -Parent) 'soma-conhecimento-1.0.0' }
& node (Join-Path $projectRoot 'test_expressions.js') $knowledgePack
if ($LASTEXITCODE -ne 0) { throw 'Testes numéricos das expressões falharam.' }
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'payload-manifest.json') -Raw | ConvertFrom-Json
$hashEntries = @()
$resourceArgs = @()
foreach ($payload in $manifest.payloads) {
    $payloadPath = Join-Path $projectRoot ('payloads\' + $payload.name + '.zip')
    $hash = (Get-FileHash -LiteralPath $payloadPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $payload.sha256) { throw "Integridade inválida: $($payload.name)" }
    $hashEntries += ('{"' + $payload.name + '","' + $hash + '"}')
    $resourceArgs += ('/resource:' + $payloadPath + ',' + $payload.name + '.zip')
}
$hashSource = 'using System.Collections.Generic; namespace SomaSetup { static class PayloadHashes { public static readonly Dictionary<string,string> Values = new Dictionary<string,string> { ' + ($hashEntries -join ',') + ' }; } }'
$buildPath = Join-Path $projectRoot 'build'
New-Item -ItemType Directory -Path $buildPath -Force | Out-Null
$hashPath = Join-Path $buildPath 'PayloadHashes.cs'
Set-Content -LiteralPath $hashPath -Value $hashSource -Encoding UTF8
$references = @('/r:System.dll','/r:System.Core.dll','/r:System.Runtime.Serialization.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll','/r:System.Web.Extensions.dll','/r:System.Xml.dll')
$enginePath = Join-Path $projectRoot 'installer\InstallEngine.cs'
$profilesPath = Join-Path $projectRoot 'installer\McpProfiles.cs'
$knowledgePath = Join-Path $projectRoot 'installer\KnowledgeSetup.cs'
$testPath = Join-Path $buildPath 'EngineTests.exe'
& $compilerPath /nologo /utf8output /target:exe ("/out:" + $testPath) $references $enginePath $profilesPath $knowledgePath (Join-Path $projectRoot 'installer\EngineTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar testes.' }
& $testPath (Join-Path $projectRoot 'payloads')
if ($LASTEXITCODE -ne 0) { throw 'Testes do instalador falharam.' }
$outputPath = Join-Path $projectRoot 'Soma-Agente-Instalador-1.9.0-preview.exe'
& $compilerPath /nologo /utf8output /target:winexe /platform:anycpu ("/out:" + $outputPath) $references /r:System.Windows.Forms.dll /r:System.Drawing.dll $resourceArgs $enginePath $profilesPath $knowledgePath (Join-Path $projectRoot 'installer\Program.cs') $hashPath
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o instalador.' }
Get-FileHash -LiteralPath $outputPath -Algorithm SHA256
