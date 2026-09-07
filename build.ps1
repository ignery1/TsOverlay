# Definir caminhos
$jsonPath = "version.json"
$csprojPath = "TS6-SpeakerOverlay\TS6-SpeakerOverlay.csproj"
$publishDir = "TS6-SpeakerOverlay\bin\Release\net10.0-windows\win-x64\publish\*"
$targetDir = "C:\inetpub\tsoverlay"

Write-Host "Lendo arquivos de versão..." -ForegroundColor Cyan

# 1. Incrementar versão no version.json usando Regex para não quebrar a formatação original
$jsonContent = Get-Content $jsonPath -Raw
if ($jsonContent -match '"version":\s*"(\d+)\.(\d+)\.(\d+)\.(\d+)"') {
    $major = $matches[1]
    $minor = $matches[2]
    $build = $matches[3]
    $rev = [int]$matches[4] + 1
    $newVersion = "$major.$minor.$build.$rev"
    
    $jsonContent = $jsonContent -replace '"version":\s*".*?"', "`"version`": `"$newVersion`""
    Set-Content -Path $jsonPath -Value $jsonContent -Encoding UTF8
    Write-Host "--> version.json atualizado para: $newVersion" -ForegroundColor Green
} else {
    Write-Host "--> Erro: Não foi possível ler a versão do version.json." -ForegroundColor Red
    exit
}

# 2. Atualizar a versão no TS6-SpeakerOverlay.csproj
if (Test-Path $csprojPath) {
    $csprojContent = Get-Content $csprojPath -Raw
    $csprojContent = $csprojContent -replace '<Version>.*?</Version>', "<Version>$newVersion</Version>"
    $csprojContent = $csprojContent -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$newVersion</AssemblyVersion>"
    $csprojContent = $csprojContent -replace '<FileVersion>.*?</FileVersion>', "<FileVersion>$newVersion</FileVersion>"
    Set-Content -Path $csprojPath -Value $csprojContent -Encoding UTF8
    Write-Host "--> TS6-SpeakerOverlay.csproj atualizado para: $newVersion" -ForegroundColor Green
} else {
    Write-Host "--> Erro: Arquivo .csproj não encontrado em $csprojPath." -ForegroundColor Red
    exit
}

# 3. Executar o Publish
Write-Host "`nIniciando o build (dotnet publish)..." -ForegroundColor Cyan
dotnet publish $csprojPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nErro durante o build! O processo será interrompido." -ForegroundColor Red
    exit
}

# 4. Copiar para o diretório de destino
Write-Host "`nCopiando arquivos para $targetDir..." -ForegroundColor Cyan
if (!(Test-Path $targetDir)) {
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
}

Copy-Item -Path $publishDir -Destination $targetDir -Recurse -Force
Write-Host "Deploy concluído com sucesso para a pasta C:\inetpub\tsoverlay!" -ForegroundColor Green