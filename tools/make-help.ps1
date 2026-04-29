[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$makefile = Join-Path $PSScriptRoot '..\Makefile'
Write-Host ''
Write-Host 'Honorare -- comandos disponiveis'
Write-Host ''
Get-Content $makefile -Encoding UTF8 | ForEach-Object {
    if ($_ -match '^([a-zA-Z_-]+):.*##\s*(.*)') {
        '  {0,-22} {1}' -f $Matches[1], $Matches[2]
    }
}
Write-Host ''
