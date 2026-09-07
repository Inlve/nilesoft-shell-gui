# Shell Studio

Shell Studio 是面向 [Nilesoft Shell](https://nilesoft.org/) 的原生 Windows GUI 管理器。它读取现有 `shell.nss` 和递归导入的 `.nss` 文件，提供配置概览、结构导航、常用菜单项向导、主题预设、源码编辑、语法检查与自动备份。

## 设计原则

- **不破坏现有配置**：现有文件保持原始文本；向导新增内容写入独立的 `imports/shell-studio.nss`。
- **保留 Shell 能力**：复杂表达式、变量、图标、`modify/remove` 等高级语法可在源码编辑器中完整编辑。
- **保存可恢复**：写入前检查括号、引号与注释结构，并在 `%LOCALAPPDATA%\Shell Studio\Backups` 创建备份。
- **正确应用配置**：调用 Shell 官方命令行参数 `shell.exe -restart -silent`，重启 Explorer 后加载新配置。
- **原生现代界面**：使用 WinUI 3、NavigationView、Mica 和 Windows 11 Fluent 控件。
- **独立分发**：采用未打包、自包含、单文件发布；目标电脑无需预装 .NET 或 Windows App Runtime。
- **精简依赖**：仅引用 Windows App SDK 的 WinUI 组件，不引入 Web 框架或 Electron。

## 技术栈

- .NET 9
- WinUI 3 / Microsoft Windows App SDK 2.4 stable
- Unpackaged + self-contained
- Windows 10 19041 或更高版本，x64

## 开发与运行

```powershell
dotnet restore ShellStudio.sln
dotnet build ShellStudio.sln -p:Platform=x64
dotnet run --project src/ShellManager.App -p:Platform=x64
```

程序需要修改 `C:\Program Files\Nilesoft Shell` 时会请求管理员权限。

## 验证

```powershell
dotnet run --project tests/ShellManager.Tests
```

## 发布独立 EXE

```powershell
dotnet publish src/ShellManager.App -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:NuGetLockFilePath=obj/packages.publish.lock.json -p:RestoreLockedMode=false -o artifacts/win-x64
```

输出为 `artifacts/win-x64/ShellStudio.exe`。

## 参与社区

- 阅读 [CONTRIBUTING.md](CONTRIBUTING.md) 了解开发、测试和 Conventional Commits 规则。
- Bug、功能建议和 NSS 兼容问题使用对应的结构化 Issue 模板。
- 安全问题按照 [SECURITY.md](SECURITY.md) 通过 GitHub 私密安全公告报告。
- 行为规范和治理方式分别见 [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) 与 [GOVERNANCE.md](GOVERNANCE.md)。

## 版本与发布

项目遵循 Semantic Versioning。唯一版本源是 `Directory.Build.props` 中的
`VersionPrefix`。推送与该版本一致的签名注解标签（例如 `v0.2.0`）后，GitHub
Actions 会重新构建、测试并发布自包含 EXE、SHA-256 校验文件和构建来源证明。

详细流程见 [docs/RELEASING.md](docs/RELEASING.md)，历史变更见
[CHANGELOG.md](CHANGELOG.md)。

## 当前范围

这是首个可运行版本。结构化向导目前覆盖常用的 `item` 与 `theme` 场景；所有官方 NSS 能力都可通过源码编辑器使用。后续可在不改变存储格式的前提下，继续加入完整的 `modify/remove` 规则构建器、拖放排序和上下文菜单沙盒预览。
