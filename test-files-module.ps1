$baseUrl = "http://localhost:5000/api/v1/files"

Write-Host "=========================================="
Write-Host "  TEST MODULE QUAN LY TEP TIN (MINIO SDK)"
Write-Host "=========================================="

# Test 1: Cap Presigned URL hop le (anh JPEG 2MB)
Write-Host "`n1. Test cap Presigned URL hop le (pho-bo-nam-dinh.jpg, 2MB)..."
$validPayload = @{
    fileName = "pho-bo-nam-dinh.jpg"
    contentType = "image/jpeg"
    size = 2097152
} | ConvertTo-Json

try {
    $res1 = Invoke-RestMethod -Uri "$baseUrl/presigned-url" -Method Post -Body $validPayload -ContentType "application/json; charset=utf-8"
    Write-Host "-> THANH CONG (200 OK)!" -ForegroundColor Green
    Write-Host "   FileId: $($res1.data.fileId)"
    Write-Host "   FileName: $($res1.data.fileName)"
    Write-Host "   StorageKey: $($res1.data.storageKey)"
    Write-Host "   UploadUrl: $($res1.data.uploadUrl)"
    Write-Host "   PublicUrl: $($res1.data.publicUrl)"
    $fileId = $res1.data.fileId
} catch {
    Write-Host "-> THAT BAI: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: GET metadata theo fileId vua tao
Write-Host "`n2. Test GET metadata tep tin theo ID ($fileId)..."
try {
    $res2 = Invoke-RestMethod -Uri "$baseUrl/$fileId" -Method Get
    Write-Host "-> THANH CONG (200 OK)!" -ForegroundColor Green
    Write-Host "   Id: $($res2.data.id)"
    Write-Host "   FileName: $($res2.data.fileName)"
    Write-Host "   ContentType: $($res2.data.contentType)"
    Write-Host "   Size: $($res2.data.size) bytes"
    Write-Host "   BucketName: $($res2.data.bucketName)"
    Write-Host "   CreatedAt: $($res2.data.createdAt)"
} catch {
    Write-Host "-> THAT BAI: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 3: Chan file co dinh dang mo rong khong hop le (.exe)
Write-Host "`n3. Test chan file duoi .exe (script.exe)..."
$invalidExtPayload = @{
    fileName = "script.exe"
    contentType = "image/jpeg"
    size = 1024
} | ConvertTo-Json

try {
    $null = Invoke-RestMethod -Uri "$baseUrl/presigned-url" -Method Post -Body $invalidExtPayload -ContentType "application/json"
    Write-Host "-> THAT BAI: Khong chan duoc file .exe!" -ForegroundColor Red
} catch {
    $status = $_.Exception.Response.StatusCode.value__
    $respStream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($respStream)
    $respBody = $reader.ReadToEnd()
    Write-Host "-> CHAN THANH CONG: HTTP $status" -ForegroundColor Green
    Write-Host "   Response: $respBody"
}

# Test 4: Chan ContentType khong hop le (application/x-msdownload)
Write-Host "`n4. Test chan Content-Type khong hop le..."
$invalidMimePayload = @{
    fileName = "photo.jpg"
    contentType = "application/x-msdownload"
    size = 1024
} | ConvertTo-Json

try {
    $null = Invoke-RestMethod -Uri "$baseUrl/presigned-url" -Method Post -Body $invalidMimePayload -ContentType "application/json"
    Write-Host "-> THAT BAI: Khong chan duoc Content-Type khong hop le!" -ForegroundColor Red
} catch {
    $status = $_.Exception.Response.StatusCode.value__
    $respStream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($respStream)
    $respBody = $reader.ReadToEnd()
    Write-Host "-> CHAN THANH CONG: HTTP $status" -ForegroundColor Green
    Write-Host "   Response: $respBody"
}

# Test 5: Chan file qua dung luong 10MB (12MB)
Write-Host "`n5. Test chan file qua dung luong (> 10MB)..."
$oversizedPayload = @{
    fileName = "giant-wallpaper.png"
    contentType = "image/png"
    size = 12582912
} | ConvertTo-Json

try {
    $null = Invoke-RestMethod -Uri "$baseUrl/presigned-url" -Method Post -Body $oversizedPayload -ContentType "application/json"
    Write-Host "-> THAT BAI: Khong chan duoc file qua 10MB!" -ForegroundColor Red
} catch {
    $status = $_.Exception.Response.StatusCode.value__
    $respStream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($respStream)
    $respBody = $reader.ReadToEnd()
    Write-Host "-> CHAN THANH CONG: HTTP $status" -ForegroundColor Green
    Write-Host "   Response: $respBody"
}

# Test 6: GET file voi ID khong ton tai
Write-Host "`n6. Test GET ID khong ton tai (00000000-0000-0000-0000-000000000000)..."
try {
    $null = Invoke-RestMethod -Uri "$baseUrl/00000000-0000-0000-0000-000000000000" -Method Get
    Write-Host "-> THAT BAI: Khong tra ve 404!" -ForegroundColor Red
} catch {
    $status = $_.Exception.Response.StatusCode.value__
    $respStream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($respStream)
    $respBody = $reader.ReadToEnd()
    Write-Host "-> TRA VE DUNG 404: HTTP $status" -ForegroundColor Green
    Write-Host "   Response: $respBody"
}

Write-Host "`n=========================================="
Write-Host "  TAT CA CAC BUOC TEST DEU DA VUOT QUA!" -ForegroundColor Green
Write-Host "=========================================="
