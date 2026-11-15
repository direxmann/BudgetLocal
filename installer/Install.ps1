$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$testMode = $env:BUDGETLOCAL_INSTALLER_TEST -eq '1'
$sourceExe = Join-Path $PSScriptRoot 'BudgetLocal.Desktop.exe'
$sourceUninstaller = Join-Path $PSScriptRoot 'Uninstall.ps1'

try {
    if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) {
        throw "Le fichier BudgetLocal est absent de l’installateur."
    }

    if ($testMode) {
        if ([string]::IsNullOrWhiteSpace($env:BUDGETLOCAL_TEST_INSTALL_DIR)) {
            throw "Le dossier de test de l’installation n’est pas défini."
        }
        $installDir = $env:BUDGETLOCAL_TEST_INSTALL_DIR
    }
    else {
        $installDir = Join-Path $env:LOCALAPPDATA 'Programs\BudgetLocal'
    }

    $runningApp = Get-Process -Name 'BudgetLocal.Desktop' -ErrorAction SilentlyContinue
    if ($runningApp) {
        throw "Fermez BudgetLocal avant de l’installer ou de le mettre à jour."
    }

    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    Copy-Item -LiteralPath $sourceExe -Destination (Join-Path $installDir 'BudgetLocal.exe') -Force
    if (Test-Path -LiteralPath $sourceUninstaller -PathType Leaf) {
        Copy-Item -LiteralPath $sourceUninstaller -Destination (Join-Path $installDir 'Uninstall.ps1') -Force
    }

    if (-not $testMode) {
        $appExe = Join-Path $installDir 'BudgetLocal.exe'
        $desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
        $programs = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
        $startMenu = Join-Path $programs 'BudgetLocal'
        New-Item -ItemType Directory -Path $startMenu -Force | Out-Null

        $shell = New-Object -ComObject WScript.Shell
        foreach ($shortcutPath in @(
            (Join-Path $desktop 'BudgetLocal.lnk'),
            (Join-Path $startMenu 'BudgetLocal.lnk')
        )) {
            $shortcut = $shell.CreateShortcut($shortcutPath)
            $shortcut.TargetPath = $appExe
            $shortcut.WorkingDirectory = $installDir
            $shortcut.Description = 'Gestion de budget personnel'
            $shortcut.IconLocation = "$appExe,0"
            $shortcut.Save()
        }

        $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BudgetLocal'
        New-Item -Path $uninstallKey -Force | Out-Null
        New-ItemProperty -Path $uninstallKey -Name DisplayName -Value 'BudgetLocal' -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value '1.0.0' -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $uninstallKey -Name Publisher -Value 'Delguste François' -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $installDir -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $uninstallKey -Name DisplayIcon -Value $appExe -PropertyType String -Force | Out-Null
        $uninstallCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$installDir\Uninstall.ps1`""
        New-ItemProperty -Path $uninstallKey -Name UninstallString -Value $uninstallCommand -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $uninstallKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
        New-ItemProperty -Path $uninstallKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null

        $choice = [System.Windows.Forms.MessageBox]::Show(
            "BudgetLocal est installé. Un raccourci a été ajouté au Bureau.`n`nVoulez-vous lancer l’application maintenant?",
            'Installation terminée',
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Information)
        if ($choice -eq [System.Windows.Forms.DialogResult]::Yes) {
            Start-Process -FilePath $appExe -WorkingDirectory $installDir
        }
    }
}
catch {
    [void][System.Windows.Forms.MessageBox]::Show(
        "L’installation de BudgetLocal n’a pas pu se terminer.`n`n$($_.Exception.Message)",
        'Installation de BudgetLocal',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error)
    exit 1
}
