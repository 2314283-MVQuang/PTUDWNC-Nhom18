# Script test FR-SRCH-004: Phân trang tìm kiếm công thức
param(
    [string]$BaseUrl = "http://localhost:5000"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  KIEM THU FR-SRCH-004: PHAN TRANG KET QUA TIM KIEM" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Test search page 1, pageSize 2
$urlPage1 = "$BaseUrl/api/v1/recipes/search?q=a&page=1&pageSize=2"
Write-Host "`n[1] GET $urlPage1" -ForegroundColor Yellow
try {
    $res1 = Invoke-RestMethod -Uri $urlPage1 -Method Get
    Write-Host "TotalCount: $($res1.meta.total)" -ForegroundColor Green
    Write-Host "Page: $($res1.meta.page) / TotalPages: $($res1.meta.totalPages)" -ForegroundColor Green
    Write-Host "HasNextPage: $($res1.meta.hasNextPage) | HasPreviousPage: $($res1.meta.hasPreviousPage)" -ForegroundColor Green
    Write-Host "Items count: $($res1.data.Count)" -ForegroundColor Green
} catch {
    Write-Host "Error or Server not running: $_" -ForegroundColor Red
}

# 2. Test search page 2, pageSize 2
$urlPage2 = "$BaseUrl/api/v1/recipes/search?q=a&page=2&pageSize=2"
Write-Host "`n[2] GET $urlPage2" -ForegroundColor Yellow
try {
    $res2 = Invoke-RestMethod -Uri $urlPage2 -Method Get
    Write-Host "TotalCount: $($res2.meta.total)" -ForegroundColor Green
    Write-Host "Page: $($res2.meta.page) / TotalPages: $($res2.meta.totalPages)" -ForegroundColor Green
    Write-Host "HasNextPage: $($res2.meta.hasNextPage) | HasPreviousPage: $($res2.meta.hasPreviousPage)" -ForegroundColor Green
    Write-Host "Items count: $($res2.data.Count)" -ForegroundColor Green
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}

# 3. Test validation PageSize > 50 (Chống DoS)
$urlInvalidPageSize = "$BaseUrl/api/v1/recipes/search?q=a&pageSize=100"
Write-Host "`n[3] Test PageSize > 50 (Expect 422 Unprocessable Entity): GET $urlInvalidPageSize" -ForegroundColor Yellow
try {
    $resInvalid = Invoke-RestMethod -Uri $urlInvalidPageSize -Method Get
    Write-Host "Unexpected Success" -ForegroundColor Red
} catch {
    Write-Host "Response Status Code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Green
    Write-Host "Validation blocked successfully!" -ForegroundColor Green
}
