# RBI表格翻译助手 v1.0.0

WPS 表格内的中 / 英 / 印尼语三语互译插件。基于智谱 GLM 大模型，源语种自动识别，支持自定义专业术语表。专为多语种工厂文档场景打造。

## ✨ 功能特性

- **三语互译**：中文、英文、印尼语任意方向互译，源语种自动识别，目标语种一键切换
- **专业术语表**：内置可自定义的术语表，按目标语种分别保存，确保行业术语翻译统一准确
- **双语对照输出**：原文保留，译文换行追加，方便对照
- **批量并发翻译**：支持选区 / 当前表 / 整个工作簿翻译，多路并发，整表数百条数十秒完成
- **本地缓存**：相同文本不重复调用 API，省额度、提速度
- **凭据加密**：API Key 由本机助手通过 Windows DPAPI 加密保存，不写入工作簿、不上传
- **自动备份**：翻译前可自动创建原文件副本

## 🚀 快速开始

1. 下载 `YN-WPS-Indonesian-Translator-1.0.0.zip` 并解压
2. **关闭 WPS**（先运行 `kill.cmd` 结束相关进程）
3. 运行 `install.cmd` 完成安装
4. 打开 WPS 表格，在「RBI表格翻译助手」选项卡 →「API 设置」中：
   - 填入智谱 API Key（在 [智谱开放平台](https://open.bigmodel.cn) → 个人中心 获取，GLM-4-Flash 免费）
   - 选择目标语种，保存并测试
5. 选中单元格，点击「翻译选区 / 翻译当前表 / 翻译工作簿」即可

## 📋 环境要求

- Windows + WPS Office（仅支持 WPS 表格）
- 可访问智谱 API 的网络环境

## 🔧 技术说明

- 翻译引擎采用兼容 OpenAI 协议的接口，默认对接智谱 GLM-4-Flash；可在「API 设置」中修改接口地址 / 模型，切换至 Gemini、硅基流动等其他服务
- 本机后台助手处理 API 调用，规避浏览器跨域与签名问题

---

Developed by RBI IE · Jerry


 # RBI Sheet Translator v1.0.0

A Chinese / English / Indonesian translation add-in for WPS Spreadsheets, powered by Zhipu GLM. Automatically detects the source language and supports a customizable terminology glossary — built for multilingual factory documentation.

## ✨ Features

- **Tri-lingual translation**: Translate between Chinese, English, and Indonesian in any direction, with automatic source-language detection and one-click target switching
- **Professional glossary**: Built-in customizable glossary, stored separately per target language, for consistent and accurate industry terminology
- **Bilingual output**: Keeps the original text and appends the translation on a new line for easy comparison
- **Concurrent batch translation**: Translate a selection / sheet / entire workbook with multi-threaded concurrency — hundreds of cells in seconds
- **Local cache**: Identical text is never re-sent to the API, saving quota and time
- **Encrypted credentials**: The API key is encrypted via Windows DPAPI by a local helper — never written to the workbook or uploaded
- **Auto backup**: Optionally creates a backup of the original file before translating

## 🚀 Quick Start

1. Download and extract `YN-WPS-Indonesian-Translator-1.0.0.zip`
2. **Close WPS** (run `kill.cmd` to end related processes first)
3. Run `install.cmd` to install
4. Open WPS Spreadsheets, go to the **RBI Sheet Translator** tab → **API Settings**:
   - Enter your Zhipu API key (get one at [bigmodel.cn](https://open.bigmodel.cn) → Profile; GLM-4-Flash is free)
   - Choose the target language, then save and test
5. Select cells and click **Translate Selection / Sheet / Workbook**

## 📋 Requirements

- Windows + WPS Office (WPS Spreadsheets only)
- Network access to the Zhipu API

## 🔧 Technical Notes

- The translation engine uses an OpenAI-compatible API, defaulting to Zhipu GLM-4-Flash. You can change the endpoint / model in **API Settings** to switch to Gemini, SiliconFlow, or other providers.
- A local background helper handles API calls, avoiding browser CORS and request-signing issues.

---

Developed by RBI IE · Jerry
