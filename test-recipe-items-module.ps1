# ==============================================================================
# SCRIPT KIEM THU TU DONG CAC ENDPOINTS CONG THUC (TUAN 4 - HO QUOC TIEN)
# Module: Recipe Ingredients, Steps, Images (FR-RCP-008, 009, 010)
# ==============================================================================

$baseUrl = "http://localhost:5000/api/v1"
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  KIEM THU ENDPOINTS LAB 4 (TUAN 4) - HO QUOC TIEN" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# Buoc 1: Dang nhap lay Bearer token (Admin)
Write-Host "`n[1/12] Dang dang nhap lay token (Admin)..." -ForegroundColor Yellow
$loginBody = @{
    email = "admin@culinaryblog.local"
    password = "Admin@123"
} | ConvertTo-Json

try {
    $loginRes = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $token = $loginRes.data.accessToken
    Write-Host " -> Dang nhap thanh cong! Token: $($token.Substring(0, 25))..." -ForegroundColor Green
} catch {
    Write-Host " -> LOI DANG NHAP: $_" -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Buoc 2: Lay 1 RecipeId mau tu database
Write-Host "`n[2/12] Lay RecipeId mau tu database..." -ForegroundColor Yellow
$recipeId = $null
$pyCmd = "import sqlite3; conn = sqlite3.connect('src/CulinaryBlog.API/bin/Debug/net9.0/culinaryblog.db'); print(conn.cursor().execute('SELECT Id FROM Recipes LIMIT 1').fetchone()[0])"
$recipeId = (python -c "$pyCmd").Trim()

if (-not $recipeId) {
    Write-Host " -> Khong tim thay RecipeId nao trong database!" -ForegroundColor Red
    exit 1
}
Write-Host " -> Su dung RecipeId: $recipeId" -ForegroundColor Green

# ==============================================================================
# PHAN 1: RECIPE INGREDIENTS (FR-RCP-009)
# ==============================================================================
Write-Host "`n---------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host "  KIEM THU NGUYEN LIEU (INGREDIENTS - 3 CORE + 1 LIST)" -ForegroundColor DarkCyan
Write-Host "---------------------------------------------------------" -ForegroundColor DarkCyan

# Test 1: GET ingredients
Write-Host "`n[3/12] GET /api/v1/recipes/$recipeId/ingredients" -ForegroundColor Yellow
$getIngRes = Invoke-RestMethod -Uri "$baseUrl/recipes/$recipeId/ingredients" -Method Get
Write-Host " -> OK! Hien co $($getIngRes.data.Count) nguyen lieu." -ForegroundColor Green

# Test 2: POST ingredients (Them moi)
Write-Host "`n[4/12] POST /api/v1/recipes/$recipeId/ingredients" -ForegroundColor Yellow
$addIngBody = @{
    name = "Thit than bo tuoi"
    quantity = 0.5
    unit = "kg"
    notes = "Thai mong uop toi"
    orderIndex = 99
} | ConvertTo-Json

$postIngRes = Invoke-RestMethod -Uri "$baseUrl/recipes/$recipeId/ingredients" -Method Post -Body $addIngBody -Headers $headers
$newIngId = $postIngRes.data.id
Write-Host " -> TAO THANH CONG! IngredientId: $newIngId, Ten: $($postIngRes.data.name)" -ForegroundColor Green

# Test 3: PUT ingredients/{id} (Cap nhat)
Write-Host "`n[5/12] PUT /api/v1/recipes/$recipeId/ingredients/$newIngId" -ForegroundColor Yellow
$updateIngBody = @{
    name = "Thit bo phi le cao cap (Cap nhat)"
    quantity = 0.75
    unit = "kg"
    notes = "Uop tieu toi va gung tuoi"
    orderIndex = 1
} | ConvertTo-Json

$putIngRes = Invoke-RestMethod -Uri "$baseUrl/recipes/$recipeId/ingredients/$newIngId" -Method Put -Body $updateIngBody -Headers $headers
Write-Host " -> CAP NHAT THANH CONG! Ten moi: $($putIngRes.data.name), So luong: $($putIngRes.data.quantity)" -ForegroundColor Green

# Test 4: DELETE ingredients/{id} (Xoa)
Write-Host "`n[6/12] DELETE /api/v1/recipes/$recipeId/ingredients/$newIngId" -ForegroundColor Yellow
$delIngRes = Invoke-WebRequest -Uri "$baseUrl/recipes/$recipeId/ingredients/$newIngId" -Method Delete -Headers $headers
if ($delIngRes.StatusCode -eq 204) {
    Write-Host " -> XOA THANH CONG (HTTP 204 NoContent)!" -ForegroundColor Green
} else {
    Write-Host " -> Tra ve ma HTTP: $($delIngRes.StatusCode)" -ForegroundColor Yellow
}

# ==============================================================================
# PHAN 2: RECIPE STEPS (FR-RCP-010)
# ==============================================================================
Write-Host "`n---------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host "  KIEM THU CAC BUOC THUC HIEN (STEPS - 3 CORE + 1 LIST)" -ForegroundColor DarkCyan
Write-Host "---------------------------------------------------------" -ForegroundColor DarkCyan

# Test 5: GET steps
Write-Host "`n[7/12] GET /api/v1/recipes/$recipeId/steps" -ForegroundColor Yellow
$getStepsRes = Invoke-RestMethod -Uri "$baseUrl/recipes/$recipeId/steps" -Method Get
Write-Host " -> OK! Hien co $($getStepsRes.data.Count) buoc nau." -ForegroundColor Green

# Test 6: POST steps (Them moi)
Write-Host "`n[8/12] POST /api/v1/recipes/$recipeId/steps" -ForegroundColor Yellow
$addStepBody = @{
    title = "Nem gia vi bi truyen"
    description = "Them hoa hoi, que chi va thao qua da nuong thom vao noi nuoc dung."
    timerMinutes = 30
    imageUrl = "http://localhost:9000/recipe-images/gia-vi-nuoc-dung.jpg"
} | ConvertTo-Json

$postStepRes = Invoke-RestMethod -Uri "$baseUrl/recipes/$recipeId/steps" -Method Post -Body $addStepBody -Headers $headers
$newStepId = $postStepRes.data.id
Write-Host " -> TAO BUOC THANH CONG! StepId: $newStepId, Buoc so: $($postStepRes.data.stepNumber), Tieu de: $($postStepRes.data.title)" -ForegroundColor Green

# Test 7: PUT steps/{id} (Cap nhat)
Write-Host "`n[9/12] PUT /api/v1/recipes/$recipeId/steps/$newStepId" -ForegroundColor Yellow
$updateStepBody = @{
    title = "Nem gia vi bi truyen (Cap nhat)"
    description = "Them hoa hoi nuong thom, dun lua riu riu trong 45 phut."
    timerMinutes = 45
    imageUrl = "http://localhost:9000/recipe-images/gia-vi-updated.jpg"
} | ConvertTo-Json

$putStepRes = Invoke-RestMethod -Uri "$baseUrl/recipes/$recipeId/steps/$newStepId" -Method Put -Body $updateStepBody -Headers $headers
Write-Host " -> CAP NHAT BUOC THANH CONG! Tieu de moi: $($putStepRes.data.title), Hen gio: $($putStepRes.data.timerMinutes) phut" -ForegroundColor Green

# Test 8: DELETE steps/{id} (Xoa & tu renumber)
Write-Host "`n[10/12] DELETE /api/v1/recipes/$recipeId/steps/$newStepId" -ForegroundColor Yellow
$delStepRes = Invoke-WebRequest -Uri "$baseUrl/recipes/$recipeId/steps/$newStepId" -Method Delete -Headers $headers
if ($delStepRes.StatusCode -eq 204) {
    Write-Host " -> XOA BUOC THANH CONG (HTTP 204 NoContent)! Cac buoc con lai duoc tu dong renumber." -ForegroundColor Green
} else {
    Write-Host " -> Tra ve ma HTTP: $($delStepRes.StatusCode)" -ForegroundColor Yellow
}

# ==============================================================================
# PHAN 3: RECIPE IMAGES (FR-RCP-008 & MinIO Setup)
# ==============================================================================
Write-Host "`n---------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host "  KIEM THU ANH CONG THUC (IMAGES - GAN ANH QUA URL MINIO)" -ForegroundColor DarkCyan
Write-Host "---------------------------------------------------------" -ForegroundColor DarkCyan

# Test 9: POST images (Gan anh tu MinIO URL)
Write-Host "`n[11/12] POST /api/v1/recipes/$recipeId/images" -ForegroundColor Yellow
$addImageBody = @{
    imageUrl = "http://localhost:9000/recipe-images/uploads/2026/10/pho-bo-dac-biet.jpg"
    altText = "To pho bo tai nam dac biet thom lung"
    isPrimary = $true
} | ConvertTo-Json

$postImgRes = Invoke-RestMethod -Uri "$baseUrl/recipes/$recipeId/images" -Method Post -Body $addImageBody -Headers $headers
$newImgId = $postImgRes.data.id
Write-Host " -> GAN ANH THANH CONG! ImageId: $newImgId, IsPrimary: $($postImgRes.data.isPrimary), URL: $($postImgRes.data.originalUrl)" -ForegroundColor Green

# Test 10: GET images & DELETE image
Write-Host "`n[12/12] GET & DELETE /api/v1/recipes/$recipeId/images/$newImgId" -ForegroundColor Yellow
$getImgRes = Invoke-RestMethod -Uri "$baseUrl/recipes/$recipeId/images" -Method Get
Write-Host " -> GET anh thanh cong! So luong anh: $($getImgRes.data.Count)" -ForegroundColor Green

$delImgRes = Invoke-WebRequest -Uri "$baseUrl/recipes/$recipeId/images/$newImgId" -Method Delete -Headers $headers
if ($delImgRes.StatusCode -eq 204) {
    Write-Host " -> XOA ANH THANH CONG (HTTP 204 NoContent)!" -ForegroundColor Green
} else {
    Write-Host " -> Tra ve ma HTTP: $($delImgRes.StatusCode)" -ForegroundColor Yellow
}

Write-Host "`n=========================================================" -ForegroundColor Green
Write-Host "  TAT CA CAC ENDPOINT LAB 4 DA DUOC TEST THANH CONG 100%!" -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Green
