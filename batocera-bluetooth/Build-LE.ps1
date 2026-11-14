$ErrorActionPreference = "Stop"
$framework = "$env:windir\Microsoft.NET\Framework64\v4.0.30319"
$refs = @("$env:windir\System32\WinMetadata\Windows.Foundation.winmd", "$env:windir\System32\WinMetadata\Windows.Devices.winmd")
foreach ($name in "System.Runtime","System.Runtime.InteropServices.WindowsRuntime") {
    $refs += (Get-ChildItem "$env:windir\Microsoft.NET\assembly\GAC_MSIL\$name" -Recurse -Filter *.dll | Select-Object -First 1).FullName
}
$sources = @("$PSScriptRoot\Program.cs","$PSScriptRoot\LowEnergy.cs") + @(Get-ChildItem "$PSScriptRoot\Lib\*.cs" | ForEach-Object FullName)
& "$framework\csc.exe" /nologo /target:exe "/out:$PSScriptRoot\batocera-bluetooth.exe" ($refs | ForEach-Object {"/r:$_"}) $sources
exit $LASTEXITCODE
