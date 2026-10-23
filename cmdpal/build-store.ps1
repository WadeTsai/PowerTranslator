#requires -Version 7
<#
.SYNOPSIS
    構建用於 Microsoft Store 提交的 .msixbundle（x64 + arm64）。

.DESCRIPTION
    本專案是單專案 MSIX (EnableMsixTooling=true)，老 UWP 的
    `AppxBundle=Always` / `UapAppxPackageBuildMode=StoreUpload` 不生效，
    必須分別為每個架構跑一次 msbuild，再用 Windows SDK 的 MakeAppx.exe
    手動合成 .msixbundle。

    流程：
      1) 校驗 Package.appxmanifest 身份資訊；
      2) 清掉舊 AppPackages；
      3) 分別對 x64 / arm64 跑 msbuild（不簽名，Store 會重簽）；
      4) 收集兩個單架構 .msix 到臨時目錄；
      5) MakeAppx.exe bundle 合成 .msixbundle；
      6) 拷到倉庫根 dist/。

    Partner Center 直接接受 .msixbundle 上傳（也接受 .msixupload，但單專案
    MSIX 沒有原生的 .msixupload 產出路徑，bundle 已足夠）。

.PARAMETER Configuration
    構建配置，預設 Release。

.PARAMETER Platforms
    bundle 裡要包含的架構，預設 @('x64', 'arm64')。

.PARAMETER SkipManifestCheck
    跳過 Package.appxmanifest 佔位符檢測。CI 或確知無誤時使用。

.EXAMPLE
    pwsh .\cmdpal\build-store.ps1

.EXAMPLE
    pwsh .\cmdpal\build-store.ps1 -Platforms x64
#>

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('x64', 'arm64', 'ARM64')]
    [string[]]$Platforms = @('x64', 'arm64'),

    [switch]$SkipManifestCheck
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# === 路徑常量 ===
$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$projectDir = Join-Path $PSScriptRoot 'PowerTranslatorExtension'
$csproj = Join-Path $projectDir 'PowerTranslatorExtension.csproj'
$manifest = Join-Path $projectDir 'Package.appxmanifest'
$distDir = Join-Path $repoRoot 'dist'

if (-not (Test-Path $csproj)) {
    throw "找不到專案: $csproj"
}

# 規範化平臺名：msbuild 接受 'x64' / 'ARM64'（注意大小寫）
$Platforms = $Platforms | ForEach-Object {
    if ($_ -ieq 'arm64') { 'ARM64' } else { 'x64' }
} | Select-Object -Unique

# === 1) 找 msbuild ===
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "找不到 vswhere.exe。請安裝 Visual Studio 2026（或 Build Tools 2026） (含 'Windows App SDK C# templates' / 'WinUI 應用開發' 工作負載)。"
}
$msbuildPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuildPath) {
    throw "vswhere 沒找到 msbuild。請確認已裝 'MSBuild' 元件。"
}
Write-Host "msbuild  : $msbuildPath" -ForegroundColor DarkGray

# === 2) 找 MakeAppx.exe（取最新版本，優先匹配 csproj 鎖定的 SDK 版本） ===
$sdkRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
if (-not (Test-Path $sdkRoot)) {
    throw "找不到 Windows SDK 目錄: $sdkRoot"
}
$makeAppxPath = Get-ChildItem $sdkRoot -Recurse -Filter 'makeappx.exe' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } |
    Sort-Object { [version]($_.Directory.Parent.Name) } -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $makeAppxPath) {
    throw "找不到 MakeAppx.exe。請安裝 Windows 10/11 SDK。"
}
Write-Host "makeappx : $makeAppxPath" -ForegroundColor DarkGray

