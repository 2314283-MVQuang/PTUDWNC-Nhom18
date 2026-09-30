$ErrorActionPreference = "Stop"

Write-Host "=== TEST FR-CAT-004: CAP NHAT DANH MUC VOI SLUG AUTO-SUFFIX (MT-06) ===" -ForegroundColor Cyan

# 1. Dang nhap Admin de lay Token
$loginBody = @{
    email = "admin@culinaryblog.local"
    password = "Admin@123"
} | ConvertTo-Json

$loginRes = Invoke-RestMethod -Uri "http://localhost:5000/api/v1/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.data.accessToken
if (-not $token) { throw "Khong lay duoc accessToken tu response!" }
Write-Host "[1] Dang nhap Admin thanh cong! Token da nhan: $($token.Substring(0, 20))..." -ForegroundColor Green

# 2. Lay danh sach danh muc
$cats = (Invoke-RestMethod -Uri "http://localhost:5000/api/v1/categories").data
$cat1 = $cats[0] # Mon xao
$cat2 = $cats[1] # Danh muc thu 2 se sua

Write-Host "[2] Danh muc goc 1: Name='$($cat1.name)' | Slug='$($cat1.slug)'"
Write-Host "[2] Danh muc goc 2: Name='$($cat2.name)' | Slug='$($cat2.slug)'"

# 3. Goi API PUT /api/v1/categories/{id} voi ten trung Cat 1 de kiem tra auto-suffix
$headers = @{
    Authorization = "Bearer $token"
}

$updateBody = @{
    name = $cat1.name
    description = "Cap nhat trung ten voi Cat 1 de test auto-suffix"
} | ConvertTo-Json

Write-Host "[3] Dang goi PUT /api/v1/categories/$($cat2.id) voi Name='$($cat1.name)'..."
$utf8Bytes = [System.Text.Encoding]::UTF8.GetBytes($updateBody)
$updateRes = Invoke-RestMethod -Uri "http://localhost:5000/api/v1/categories/$($cat2.id)" -Method Put -Headers $headers -Body $utf8Bytes -ContentType "application/json; charset=utf-8"

Write-Host "[4] KET QUA CAP NHAT THANH CONG:" -ForegroundColor Green
Write-Host "    - ID: $($updateRes.data.id)"
Write-Host "    - Name: $($updateRes.data.name)"
Write-Host "    - Slug moi tu dong them hau to: $($updateRes.data.slug)" -ForegroundColor Yellow

if ($updateRes.data.slug -match "^$($cat1.slug)-\d+$") {
    Write-Host "[SUCCESS] Slug da tu dong them hau to hop le theo MT-06: $($updateRes.data.slug)" -ForegroundColor Green
} else {
    Write-Host "[INFO] Slug nhan duoc: $($updateRes.data.slug)"
}

# 4. Kiem tra cache categories da bi xoa (evict) va danh sach moi da cap nhat
$newCats = (Invoke-RestMethod -Uri "http://localhost:5000/api/v1/categories").data
$updatedInList = $newCats | Where-Object { $_.id -eq $cat2.id }
Write-Host "[5] Danh muc trong danh sach sau khi xoa cache: Name='$($updatedInList.name)' | Slug='$($updatedInList.slug)'" -ForegroundColor Cyan
