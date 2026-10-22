# Getting Start

## 結構

程式的入口是官方提供的 IPlugin 介面，通過 query 函式觸發。下面講解各個類的主要作用

### Translator

外掛的各個基礎功能，外掛的屬性，實現官方的各種基礎介面（比如 設定、查詢）

### Utils

工具空間，通用功能

### TranslateHelper

翻譯功能的轉接器，向上層（Transltor）提供可直接呼叫的翻譯介面函式，向下負責排程各個翻譯 API 的實現，確保翻譯的穩定和快速

### SuggestHelper

建議功能的轉接器，目前只有一個 suggest 介面故同時集成了建議功能的實現

### HistoryHelper

歷史功能的轉接器

### ITransltor

具體的翻譯實現（通常是網路介面呼叫）

目前已經實現的翻譯介面有：
- 有道翻譯網頁版本
- 有道翻譯 api 試用版本（用於 backup）
- 有道翻譯老版本網頁

計劃中的翻譯介面實現：
- 微軟翻譯
- 谷歌翻譯

### SettingHelper

提供設定介面，主要是在 Power toys 介面中顯示的設定

## 如何新增一個翻譯實現

假設我們要新增 Google 翻譯

1. 新建一個資料夾 Google
2. 新建 GoogleTransltor.cs
3. 新建 class GoogleTransltor 繼承自 ITranslater，實現介面。
4. 在 TranslateHelper 中的建構函式裡的 translatorTypes 中新增 GoogleTransltor。
5. 測試是否成功呼叫

就這樣你就添加了一個新的翻譯介面了！


## 測試 and 打包

測試的一般步驟是：
1. 編寫好程式碼後，使用 build 命令生成外掛 dll
2. 放到外掛目錄下後重新執行 Power toys run

### build

執行 `dotnet build -p:Platform={x64|ARM64}` 會在 `/bin/output` 下生成完整的外掛檔案，可以直接拖到外掛目錄下完成安裝

### 測試用例

目前還沒有測試用例的編寫，考慮在後期加上

### pack

自己手動測試沒有問題後，執行 `dotnet pack` 打包命令，會生成 `/bin/Translator_{x64|ARM64}.zip` 就是完整的外掛檔案了

pack 和 build 命令可以通過修改 [Translater.csproj](../Translater.csproj) 檔案修改