# === 3) 檢查 manifest ===
if (-not $SkipManifestCheck) {
    [xml]$xml = Get-Content $manifest -Raw
    $identity = $xml.Package.Identity
    $name = "$($identity.Name)"
    $publisher = "$($identity.Publisher)"
    $version = "$($identity.Version)"

    Write-Host "`n=== manifest 身份檢查 ===" -ForegroundColor Cyan
    Write-Host "  Name      : $name"
    Write-Host "  Publisher : $publisher"
    Write-Host "  Version   : $version"

    $warnings = @()
    $placeholderNames = @('PowerTranslatorExtension')
    $placeholderPublishers = @('CN=N0I0C0K')
    if ($name -in $placeholderNames) {
        $warnings += "Identity.Name 還是預設值 '$name'。Partner Center 給的應是 'Publisher.AppName' 形式（如 NickXii.PowerTranslator）。"
    }
    if ($publisher -in $placeholderPublishers) {
        $warnings += "Publisher 還是預設值 '$publisher'，必須改成 Partner Center 給的 'CN=<GUID>' 形式。"
    }
    if ($publisher -notmatch '^CN=') {
        $warnings += "Publisher '$publisher' 不是 'CN=...' 形式，Store 會拒收。"
    }
    if ($version -notmatch '^\d+\.\d+\.\d+\.0$') {
        $warnings += "Version '$version' 末位不是 0。Store 要求 X.Y.Z.0 格式（末位保留給重新提交）。"
    }

    if ($warnings.Count -gt 0) {
        Write-Warning "以下問題會導致 Store 拒收，強烈建議先在 Package.appxmanifest 裡改好："
        foreach ($w in $warnings) { Write-Warning "  - $w" }
        $resp = Read-Host "`n是否繼續構建？(y/N)"
        if ($resp -ne 'y' -and $resp -ne 'Y') {
            Write-Host "已取消。" -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Host "✓ 看起來沒有佔位符。" -ForegroundColor Green
    }
} else {
    [xml]$xml = Get-Content $manifest -Raw
    $name = "$($xml.Package.Identity.Name)"
    $version = "$($xml.Package.Identity.Version)"
}

# === 4) 清舊產物 ===
Write-Host "`n清理舊 AppPackages..." -ForegroundColor DarkGray
foreach ($p in @('x64', 'ARM64', 'arm64')) {
    $platBin = Join-Path $projectDir "bin\$p"
    if (Test-Path $platBin) {
        Get-ChildItem $platBin -Filter 'AppPackages' -Recurse -Directory -ErrorAction SilentlyContinue |
            ForEach-Object {
                Write-Host "  rm $($_.FullName)" -ForegroundColor DarkGray
                Remove-Item $_.FullName -Recurse -Force
            }
    }
}

# === 5) 分平臺 msbuild ===
$producedMsix = @()
foreach ($plat in $Platforms) {
    Write-Host "`n=== msbuild $plat ===" -ForegroundColor Cyan
    $args = @(
        $csproj,
        '/restore',
        '/nologo',
        '/v:minimal',
        "/p:Configuration=$Configuration",
        "/p:Platform=$plat",
        '/p:GenerateAppxPackageOnBuild=true',
        '/p:AppxPackageSigningEnabled=false'
    )
    & $msbuildPath @args
    if ($LASTEXITCODE -ne 0) {
        throw "msbuild $plat 退出碼 $LASTEXITCODE"
    }

    # 找出這次 build 產出的 msix：bin\<plat> 下、不在 Dependencies 子目錄、
    # 檔名帶本次版本與架構，取最新寫入的那個。
    # 新版 MSIX 工具鏈把 msix 放在 bin\<plat>\<cfg>\<tfm>\<rid>\，不再有 AppPackages 子目錄。
    $platBin = Join-Path $projectDir "bin\$plat"
    $msix = Get-ChildItem $platBin -Recurse -Filter "*_${version}_$plat.msix" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Extension -eq '.msix' -and
            $_.FullName -notmatch '\\Dependencies\\'
        } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $msix) {
        $hint = Get-ChildItem $platBin -Recurse -Filter '*.msix*' -ErrorAction SilentlyContinue |
            ForEach-Object { "    $($_.FullName)" }
        throw ("沒在 $platBin 下找到 $plat 的 .msix。當前候選：`n" + ($hint -join "`n"))
    }
    Write-Host "  ✓ $($msix.Name)" -ForegroundColor Green
    $producedMsix += $msix.FullName
}

# === 6) MakeAppx bundle ===
Write-Host "`n=== 合成 .msixbundle ===" -ForegroundColor Cyan
$bundleStaging = Join-Path $env:TEMP "ptbundle_$(Get-Random)"
New-Item -ItemType Directory -Path $bundleStaging | Out-Null
try {
    foreach ($m in $producedMsix) {
        Copy-Item $m -Destination $bundleStaging
    }

    if (-not (Test-Path $distDir)) {
        New-Item -ItemType Directory -Path $distDir | Out-Null
    }
    $bundleName = "${name}_${version}_x64_arm64.msixbundle"
    $bundlePath = Join-Path $distDir $bundleName

    if (Test-Path $bundlePath) {
        Remove-Item $bundlePath -Force
    }

    Write-Host "  staging : $bundleStaging" -ForegroundColor DarkGray
    Write-Host "  output  : $bundlePath" -ForegroundColor DarkGray
    & $makeAppxPath bundle /d $bundleStaging /p $bundlePath /bv $version /o
    if ($LASTEXITCODE -ne 0) {
        throw "MakeAppx bundle 退出碼 $LASTEXITCODE"
    }

    $info = Get-Item $bundlePath
    $sizeMB = [Math]::Round($info.Length / 1MB, 2)
    Write-Host ("`n✓ {0}  ({1} MB)" -f $bundlePath, $sizeMB) -ForegroundColor Green
} finally {
    if (Test-Path $bundleStaging) {
        Remove-Item $bundleStaging -Recurse -Force
    }
}

Write-Host "`n下一步：" -ForegroundColor Cyan
Write-Host "  1) 登入 https://partner.microsoft.com/dashboard"
Write-Host "  2) 選你的 app → Submissions → Start your submission"
Write-Host "  3) Packages 頁面 ★只傳 dist\$bundleName★ 這一個檔案"
Write-Host "     —— 不要順手把 cmdpal\PowerTranslatorExtension\bin\... 下的 .msix 一起拖上去，"
Write-Host "        那些是 bundle 的零件，和 bundle 同時上傳會讓 Partner Center 卡在'處理中'狀態。"
Write-Host "  4) 'Device family availability' 勾上 Windows 10/11 Desktop（其餘不勾）"
Write-Host "  5) 填 listing / pricing / capability justification → Submit"
