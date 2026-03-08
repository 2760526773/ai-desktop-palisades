# AI Desktop Palisades

<p align="center">
  <strong>Windows 透明桌面栅栏 + AI 自动分类 + 物理归档</strong>
</p>

<p align="center">
  A lightweight desktop fence tool for Windows with AI-powered desktop classification, physical archiving, and drag-and-drop organization.
</p>

<p align="center">
  <a href="https://github.com/Xstoudi/Palisades">Upstream: Palisades</a> ·
  <a href="https://github.com/Twometer/NoFences">Inspired by NoFences</a> ·
  <a href="https://www.stardock.com/products/fences/">Reference: Fences</a>
</p>

---

## 中文简介

`AI Desktop Palisades` 是一个面向 Windows 10/11 的桌面整理工具，基于透明桌面栅栏交互，加入了 AI 模型分类能力。

它的目标不是简单地“复制一份桌面图标到栅栏里显示”，而是：

- 扫描桌面第一层文件、文件夹、快捷方式
- 使用 AI 模型判断项目类型
- 自动把项目归档到对应栅栏
- 支持从桌面拖入栅栏、从栅栏拖回桌面
- 支持常用右键操作与多模型切换

这是一个基于开源项目二次开发的改造版本，重点在于把原本的桌面栅栏体验扩展为“可接入主流 AI 模型的桌面分类整理工具”。

---

## English Overview

`AI Desktop Palisades` is a Windows desktop organization tool built around transparent desktop fences, extended with AI-powered desktop item classification.

Its goal is not to simply mirror desktop shortcuts inside fences, but to:

- scan top-level desktop files, folders, and shortcuts
- classify them with AI models
- physically archive items into categorized fences
- support drag-in / drag-out interactions
- provide practical desktop-style actions and multi-provider AI switching

This is a modified fork of an open-source desktop fence project, focused on turning it into an AI-assisted desktop organizer.

---

## Upstream And Credits | 项目来源与致谢

This project is based on the following open-source work:

