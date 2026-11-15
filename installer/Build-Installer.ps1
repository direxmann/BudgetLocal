$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$payload = Join-Path $repo 'Distribution\BudgetLocal-Setup-Payload'
$distribution = Join-Path $repo 'Distribution'
$installer = Join-Path $distribution 'BudgetLocal-Setup.exe'
$project = Join-Path $repo 'BudgetLocal.Desktop\BudgetLocal.Desktop.csproj'
$icon = Join-Path $repo 'BudgetLocal.Desktop\Assets\BudgetLocal.ico'
$bootstrapperSource = Join-Path $PSScriptRoot 'Bootstrapper\Program.cs'
$dotnetHome = Join-Path $repo '.dotnet-cli'
$env:DOTNET_CLI_HOME = $dotnetHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'

if (Test-Path -LiteralPath $payload) {
    throw "Le dossier de préparation existe déjà : $payload. Déplacez-le ou supprimez-le après vérification avant de reconstruire."
}
if (Test-Path -LiteralPath $installer) {
    throw "L’installateur existe déjà : $installer. Déplacez-le avant de reconstruire."
}

New-Item -ItemType Directory -Path $payload -Force | Out-Null
& dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None -p:DebugSymbols=false `
    --no-restore -o $payload
if ($LASTEXITCODE -ne 0) { throw "La génération autonome a échoué ($LASTEXITCODE)." }

$appExe = Join-Path $payload 'BudgetLocal.Desktop.exe'
if (-not (Test-Path -LiteralPath $appExe -PathType Leaf)) { throw "L’exécutable autonome est introuvable." }
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { throw "Le compilateur Windows .NET Framework requis pour créer l’installateur est absent." }
$uninstaller = Join-Path $PSScriptRoot 'Uninstall.ps1'
& $csc /nologo /target:winexe "/out:$installer" "/win32icon:$icon" /reference:System.Windows.Forms.dll /reference:System.dll /reference:Microsoft.CSharp.dll "/resource:$appExe,BudgetLocal.Desktop.exe" "/resource:$uninstaller,Uninstall.ps1" $bootstrapperSource
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installer -PathType Leaf)) {
    throw "La création de l’installateur a échoué ($LASTEXITCODE). Le dossier de préparation est conservé : $payload"
}

$size = [math]::Round((Get-Item -LiteralPath $installer).Length / 1MB, 1)
Remove-Item -LiteralPath $payload -Recurse -Force
Write-Output "Installateur cree : $installer ($size Mo)"
Write-Output "Cible : Windows 10/11 64 bits. Le runtime .NET 10 de l’application est inclus."
