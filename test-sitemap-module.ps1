# ==============================================================================
# SCRIPT KIEM THU TU DONG CHUC NANG SINH SITEMAP (TUAN 5 - HO QUOC TIEN)
# Chuc nang: FR-JOB-003 - Hangfire Job sinh sitemap.xml & Ping Search Engines
# ==============================================================================

param(
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Continue"

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  KIEM THU MODULE SITEMAP (TUAN 5) - HO QUOC TIEN (2312769)" -ForegroundColor Cyan
Write-Host "  Ma chuc nang: FR-JOB-003 / NFR-SEO" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

$allPassed = $true

# ------------------------------------------------------------------------------
# Test 1: Kich hoat job sinh sitemap thu cong qua API
# ------------------------------------------------------------------------------
Write-Host "`n[Test 1/3] Kich hoat job sinh sitemap thu cong (POST /api/v1/jobs/sitemap/trigger)..." -ForegroundColor Yellow
$triggerUrl = "$BaseUrl/api/v1/jobs/sitemap/trigger"

try {
    $resTrigger = Invoke-RestMethod -Uri $triggerUrl -Method Post -TimeoutSec 15
    if ($resTrigger.success -eq $true) {
        Write-Host "  [PASS] Trigger thanh cong: $($resTrigger.message)" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Trigger phan hoi khong thanh cong: $resTrigger" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  [FAIL] Loi khi goi POST /api/v1/jobs/sitemap/trigger: $_" -ForegroundColor Red
    $allPassed = $false
}

# ------------------------------------------------------------------------------
# Test 2: Lay noi dung sitemap.xml va kiem tra chuan XML sitemaps.org
# ------------------------------------------------------------------------------
Write-Host "`n[Test 2/3] Kiem tra endpoint GET /sitemap.xml..." -ForegroundColor Yellow
$sitemapUrl = "$BaseUrl/sitemap.xml"

try {
    $rawRes = Invoke-WebRequest -Uri $sitemapUrl -Method Get -TimeoutSec 15
    $statusCode = $rawRes.StatusCode
    $contentType = $rawRes.Headers["Content-Type"]
    $content = $rawRes.Content

    Write-Host "  -> HTTP Status: $statusCode" -ForegroundColor Gray
    Write-Host "  -> Content-Type: $contentType" -ForegroundColor Gray

    if ($statusCode -eq 200 -and $contentType -match "application/xml") {
        Write-Host "  [PASS] Endpoint tra ve HTTP 200 va Content-Type application/xml." -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] StatusCode hoac Content-Type khong dung!" -ForegroundColor Red
        $allPassed = $false
    }

    # Kiem tra cau truc XML
    [xml]$xmlDoc = $content
    $ns = "http://www.sitemaps.org/schemas/sitemap/0.9"

    if ($xmlDoc.urlset -and $xmlDoc.urlset.NamespaceURI -eq $ns) {
        Write-Host "  [PASS] XML Namespace dung chuan: $ns" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Namespace XML khong khop voi chuan sitemaps.org 0.9!" -ForegroundColor Red
        $allPassed = $false
    }

    $urls = $xmlDoc.urlset.url
    $count = ($urls | Measure-Object).Count
    Write-Host "  -> So luong URL co trong sitemap: $count" -ForegroundColor Gray

    if ($count -ge 3) {
        Write-Host "  [PASS] Sitemap chua day du cac URL mac dinh (trang chu, recipes, categories)." -ForegroundColor Green
        foreach ($u in $urls) {
            Write-Host "     * $($u.loc) [freq=$($u.changefreq), priority=$($u.priority), lastmod=$($u.lastmod)]" -ForegroundColor DarkGray
        }
    } else {
        Write-Host "  [FAIL] So luong URL qua it ($count < 3)" -ForegroundColor Red
        $allPassed = $false
    }

} catch {
    Write-Host "  [FAIL] Loi khi goi GET /sitemap.xml: $_" -ForegroundColor Red
    $allPassed = $false
}

# ------------------------------------------------------------------------------
# Test 3: Kiem tra Dashboard Hangfire (/hangfire)
# ------------------------------------------------------------------------------
Write-Host "`n[Test 3/3] Kiem tra Hangfire Dashboard (/hangfire)..." -ForegroundColor Yellow
$hangfireUrl = "$BaseUrl/hangfire"

try {
    $hfRes = Invoke-WebRequest -Uri $hangfireUrl -Method Get -TimeoutSec 15 -MaximumRedirection 5
    if ($hfRes.StatusCode -eq 200 -and ($hfRes.Content -match "Hangfire" -or $hfRes.Content -match "Dashboard")) {
        Write-Host "  [PASS] Hangfire Dashboard hoat dong binh thuong tai $hangfireUrl." -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Hangfire Dashboard tra ve Status: $($hfRes.StatusCode)" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  [FAIL] Loi khi truy cap Hangfire Dashboard: $_" -ForegroundColor Red
    $allPassed = $false
}

Write-Host "`n=========================================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "  KET QUA: TAT CA TEST CASE SITEMAP TUAN 5 DEU DAT (PASS)" -ForegroundColor Green
} else {
    Write-Host "  KET QUA: CO TEST CASE THAT BAI (FAIL)" -ForegroundColor Red
}
Write-Host "=========================================================" -ForegroundColor Cyan
