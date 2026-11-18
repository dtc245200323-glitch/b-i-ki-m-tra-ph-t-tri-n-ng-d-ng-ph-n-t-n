$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$appRoot=Join-Path $root 'output/Supportly-App'
if(Test-Path (Join-Path $root 'api/Supportly.Api.exe')){$appRoot=$root}
$file=Join-Path $appRoot 'data/processes.json'
if(Test-Path -LiteralPath $file){foreach($record in @(Get-Content -LiteralPath $file -Raw | ConvertFrom-Json)){$proc=Get-Process -Id $record.Id -ErrorAction SilentlyContinue;if($proc -and $proc.Path -eq $record.Path){Stop-Process -Id $proc.Id}};Remove-Item -LiteralPath $file}
Write-Host 'Da dung API va Worker. RabbitMQ van chay de bao toan hang doi.'
