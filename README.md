<div align="center">

# Haiyu

**一款好看高性能的库洛游戏启动器**

*鸣潮 · 战双 ·*

<br/>

<img src="img/home.png" alt="主界面预览" width="600"/>

<br/>

[![.NET](https://img.shields.io/badge/dotnet-10.0-512bd4?style=flat-square)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/CSharp-14.0-512bd4?style=flat-square)](https://learn.microsoft.com/dotnet/csharp/)
[![WinUI3](https://img.shields.io/badge/WindowsAppSdk-2.0-blue?style=flat-square)](https://learn.microsoft.com/windows/apps/windows-app-sdk/)

</div>

---

## ✨ 它能干什么？

### 🎮 游戏管理

管理你的鸣潮和战双游戏文件——下载、更新、校验。国服、国际服、B服、台服等四种渠道。

### 📊 库街区集成

支持手机号、Token 登录库街区，登录后支持扫描二维码登陆游戏，以及数据面板信息、自动签到等功能。

### ☁️ 云游戏

集成云鸣潮，可在启动器中一键登录游玩，以及抽卡记录分析等功能。

### 📸 截图功能

支持自定义功能键截图。

---

## 🚀 下载安装

去 [Release](https://github.com/BlameTwo/WutheringWavesTool/releases) 页面拿最新版。

Microsoft Store 也可以直接装👇

[<img src="https://get.microsoft.com/images/zh-cn%20dark.svg" width="200"/>](https://apps.microsoft.com/detail/9p70bt6bvwfh?hl=zh-CN&gl=CN)

---

## 🤝 致谢

感谢 [扑克](https://github.com/Poker-sang) 和 [ghost1372](https://github.com/ghost1372) 的前期指导。

本项目使用了这些优秀的开源项目：[WindowsAppSdk](https://github.com/microsoft/WindowsAppSDK) · [DevWinUI](https://github.com/ghost1372/DevWinUI) · [CommunityToolkit](https://github.com/CommunityToolkit) · [WinUIEx](https://github.com/dotMorten/WinUIEx) · [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon) · [Win2D](https://github.com/microsoft/Win2D)

---

## 📄 开源协议

本项目基于 [LICENSE](LICENSE.txt) 开源。

*用 ❤️ 和过量咖啡因打造*

---

## 🛠️ 对于开发者了解此项目

### 技术栈

| 层级  | 技术                                      |
| --- | --------------------------------------- |
| 框架  | .NET 10 + WinUI 3 (Windows App SDK 2.0) |
| 语言  | C# preview+                             |
| 模式  | MVVM+DI Service                         |
| 打包  | MSIX / 独立 EXE 双模式发布                     |

### 项目结构速览（不完整）

```
src/
├── WutheringWavesTool/     ← 主应用（UI、页面、ViewModel、服务）
├── Waves.Core/             ← 核心引擎（游戏上下文、下载、账号、事件系统）
├── Waves.Api/              ← API 模型与数据契约（和库洛服务器打交道的层）
├── Haiyu.Plugin/           ← 插件系统（Contracts + Native 互操作）
├── Haiyu.ServiceHost/      ← RPC 服务宿主（WebSocket 通信桥梁）
├── Astronomical/           ← 天文计算模块（月相、日出日落，浪漫但实用，已弃用）
├── LanguageEditer/         ← 多语言编辑器（独立小工具，方便翻译）
├── KuroGameDownloadProgram/← 下载测试
└── Server/HaiyuServer/     ← 服务端
```

### 怎么跑起来以及编译注意事项

1. 注意PC编译环境配置，由于开启了AOT兼容，所有类型边界为强类型，编译环境会全开检查，在Visual Studio 2026编译环境中，Devhub.exe的RAM占用异常高，因此推荐配置CPU单核主频越高越好，RAM最低要求32GB，硬盘无要求。
2. 关于第一点的解释，在IDE中进行不断地跳转代码页时，IDE会自身的增量分析这些文件（编译会全量分析），如果RAM内存和磁盘交换不及时则会造成代码分析缓慢、报错信息消失、语法检查消失等严重的问题。
3. **Visual Studio 2026**，负载：.Net 桌面开发、Windows开发模板，Windows SDK
4. 克隆仓库，打开 `src/WutheringWavesTool.slnx`

> 想打独立 EXE 包？看 [发布方式说明](src/ReadMe.md)

### 想深入？

可以翻阅以下AI生成的技术文档查看：

- [项目结构与布局](https://zread.ai/BlameTwo/Haiyu/3-project-structure-and-layout)
- [WinUI3 应用启动流程](https://zread.ai/BlameTwo/Haiyu/5-winui3-application-bootstrap-flow)
- [游戏上下文 V2 生命周期与状态机](https://zread.ai/BlameTwo/Haiyu/8-game-context-v2-lifecycle-and-state-machine)

---
