#requires -Version 7
<#
.SYNOPSIS
    重新生成 cmdpal 擴充功能模板自帶的 Asset PNG 檔案。

.DESCRIPTION
    cmdpal 模板預設生成的 PNG 在某次 commit 中被某個工具按 UTF-8 文本讀了，
    所有非 ASCII 位元組被替換成 U+FFFD，檔案已損壞（git blob 裡就是壞的）。

    此指令碼以倉庫裡乾淨的 translator.light.png (48x48) 為底圖，居中縮放貼到
    Store / WinUI3 包要求的各個尺寸畫布上，輸出標準 PNG。

    需要的原始檔 translator.light.png 必須是真實 PNG。所有輸出會覆蓋現有
    檔案。

.EXAMPLE
    pwsh .\cmdpal\generate-assets.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$assetsDir = Join-Path $PSScriptRoot 'PowerTranslatorExtension\Assets'
$source = Join-Path $assetsDir 'translator.light.png'

if (-not (Test-Path $source)) {
    throw "找不到源圖: $source"
}

Add-Type -AssemblyName System.Drawing

# 校驗源圖是真 PNG
$srcBytes = [System.IO.File]::ReadAllBytes($source)
if (-not ($srcBytes[0] -eq 0x89 -and $srcBytes[1] -eq 0x50 -and $srcBytes[2] -eq 0x4E -and $srcBytes[3] -eq 0x47)) {
    throw "源圖 translator.light.png 本身也是壞的（不是 PNG），無法繼續。"
}
Write-Host "源圖: $source ($(([System.Drawing.Image]::FromFile($source)).Width)x$([System.Drawing.Image]::FromFile($source).Height))" -ForegroundColor DarkGray

# 目標 (filename, w, h, [iconScale 0-1, 0.8 = 圖標占畫布 80% 邊長])
$targets = @(
    @{ Name = 'StoreLogo.png';                                        W = 50;   H = 50;  Scale = 0.85 },
    @{ Name = 'Square44x44Logo.scale-200.png';                        W = 88;   H = 88;  Scale = 0.85 },
    @{ Name = 'Square44x44Logo.targetsize-24_altform-unplated.png';   W = 24;   H = 24;  Scale = 1.00 },
    @{ Name = 'Square150x150Logo.scale-200.png';                      W = 300;  H = 300; Scale = 0.60 },
    @{ Name = 'Wide310x150Logo.scale-200.png';                        W = 620;  H = 300; Scale = 0.50 },
    @{ Name = 'SplashScreen.scale-200.png';                           W = 1240; H = 600; Scale = 0.30 },
    @{ Name = 'LockScreenLogo.scale-200.png';                         W = 48;   H = 48;  Scale = 0.85 }
)

$srcImg = [System.Drawing.Image]::FromFile($source)
try {
    foreach ($t in $targets) {
        $outPath = Join-Path $assetsDir $t.Name
        $canvas = New-Object System.Drawing.Bitmap($t.W, $t.H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($canvas)
        try {
            $g.Clear([System.Drawing.Color]::Transparent)
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

            # 等比縮放 + 居中
            $iconSide = [int]([Math]::Min($t.W, $t.H) * $t.Scale)
            $x = [int](($t.W - $iconSide) / 2)
            $y = [int](($t.H - $iconSide) / 2)
            $g.DrawImage($srcImg, $x, $y, $iconSide, $iconSide)
        } finally {
            $g.Dispose()
        }
        $canvas.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $canvas.Dispose()

        # 驗證產物
        $b = [System.IO.File]::ReadAllBytes($outPath)
        $magic = ($b[0..7] | ForEach-Object { '{0:x2}' -f $_ }) -join ' '
        $ok = $b[0] -eq 0x89 -and $b[1] -eq 0x50 -and $b[2] -eq 0x4E -and $b[3] -eq 0x47
        $status = if ($ok) { '✓' } else { '✗' }
        Write-Host ("  {0} {1,-50} {2,4}x{3,-4}  {4,6} B  magic={5}" -f $status, $t.Name, $t.W, $t.H, $b.Length, $magic)
    }
} finally {
    $srcImg.Dispose()
}

Write-Host "`n完成。下一步：" -ForegroundColor Cyan
Write-Host "  1) git add cmdpal/PowerTranslatorExtension/Assets/*.png"
Write-Host "  2) 重跑 pwsh .\cmdpal\build-store.ps1 出新 bundle"
