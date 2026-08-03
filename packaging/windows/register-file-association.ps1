# Registers view-md as an available (not automatically default) handler for
# .md files for the CURRENT user, via the standard per-user
# HKCU\Software\Classes mechanism (no admin rights required).
#
# After running this, right-click a .md file -> "Open with" -> "Choose
# another app" -> view-md -> "Always use this app" to make it the default,
# same as the manual flow on any Windows install.
#
# Usage (from an unzipped view-md_<version>_win-x64 folder):
#   powershell -ExecutionPolicy Bypass -File register-file-association.ps1

$ErrorActionPreference = "Stop"

$exePath = Join-Path $PSScriptRoot "view-md.exe"
if (-not (Test-Path $exePath)) {
    throw "view-md.exe not found next to this script ($exePath). Run this from the unzipped publish folder."
}

$progId = "ViewMd.MarkdownFile"

New-Item -Path "HKCU:\Software\Classes\$progId\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\$progId\shell\open\command" -Name "(default)" -Value "`"$exePath`" `"%1`""

New-Item -Path "HKCU:\Software\Classes\$progId" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\$progId" -Name "(default)" -Value "Markdown Document"

foreach ($ext in ".md", ".markdown") {
    # OpenWithProgids entries are REG_NONE with zero-length data (per Microsoft's
    # "Programmatic Identifiers" Win32 docs) — Set-ItemProperty has no -Type None,
    # so this goes through the .NET Registry API directly to get the value kind right.
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("Software\Classes\$ext\OpenWithProgids")
    $key.SetValue($progId, [byte[]]@(), [Microsoft.Win32.RegistryValueKind]::None)
    $key.Close()
}

Write-Host "view-md registered as an available handler for .md/.markdown files."
Write-Host "Right-click a .md file -> Open with -> Choose another app -> view-md -> Always use this app to set it as default."
