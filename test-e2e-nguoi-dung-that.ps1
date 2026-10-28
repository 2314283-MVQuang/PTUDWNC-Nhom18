# Tuan 8 (Quang) - Kiem thu END-TO-END cac luong chinh nhu nguoi dung that, goi API dang chay
# (dotnet run, docker compose, hoac docker-compose.prod.yml). Moi lan chay tao user/danh muc/cong thuc
# MOI (ten co hau to ngau nhien) nen chay lai nhieu lan khong bi trung.
#
#   powershell -ExecutionPolicy Bypass -File .\test-e2e-nguoi-dung-that.ps1
#   powershell -ExecutionPolicy Bypass -File .\test-e2e-nguoi-dung-that.ps1 -BaseUrl http://IP:5000 -AdminEmail a@b.c -AdminPassword 'Xx@12345'
#
# Luu y: nhom /auth gioi han 10 request/phut/IP - script dung 5 request /auth.
param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$AdminEmail = 'admin@culinaryblog.local',
    [string]$AdminPassword = 'Admin@123'
)
$ErrorActionPreference = 'Stop'
$api = "$BaseUrl/api/v1"
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$script:pass = 0
$script:fail = 0

function Check([string]$Name, [bool]$Ok, [string]$Detail = '') {
    if ($Ok) { $script:pass++; Write-Host "  PASS  $Name" -ForegroundColor Green }
    else { $script:fail++; Write-Host "  FAIL  $Name $Detail" -ForegroundColor Red }
}

function RawStatus([string]$Url) {
    try {
        $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop
        return [int]$r.StatusCode
    } catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return 0
    }
}

function Call([string]$Method, [string]$Path, $Body = $null, [string]$Token = $null, [hashtable]$Headers = @{}) {
    $h = @{} + $Headers
    if ($Token) { $h['Authorization'] = "Bearer $Token" }
    $params = @{ Method = $Method; Uri = "$api$Path"; Headers = $h; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) {
        $params['Body'] = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10))
        $params['ContentType'] = 'application/json; charset=utf-8'
    }
    if ($PSVersionTable.PSVersion.Major -lt 7) {
        # Windows PowerShell 5.1 khong co SkipHttpErrorCheck: bat loi va doc status tu exception.
        $params.Remove('SkipHttpErrorCheck')
        try { $r = Invoke-WebRequest @params -UseBasicParsing; $status = [int]$r.StatusCode; $text = $r.Content }
        catch { $status = [int]$_.Exception.Response.StatusCode; $reader = New-Object IO.StreamReader($_.Exception.Response.GetResponseStream()); $text = $reader.ReadToEnd() }
    } else {
        $r = Invoke-WebRequest @params; $status = [int]$r.StatusCode; $text = $r.Content
    }
    $json = $null
    if ($text) { try { $json = $text | ConvertFrom-Json } catch { } }
    return [pscustomobject]@{ Status = $status; Json = $json; Headers = $r.Headers }
}

Write-Host "== Kiem thu nguoi dung that tren $BaseUrl (hau to $suffix) ==" -ForegroundColor Cyan

# 1. He thong song
Check 'GET /health/live tra 200' ((RawStatus "$BaseUrl/health/live") -eq 200)

# 2. Dang ky 2 nguoi dung that (FR-AUTH-001)
$pw = 'Nauan@2026'
$a = Call POST '/auth/register' @{ fullName = "Lan Nguyen $suffix"; email = "lan.$suffix@example.com"; password = $pw }
Check 'Dang ky user A tra 201 + JWT' ($a.Status -eq 201 -and $a.Json.data.accessToken) "status=$($a.Status)"
$b = Call POST '/auth/register' @{ fullName = "Minh Tran $suffix"; email = "minh.$suffix@example.com"; password = $pw }
Check 'Dang ky user B tra 201' ($b.Status -eq 201) "status=$($b.Status)"
$dup = Call POST '/auth/register' @{ fullName = 'Trung'; email = "lan.$suffix@example.com"; password = $pw }
Check 'Dang ky trung email tra 409' ($dup.Status -eq 409) "status=$($dup.Status)"
$tokenB = $b.Json.data.accessToken

# 3. Dang nhap (FR-AUTH-002) - sai mat khau 401, dung mat khau co token
$bad = Call POST '/auth/login' @{ email = "lan.$suffix@example.com"; password = 'Sai@12345' }
Check 'Dang nhap sai mat khau tra 401' ($bad.Status -eq 401) "status=$($bad.Status)"
$login = Call POST '/auth/login' @{ email = "lan.$suffix@example.com"; password = $pw }
Check 'Dang nhap dung tra 200 + JWT' ($login.Status -eq 200 -and $login.Json.data.accessToken) "status=$($login.Status)"
$tokenA = $login.Json.data.accessToken
$me = Call GET '/auth/me' $null $tokenA
Check 'GET /auth/me tra dung nguoi dung' ($me.Status -eq 200 -and $me.Json.data.email -eq "lan.$suffix@example.com")

# 4. Admin tao danh muc (FR-CAT-003)
$admin = Call POST '/auth/login' @{ email = $AdminEmail; password = $AdminPassword }
Check 'Admin dang nhap' ($admin.Status -eq 200) "status=$($admin.Status) - kiem tra -AdminEmail/-AdminPassword"
$tokenAdmin = $admin.Json.data.accessToken
$cat = Call POST '/categories' @{ name = "Mon nuoc $suffix"; description = 'Pho, bun, mien' } $tokenAdmin
Check 'Admin tao danh muc tra 201' ($cat.Status -eq 201) "status=$($cat.Status)"
$catId = $cat.Json.data.id; $catSlug = $cat.Json.data.slug
$catByAuthor = Call POST '/categories' @{ name = "Khong duoc $suffix" } $tokenA
Check 'Tac gia thuong KHONG tao duoc danh muc (403)' ($catByAuthor.Status -eq 403) "status=$($catByAuthor.Status)"

