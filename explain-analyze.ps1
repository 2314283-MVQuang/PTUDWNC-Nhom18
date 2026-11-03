# Chuong 3 - do truy van bang EXPLAIN ANALYZE tren PostgreSQL dang chay trong Docker.
# Ket qua ghi ra explain-analyze-ketqua.txt. Du lieu gia 20 000 cong thuc bi ROLLBACK,
# khong anh huong du lieu that.
#
#   powershell -ExecutionPolicy Bypass -File .\explain-analyze.ps1
#   powershell -ExecutionPolicy Bypass -File .\explain-analyze.ps1 -Container culinaryblog-postgres -User postgres
param(
    [string]$Container = 'culinaryblog-prod-postgres-1',
    [string]$User = 'culinary',
    [string]$Database = 'culinaryblog',
    [string]$Output = 'explain-analyze-ketqua.txt'
)
$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 mac dinh gui chuoi sang lenh ngoai bang ASCII -> tieng Viet thanh '?'.
$utf8 = New-Object System.Text.UTF8Encoding $false
$OutputEncoding = $utf8
[Console]::OutputEncoding = $utf8
$sql = Join-Path $PSScriptRoot 'db\explain-analyze.sql'
Write-Host "Dang do truy van tren $Container/$Database ..." -ForegroundColor Cyan
Get-Content -Raw -Encoding UTF8 $sql | docker exec -i -e PGCLIENTENCODING=UTF8 $Container psql -U $User -d $Database -X -q 2>&1 |
    Out-File -Encoding utf8 $Output
Write-Host "Xong. Ket qua: $Output" -ForegroundColor Green
Get-Content -Encoding UTF8 $Output | Select-String -Pattern '^==|Execution Time|Index Scan|Seq Scan|Bitmap' | ForEach-Object { $_.Line }
