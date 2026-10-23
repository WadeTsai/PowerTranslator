<div align="center">
<h1>PowerTranslator</h1>
<p>一個用於<a href=https://github.com/microsoft/PowerToys>PowerToys Run</a>的翻譯外掛, 快速, 穩定.</p>
<p>支援 cmdpal</p>

![GitHub release (latest by date)](https://img.shields.io/github/v/release/N0I0C0K/PowerTranslator?style=flat-square) ![GitHub Repo stars](https://img.shields.io/github/stars/N0I0C0K/PowerTranslator?color=ffb900&style=flat-square) ![GitHub all releases](https://img.shields.io/github/downloads/N0I0C0K/PowerTranslator/total?style=flat-square) ![GitHub](https://img.shields.io/github/license/N0I0C0K/PowerTranslator?style=flat-square) [![Mentioned in Awesome PowerToys Run Plugins](https://awesome.re/mentioned-badge-flat.svg)](https://github.com/hlaueriksson/awesome-powertoys-run-plugins)

[English](./readme_en.md)

</div>

## About

一個用於[PowerToysRun](https://github.com/microsoft/PowerToys)的外掛。現已接入多個平臺提供**快速且穩定**的翻譯

### 翻譯來源

| 用途 | 來源 | 說明 |
|---|---|---|
| 英文單字/片語 → 繁體中文 | [ECDICT](https://github.com/skywind3000/ECDICT)（MIT） | 離線字典，隨安裝檔附帶；建置時以 OpenCC `s2twp` 轉成繁體（台灣用語） |
| 英英釋義 | [Free Dictionary API](https://dictionaryapi.dev) | 線上查詢，顯示在中文釋義之後，可在設定關閉 |
| 句子與其他語言 | DeepL | 字典查無結果時的備援；`auto` 時英文等譯為繁體中文、中日韓文字譯為英文 |

已移除有道、百度、阿里巴巴與劍橋等來源。

## Index

- [如何使用](#usage)
  - [cmdpal](#支援-cmdpal)
- [開發命令](#開發命令)
- [安裝](#install)
- [設定](#setting)
- [提交問題](#issue)
- [貢獻](#Contribution)

## 開發命令

專案根目錄的 [justfile](./justfile) 統一了開發、測試和打包命令。執行 `just` 或 `just help` 可檢視完整列表。

ECDICT 資料檔 `data/ecdict.bin`（約 57MB，不納入 git）由 `tools/ecdict/build_ecdict.py` 產生：它會下載固定版本的 ECDICT，再用 OpenCC 轉成繁體。建置時若找不到資料檔會自動嘗試產生，也可以手動執行：

```powershell
pip install opencc
just ecdict          # 或 python tools/ecdict/build_ecdict.py
```

```powershell
# 傳統 PowerToys Run 外掛
just legacy-build-all
just legacy-pack-all

# Command Palette 擴充功能
just test
just cmdpal-build-all
just cmdpal-install-local
just cmdpal-clean-local
just cmdpal-store
```

`cmdpal-install-local` 會構建、自簽名並安裝實際的本地 `.msix`，適合 e2e 驗證；其臨時證書位於被 Git 忽略的 `cmdpal/.local-cert/`，密碼使用當前使用者的 DPAPI 加密。為滿足 Windows 的 MSIX 安裝規則，首次執行會經 UAC 確認把公鑰匯入 `LocalMachine\TrustedPeople`。若檢測到舊擴充功能正在執行，會先展示 PID 並等待確認後才停止它。`cmdpal-clean-local` 也會經 UAC 刪除該信任及證書目錄；不會解除安裝應用。

`cmdpal-store` 會構建 x64 和 ARM64 的 Microsoft Store `.msixbundle` 到 `dist/`；構建前仍會執行現有指令碼中的應用清單檢查。

## Usage

- 預設觸發鍵為`|`.（下方GIF演示採用的自定義觸發鍵`^`）
- 指定翻譯文本

  - `alt+space`開啟 PowerToysRun, 輸入`^[你要翻譯的地方]`
  - `Enter`複製翻譯結果到剪貼簿

  演示翻譯 `command`
  ![command](Images/command.gif)

  演示翻譯 `命令`
  ![chinese](Images/%E5%91%BD%E4%BB%A4.gif)

- 中文帶有拼音
  參考上方演示翻譯`命令`

- 快速翻譯剪貼簿

  - 當剪貼簿內有文字, 直接鍵入觸發關鍵字`^`, 即可快速翻譯剪貼簿內的內容

    ![clipboard](Images/clipboard.gif)

- 指定翻譯目標語言

  - 使用 `^[words]->[Target language]`, 例如: `^你好->ja` 表示把你好翻譯為日文
    ![Specified language](Images/target%20lan.gif)
    常用語言代碼
    |語言|代碼|備註|
    |---------|------|-|
    |漢語(簡體) | zhs | 漢語簡體
    |漢語(繁體) | zht| 漢語翻譯
    |日語 |ja| 日本語
    |英語 |en| English
    |韓語 |ko| 한국어
    |法語 |fr| En français
    |俄語 |ru| русский

- 搜尋建議

  - 根據當前的輸入，從離線 ECDICT 建議英文單字（附繁體中文釋義）
    ![suggest](Images/suggest.gif)
  - 可以在設定裡選擇是否啟用建議, 對應`Enable search suggest`

- 朗讀內容

  - 支援朗讀中文, 英文(快捷鍵 ctrl+enter)
    ![read](Images/read.png)
  - 使用 Windows 內建語音合成（中文優先使用 zh-TW 語音）
  - 支援自動朗讀結果功能, **預設關閉**, 可以在設定開啟.對應`Automatic reading result`

- 歷史記錄
  - 鍵入`h`或者剪貼簿內無翻譯目標會顯示歷史翻譯記錄.
    ![his](Images/his.png)
  - 預設 **15** 條記錄
  - **為什麼不支援自定義上限條目?**
    因為官方只開放了 bool 型別的自定義引數, 所以目前不支援自定義歷史記錄上限. 等待後續支援我會更新.或者可以下載原始碼自行修改編譯.

- 第二語言
  - 開啟第二翻譯目標語言，預設關閉
  ![second option](Images/second_option.png)
  - 開啟後會在每次翻譯的時候自動展示第二語言結果
  ![second](Images/second.png)


### 支援 Cmdpal
所有 Powertoys Run 的功能同樣也支援在 cmdpal 中使用
![cmdpal](Images/cmdpal_translate.png)


## Install

### Cmdpal

通過 MicroStore 安裝 - [商店頁面](https://apps.microsoft.com/store/detail/9NWN01SZ6KNK?cid=DevShareMCLPCS)

### PowerToys Run 外掛

1. 關閉 PowerToys
2. 下載 [Translator.zip](https://github.com/N0I0C0K/PowerTranslator/releases)
3. 解壓到`%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins`
   如圖所示
   ![file](Images/file.png)
4. 啟動 PowerToys

(參考官方[第三方外掛安裝文件](https://github.com/microsoft/PowerToys/blob/main/doc/thirdPartyRunPlugins.md))

---

Via [ptr](https://github.com/8LWXpg/ptr)

```shell
ptr add Translator N0I0C0K/PowerTranslator
```

[安裝-升級 詳細教程](./doc/how%20to%20install.md)

## Setting

- 如果觸發按鍵衝突, 修改預設觸發鍵 (建議修改為" \` ", 同時建議檢查一下有無衝突)。**注意在v0.8之後預設觸發按鍵是`"|"`，無需再次修改**
  ![change active key](Images/change_active.png)
- 建議勾選`輸入平滑`, 可以最佳化輸入體驗
  ![enable Smooth input](Images/enable%20Smooth%20input.png)
- `啟動時清除上一查詢`可以快速觸發翻譯剪貼簿, **按需求勾選.**
  ![auto clean](Images/auto_clean.png)
- 切換預設目標翻譯語言，預設是`auto`
  ![languages](Images/languages.png)
- 目標語言為 `zht` 或 `auto` 時，英文單字與短語（最多 4 個單字）會先查離線 ECDICT 字典；查無結果才改用 DeepL。
- `Show English definitions`（預設開啟）：同時向 [Free Dictionary API](https://dictionaryapi.dev) 查詢英英釋義；目標語言為 `en` 時只顯示英英釋義。

## Issue

如果發現了無法翻譯, 出現了未知錯誤, 需要新的功能, 歡迎提交 issue. 我會及時修復或改進

## Contribution

### Environment

- .net sdk 8.0

### Step

1. fork 這個倉庫
2. clone fork 後的倉庫到本地
3. 執行 `dotnet restore`(可選)
3. 使用 vscode（任意） 開啟
5. 開始編碼吧！

[幫助文件 - Getting Start!](doc/code-start-zh.md)
