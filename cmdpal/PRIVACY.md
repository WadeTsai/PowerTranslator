# Privacy Policy / 隱私政策

_Last updated / 最後更新: 2026-05-25_

[English](#english) | [中文](#中文)

---

## English

### Overview

**PowerTranslator** (the "Extension") is an open-source translation extension for the Windows [Command Palette](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview). This document explains what data the Extension handles.

**Short version: The Extension does not collect, store, transmit, or share any personal information with the developer.** There is no analytics, no telemetry, no crash reporting, no user accounts, and no advertising.

### Information We Do Not Collect

The developer of the Extension does **not**:

- Collect personally identifiable information (name, email, IP address, device identifiers, etc.).
- Collect usage analytics or telemetry.
- Track your activity inside or outside the Extension.
- Operate any server that receives data from the Extension.
- Sell, rent, or share any data with advertisers or data brokers.

### Information Handled Locally on Your Device

The following data is processed **entirely on your own device** and never sent to the developer:

- **Text you submit for translation** — held in memory only for the duration of the request.
- **Settings and API keys** — stored by the Windows Command Palette settings system on your local machine. The Extension reads them at runtime but does not transmit them anywhere except to the third-party translation service you have explicitly selected and configured.
- **Clipboard content** — accessed only when you explicitly invoke a clipboard-based translation command. It is not logged, copied, or retained by the Extension.

### Third-Party Translation Services

To perform translation, the Extension sends the text you choose to translate to a third-party translation API of your choice. Currently supported providers include:

- **Youdao Translate** ([privacy policy](https://c.youdao.com/dict/law/youdao_dict_privacy.html))
- **DeepL** ([privacy policy](https://www.deepl.com/privacy/))

When you use these services:

- The text you submit for translation is sent over HTTPS directly from your device to the selected provider.
- If the provider requires an API key, that key is sent with the request as required by the provider's API.
- The handling of that data is governed by the provider's own privacy policy and terms of service, not by this Extension.

You can change or remove the selected provider at any time in the Extension's settings. If no provider is configured, no network requests are made.

### Permissions

The Extension declares the `internetClient` capability solely to communicate with the third-party translation services described above. It does not access any other network resources.

### Children's Privacy

The Extension is not directed to children under 13 and does not knowingly collect any information from them.

### Changes to This Policy

This privacy policy may be updated from time to time. Material changes will be reflected in the file in the project's GitHub repository, and the "Last updated" date at the top will be revised accordingly.

### Contact

Source code and issue tracker: <https://github.com/N0I0C0K/PowerTranslator>

For privacy-related questions, please open an issue in the repository above.

---

## 中文

### 概述

**PowerTranslator**（以下簡稱"本擴充功能"）是一款用於 Windows [命令面板（Command Palette）](https://learn.microsoft.com/zh-cn/windows/powertoys/command-palette/overview) 的開源翻譯擴充功能。本文件說明本擴充功能如何處理資料。

**簡短版本：本擴充功能不向開發者收集、儲存、傳輸或共享任何個人資訊。** 不包含任何分析、遙測、崩潰上報、使用者帳戶或廣告。

### 我們不收集的資訊

本擴充功能開發者**不會**：

- 收集任何個人身份資訊（姓名、郵箱、IP 地址、裝置識別碼等）。
- 收集使用分析資料或遙測資料。
- 跟蹤您在擴充功能內外的任何活動。
- 運營任何接收來自本擴充功能資料的伺服器。
- 向廣告商或資料經紀商出售、出租或共享任何資料。

### 僅在本地裝置處理的資訊

以下資料**完全在您自己的裝置上處理**，絕不會發送給開發者：

- **您提交的待翻譯文本** —— 僅在請求期間保留在記憶體中。
- **設定項與 API 金鑰** —— 由 Windows 命令面板的設定系統儲存在您本機。本擴充功能僅在執行時讀取，除傳送給您顯式選擇並配置的第三方翻譯服務外，不會傳輸到任何其他位置。
- **剪貼簿內容** —— 僅在您顯式觸發剪貼簿翻譯命令時才會訪問。本擴充功能不會記錄、複製或保留剪貼簿內容。

### 第三方翻譯服務

為執行翻譯，本擴充功能會將您選擇翻譯的文本傳送至您指定的第三方翻譯 API。目前支援的服務提供商包括：

- **有道翻譯**（[隱私政策](https://ai.youdao.com/DOCSIRMA/html/trans/price/wbfy/index.html)）
- **DeepL**（[隱私政策](https://www.deepl.com/zh/privacy/)）

當您使用上述服務時：

- 您提交的待翻譯文本通過 HTTPS 由您的裝置直接傳送至所選服務提供商。
- 如果該服務需要 API 金鑰，金鑰將依據該服務的 API 要求隨請求一同傳送。
- 上述資料的處理受所選服務提供商自身的隱私政策與服務條款約束，與本擴充功能無關。

您可以隨時在本擴充功能的設定中更改或移除所選的服務提供商。若未配置任何服務提供商，本擴充功能不會發起任何網路請求。

### 權限說明

本擴充功能聲明瞭 `internetClient` 權限，**僅**用於與上述第三方翻譯服務通訊，不會訪問任何其他網路資源。

### 兒童隱私

本擴充功能並非面向 13 歲以下兒童設計，也不會有意收集任何來自兒童的資訊。

### 政策變更

本隱私政策可能不定期更新。重大變更會反映在專案 GitHub 倉庫中的本檔案內，並相應更新頂部的"最後更新"日期。

### 聯絡方式

原始碼與問題跟蹤：<https://github.com/N0I0C0K/PowerTranslator>

如有隱私相關問題，請在上述倉庫中提交 Issue。
