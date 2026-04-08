# LiteMark

LiteMark 是一个轻量的 Windows 屏幕标记工具。

它常驻系统托盘，按住快捷键即可直接在屏幕上做临时标记，松开后自动淡出，适合演示、录屏、远程沟通和日常说明场景。

当前仓库提供的是 Windows 版本。

## 功能特性

- 托盘常驻，左键单击切换启用 / 暂停
- 暂停时托盘图标自动变灰
- 支持开机启动
- 支持自定义快捷键
- 按住 `Alt+W` 可连续绘制矩形标记
- 按住 `Alt+E` 可连续绘制水平横线
- 松开快捷键后标记会短暂淡出
- 绘制时不应选中底层网页或文本内容

## 下载

可直接在 GitHub Release 页面下载便携版：

- [Release 页面](https://github.com/mixiaojiediy/LiteMark/releases)

当前发布资产：

- `LiteMark.exe`

这是免安装的单文件 Windows 可执行程序，下载后可直接运行。

## 使用方式

1. 运行 `LiteMark.exe`
2. 程序会出现在系统托盘
3. 左键托盘图标：
   切换启用 / 暂停
4. 右键托盘图标：
   打开设置、切换开机启动、退出程序
5. 默认快捷键：
   `Alt+W` 绘制矩形
6. 默认快捷键：
   `Alt+E` 绘制水平横线

## 设置项

当前支持以下设置：

- 矩形快捷键
- 横线快捷键
- 线条颜色
- 线宽

## 技术栈

- C#
- .NET 8
- WinForms
- 全局键盘 / 鼠标 Hook

## 本地开发

环境要求：

- Windows
- .NET 8 SDK

构建：

```powershell
dotnet build -c Release
```

发布单文件便携版：

```powershell
.\publish.ps1
```

默认输出目录：

- `publish/LiteMark.exe`

## 当前范围

- 目前仅支持 Windows
- 当前定位是轻量、便携、快速启动的屏幕临时标记工具
- macOS 版本后续可在同一仓库继续扩展

## 说明

由于程序涉及托盘常驻、全局快捷键、键鼠监听和屏幕覆盖绘制，部分安全软件或系统策略可能会额外提示，这类行为本身属于此类工具的正常实现方式。

## License

本项目当前使用 MIT License。
