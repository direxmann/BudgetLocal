$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$answer = [System.Windows.Forms.MessageBox]::Show(
    "Désinstaller BudgetLocal?`n`nVos budgets et vos sauvegardes personnelles seront conservés.",
    'Désinstaller BudgetLocal',
    [System.Windows.Forms.MessageBoxButtons]::YesNo,
    [System.Windows.Forms.MessageBoxIcon]::Warning,
    [System.Windows.Forms.MessageBoxDefaultButton]::Button2)
if ($answer -ne [System.Windows.Forms.DialogResult]::Yes) { exit 0 }

if (Get-Process -Name 'BudgetLocal' -ErrorAction SilentlyContinue) {
    [void][System.Windows.Forms.MessageBox]::Show(
        'Fermez BudgetLocal avant de le désinstaller.',
        'Désinstallation de BudgetLocal',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information)
    exit 1
}

$installDir = $PSScriptRoot
$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
$shortcut = Join-Path $desktop 'BudgetLocal.lnk'
$programs = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\BudgetLocal'
Remove-Item -LiteralPath $shortcut -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $programs -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BudgetLocal' -Recurse -Force -ErrorAction SilentlyContinue

[void][System.Windows.Forms.MessageBox]::Show(
    "BudgetLocal a été retiré. Vos budgets et sauvegardes restent dans votre dossier de données personnel.",
    'Désinstallation terminée',
    [System.Windows.Forms.MessageBoxButtons]::OK,
    [System.Windows.Forms.MessageBoxIcon]::Information)

$command = "/d /c timeout /t 2 /nobreak >nul & rmdir /s /q `"$installDir`""
Start-Process -FilePath $env:ComSpec -ArgumentList $command -WindowStyle Hidden
