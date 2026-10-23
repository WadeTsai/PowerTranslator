# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.13.0] - 2026-10-23

### Added
- DeepL 被限流或無法連線時改用 Google 翻譯備援；DeepL 回傳 429 後暫停 60 秒，期間直接使用 Google
- 快取最近的翻譯結果，重複查詢不再發送請求
- 所有翻譯服務都失敗時顯示實際原因（例如 `DeepL: rate-limited (HTTP 429)`）
- 目標語言為 `en`（例如第二目標語言）時，英文單字顯示英英釋義；線上字典查不到時改用 ECDICT 內建的離線英英釋義（WordNet）

### Fixed
- 第二目標語言常顯示「No translation returned」（DeepL 免費端點限流）
- 「跳至辭典」改為帶入英文單字（輸入的英文，或中日韓輸入的翻譯結果），不再帶入翻譯後的文字
- 第二目標語言未套用 `zht` / `zhs` 等語言代碼簡寫
- `cmdpal/build-store.ps1` 適配新版 MSIX 工具鏈的輸出路徑

### Changed
- 英文單字/片語改用離線 [ECDICT](https://github.com/skywind3000/ECDICT) 查詢繁體中文釋義（建置時以 OpenCC `s2twp` 轉換，隨安裝檔附帶）
- 新增 [Free Dictionary API](https://dictionaryapi.dev) 英英釋義（設定 `Show English definitions`，預設開啟）
- 句子改由 DeepL 翻譯；`auto` 時英文等譯為繁體中文，並強制輸出繁體字
- 搜尋建議改用離線 ECDICT，朗讀改用 Windows 內建語音合成
- 跳轉字典預設改為劍橋，新增劍橋英漢（繁體）

### Removed
- 移除有道（含 TTS）、百度建議、阿里巴巴翻譯與劍橋線上查詢

## [0.12.0] - 2025-06-09

### Added
- 支援了語言代碼簡寫. 現在你可以使用 `zhs` 用來指代 `zh-CHS`
- 添加了 DeepL 翻譯(因為對頻率的限制較強，未將其設為預設翻譯引擎)
- 支援翻譯程式碼變數（在snake_case或camelCase）。例如，`hello_world` 將被當作 `hello world`
- 新增幫助列表, 支援快速檢視語言代碼

### Fixed
- 修復“是否使用代理設定”在軟體啟動時未正確設定

## [0.11]

### Added
- 新增開啟詞典快捷方式，自帶 有道、牛津、劍橋 詞典快捷方式
- 新增若干可選語言

## [0.10.1] - 2024-01-20

### Fixed
- 修復了複製時預設帶上音標的錯誤
- 內地介面之後不再使用系統預設代理

## [0.10.0] - 2024-01-14

### Added
- 新增第二語言選項
- 為單個單詞新增音標顯示

## [0.9.0] - 2023-10-01

### Added
- 新增了預設目標語言選項

## [0.8.0] - 2023-09-21

### Added
- 改進了資訊的展示
- 改進了初始化過程

### Fixed
- 修復了介面失效問題
- 修復了一個接口出現問題後的介面切換錯誤

## [0.7.1] - 2023-05-13

### Fixed
- 修復了歷史記錄功能會錯誤地將空查詢記錄
- 優化了初始化速度，此前的版本初始化速度為 **6020ms**，最佳化後為 **3ms**（其實總的初始化時間是不會改變的，只是將比較耗時的部分移動到了後臺執行）

## [0.7.0] - 2023-04-10

### Added
- 新增歷史記錄功能，鍵入 `h` 檢視歷史翻譯

### Fixed
- 修復了翻譯被特殊字元截斷的問題
- 優化了初始化速度

## [0.6.0] - 2023-03-09

### Added
- 新增朗讀功能，支援中英文朗讀，快捷鍵 `Ctrl+Enter`
- 新增自動朗讀結果（預設關閉）

### Changed
- 專案名稱更名為 `PowerTranslator`

## [0.5.1] - 2023-02-15

### Added
- 新增了備用介面，增加了穩定性

## [0.5.0] - 2023-02-14

### Added
- 新增了翻譯 API，整體遷移到新 API
- 新增了出現錯誤尋求幫助的快捷方式

### Fixed
- 修改了初始化邏輯

## [0.4.0]

### Added
- 新增了搜尋建議

### Fixed
- 修復了之前搜尋的卡頓問題
- 修復了無法在中間輸入的問題