# 5. Tac gia A tao cong thuc nhap (FR-RCP-003)
$recipeBody = @{
    title = "Pho bo tai chin $suffix"; description = 'Pho bo Ha Noi nuoc trong, thom que hoi.'
    categoryId = $catId; prepTime = 30; cookTime = 180; servings = 4; difficulty = 'Medium'
    steps = @(@{ stepNumber = 1; title = 'Ninh xuong'; description = 'Ninh xuong bo 3 tieng voi lua nho.'; timerMinutes = 180 })
    ingredients = @(@{ name = 'Xuong bo'; quantity = 1.5; unit = 'kg'; orderIndex = 0 }, @{ name = 'Banh pho'; quantity = 1; unit = 'kg'; orderIndex = 1 })
}
$rc = Call POST '/recipes' $recipeBody $tokenA
Check 'Tao cong thuc tra 201, trang thai Draft' ($rc.Status -eq 201 -and $rc.Json.data.status -eq 'Draft') "status=$($rc.Status)"
$rid = $rc.Json.data.id; $rslug = $rc.Json.data.slug

# 6. Phan quyen: nguoi la khong xem duoc ban nhap; tac gia khac khong sua duoc
$anonDraft = Call GET "/recipes/$rid"
Check 'Khach KHONG xem duoc cong thuc nhap' ($anonDraft.Status -in 403, 404) "status=$($anonDraft.Status)"
$anonSteps = Call GET "/recipes/$rid/steps"
Check 'Khach KHONG xem duoc cac buoc cua ban nhap' ($anonSteps.Status -in 403, 404) "status=$($anonSteps.Status)"
$detailA = Call GET "/recipes/$rid" $null $tokenA
$editB = Call PUT "/recipes/$rid" @{ title = 'Bi sua trom' } $tokenB @{ 'If-Match' = $detailA.Json.data.rowVersion }
Check 'Tac gia B KHONG sua duoc cong thuc cua A (403)' ($editB.Status -eq 403) "status=$($editB.Status)"

# 7. Xuat ban (FR-RCP-005) va tim kiem khong dau (FR-SRCH-001)
$pub = Call PATCH "/recipes/$rid/publish" $null $tokenA
Check 'Xuat ban tra 200, trang thai Published' ($pub.Status -eq 200 -and $pub.Json.data.status -eq 'Published') "status=$($pub.Status)"
$pubDetail = Call GET "/recipes/$rslug"
Check 'Khach xem duoc cong thuc da xuat ban' ($pubDetail.Status -eq 200)
$search = Call GET "/recipes/search?q=pho%20bo%20$suffix"
Check 'Tim kiem khong dau thay cong thuc' ($search.Status -eq 200 -and (@($search.Json.data | Where-Object { $_.id -eq $rid }).Count -eq 1)) "status=$($search.Status)"
Check 'Ket qua tim kiem co meta phan trang' ($null -ne $search.Json.meta -and $search.Json.meta.total -ge 1)
$catDetail = Call GET "/categories/$catSlug"
Check 'Trang danh muc co cong thuc vua xuat ban' ($catDetail.Status -eq 200 -and (@($catDetail.Json.data.recipes.items | Where-Object { $_.id -eq $rid }).Count -eq 1)) "status=$($catDetail.Status)"
$short = Call GET '/recipes/search?q=p'
Check 'Tim kiem tu khoa < 2 ky tu tra 422' ($short.Status -eq 422) "status=$($short.Status)"

# 8. Huy xuat ban (FR-RCP-005) -> Draft, khach khong con thay
$unpub = Call PATCH "/recipes/$rid/unpublish" $null $tokenA
Check 'Huy xuat ban tra 200, trang thai Draft' ($unpub.Status -eq 200 -and $unpub.Json.data.status -eq 'Draft') "status=$($unpub.Status)"
$afterUnpub = Call GET "/recipes/$rid"
Check 'Huy xuat ban xong khach khong xem duoc' ($afterUnpub.Status -in 403, 404) "status=$($afterUnpub.Status)"

# 9. Xoa (FR-RCP-007, xoa mem)
$del = Call DELETE "/recipes/$rid" $null $tokenA
Check 'Xoa cong thuc tra 204' ($del.Status -eq 204) "status=$($del.Status)"
$gone = Call GET "/recipes/$rid" $null $tokenA
Check 'Cong thuc da xoa tra 404' ($gone.Status -eq 404) "status=$($gone.Status)"

# 10. Bao mat van hanh
$hangfire = RawStatus "$BaseUrl/hangfire"
Check 'Khach KHONG vao duoc /hangfire' ($hangfire -in 401, 403, 404) "status=$hangfire"
$trigger = Call POST '/jobs/sitemap/trigger'
Check 'Khach KHONG chay duoc job sitemap' ($trigger.Status -in 401, 403) "status=$($trigger.Status)"

Write-Host ""
Write-Host "Ket qua: $($script:pass) PASS, $($script:fail) FAIL" -ForegroundColor $(if ($script:fail -eq 0) { 'Green' } else { 'Yellow' })
exit $script:fail