- Upstream project / 原始项目: [Xstoudi/Palisades](https://github.com/Xstoudi/Palisades)
- Inspiration / 灵感来源: [Twometer/NoFences](https://github.com/Twometer/NoFences)
- Commercial reference / 商业参考: [Stardock Fences](https://www.stardock.com/products/fences/)

Please keep the upstream link and license notice if you publish your own modified version.

如果你准备公开发布本项目的修改版，请保留上游项目链接和许可证说明。

---

## What Changed | 相对上游做了哪些改动

### 1. AI Desktop Classification | AI 桌面分类
- Added AI-based classification for top-level desktop items.
- 支持扫描桌面第一层对象并调用 AI 模型进行分类。
- Falls back to local heuristic rules when AI is unavailable.
- AI 不可用时会自动回退到本地启发式规则。

### 2. Multi-Provider AI Support | 多提供商模型支持
- Added provider switching directly in the UI.
- 支持在界面中直接切换提供商，无需手改配置文件。
- Current presets include:
  - `OpenAI`
  - `Kimi`
  - `Doubao`
  - `DeepSeek`
  - `Gemini`
  - `Qwen`
  - `Groq`
  - `Grok`
  - `OpenRouter`
  - `Mistral`
  - `Custom`
- Added recommended model dropdown per provider.
- 为每个提供商加入了推荐模型下拉，同时保留手动输入模型名。

### 3. Physical Desktop Archiving | 物理桌面归档
- Desktop items are physically moved into managed category folders.
- 不再只是把桌面对象复制显示到栅栏中，而是会进行真实的物理移动归档。
- Supports moving items back from fences to the desktop.
- 支持从栅栏中移回桌面。

### 4. Drag And Desktop-Like Interaction | 拖拽与桌面式交互
- Drag from desktop into fences.
- 支持从桌面拖入栅栏。
- Drag between fences.
- 支持栅栏之间拖动改分类。
- Drag back to desktop.
- 支持从栅栏拖回桌面。
- Added practical item actions such as open, open location, delete, properties.
- 增加打开、打开所在位置、删除、属性等常用操作。

### 5. UI / Workflow Improvements | 界面与交互增强
- Added fence collapse/expand.
- 增加栅栏折叠/展开。
- Added lock mode to prevent moving/resizing.
- 增加锁定模式，锁定后不能拖动或缩放。
- Added menu actions for AI settings, classify now, restart, and exit.
- 增加 AI 设置、立即分类、重启、退出菜单项。

### 6. Stability Fixes | 稳定性修复
- Fixed background-thread UI update crash during AI classification.
- 修复 AI 分类线程访问 UI 集合导致的崩溃。
- Reduced duplicated shortcut issues.
- 修复重复图标和旧布局脏数据问题。
- Added cache to skip repeated classification if desktop state did not change.
- 增加缓存，桌面未变化时可跳过重复分类。

---

## Features | 当前功能

- Transparent desktop fence UI
- 透明桌面栅栏界面
- Manual fence creation / deletion / editing
- 栅栏创建、删除、编辑
- AI-based desktop item classification
- AI 自动分类桌面项目
- Physical desktop archiving
- 桌面对象物理归档
- Multi-provider AI switching
- 多提供商 AI 切换
- Recommended model dropdowns
- 推荐模型下拉
- Drag and drop between desktop and fences
- 桌面与栅栏之间拖拽
- Common desktop-style context actions
- 常用右键操作
- Restart / exit menu actions
- 重启 / 退出菜单动作

---

## Screenshots | 截图

You can add your own screenshots here after pushing to GitHub.

你可以在发布到 GitHub 后把当前程序截图补充到这里。

Suggested image slots:

- Transparent fence layout
- AI settings panel
- AI classification result
- Drag desktop item into fence

---

## Getting Started | 快速开始

### Requirements | 环境要求

- Windows 10 / 11
- .NET 6 SDK

### Build And Run | 编译与运行

```powershell
cd D:\pure\Palisades
dotnet build .\Palisades.sln
dotnet run --project .\Palisades.Application\Palisades.Application.csproj
```

### AI Setup | AI 配置

1. Open the fence menu.
2. Click `AI 设置`.
3. Select a provider.
4. Pick a recommended model or enter one manually.
5. Fill in the API key.
6. Save and run `立即AI分类`.

1. 打开栅栏菜单。
2. 点击 `AI 设置`。
3. 选择提供商。
4. 选择推荐模型或手动输入模型名。
5. 填写 API Key。
6. 保存后点击 `立即AI分类`。

---

## Configuration | 配置文件

Default AI settings file:

默认 AI 配置文件位置：

```text
%LOCALAPPDATA%\Palisades\ai-settings.json
```

It stores / 其中保存：

- current provider / 当前提供商
- API endpoint / 接口地址
- selected model / 当前模型
- recommended model list / 推荐模型列表
- env key / 环境变量名
- API key / API Key

---

## Tech Stack | 技术栈

- .NET 6
- WPF
- GongSolutions.Wpf.DragDrop
- MaterialDesignThemes (partially retained)
- System.Text.Json

---

## Notes | 注意事项

- This is not the official upstream release.
- 这不是上游项目官方版本。
- Some providers may require different model IDs depending on your account access.
- 某些提供商的模型名需要根据你的账户权限调整。
- Doubao / Ark often requires your own available endpoint or model identifier.
- 豆包 / Ark 在很多情况下需要你自己的 endpoint 或可用模型标识。
- If you redistribute this project, review the upstream license carefully.
- 如果你要公开发布，请务必检查并保留上游许可证说明。

---

## License | 许可证

Please refer to the upstream project license:

请参考上游项目许可证：

- [Palisades License](https://github.com/Xstoudi/Palisades/blob/main/LICENSE)
