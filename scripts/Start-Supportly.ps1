$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$appRoot = Join-Path $root 'output/Supportly-App'
if (Test-Path (Join-Path $root 'api/Supportly.Api.exe')) { $appRoot = $root }
$dataRoot = Join-Path $appRoot 'data'
New-Item -ItemType Directory -Force $dataRoot | Out-Null
$env:DataPath = Join-Path $dataRoot 'supportly.db'
$env:SeedDemo = 'true'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Can cai .NET 10 ASP.NET Core Runtime hoac SDK.' }
try { if(Get-NetTCPConnection -LocalPort 5672 -State Listen -ErrorAction SilentlyContinue){Write-Host 'Su dung RabbitMQ dang chay o cong 5672.'}else{docker compose -p supportly -f (Join-Path $appRoot 'compose.yaml') up -d rabbitmq; if ($LASTEXITCODE -ne 0) { throw 'RabbitMQ chua khoi dong' }} }
catch { Write-Warning 'RabbitMQ chua san sang. Chat van chay, lich su se dong bo khi RabbitMQ hoat dong.' }
$records = @()
foreach ($service in @(@{Name='api';Exe='Supportly.Api.exe';Port=5080},@{Name='worker';Exe='Supportly.Worker.exe';Port=5081})) {
    $folder = Join-Path $appRoot $service.Name
    $exe = Join-Path $folder $service.Exe
    if (-not (Test-Path -LiteralPath $exe)) { throw "Thieu ban build: $exe" }
    if (Get-NetTCPConnection -LocalPort $service.Port -State Listen -ErrorAction SilentlyContinue) { Write-Warning "Cong $($service.Port) dang duoc su dung. Bo qua khoi dong $($service.Name)."; continue }
    $proc = Start-Process -FilePath $exe -WorkingDirectory $folder -ArgumentList @('--urls',"http://localhost:$($service.Port)") -WindowStyle Hidden -RedirectStandardOutput (Join-Path $dataRoot "$($service.Name).log") -RedirectStandardError (Join-Path $dataRoot "$($service.Name)-error.log") -PassThru
    $records += @{Id=$proc.Id;Path=$exe}
}
$pidFile = Join-Path $dataRoot 'processes.json'
if ($records.Count -gt 0) { if(Test-Path $pidFile){$prior=@(Get-Content $pidFile -Raw | ConvertFrom-Json);$records=$prior+$records}; ConvertTo-Json -InputObject @($records) | Set-Content -LiteralPath $pidFile }
for ($i=0; $i -lt 30; $i++) { try { $null=Invoke-RestMethod 'http://localhost:5080/health'; Start-Process 'http://localhost:5080'; exit 0 } catch { Start-Sleep -Seconds 1 } }
throw 'API chua san sang. Xem file data/api.log va data/api-error.log.'
