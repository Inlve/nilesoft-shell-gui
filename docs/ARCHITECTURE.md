# Shell Studio 设计说明

## 技术架构

界面层使用 WinUI 3 与 Windows App SDK 稳定通道，配置解析、文件事务和 Shell
发现逻辑位于独立的 `ShellManager.Core`。UI 工程只负责状态呈现与用户交互，测试
工程直接引用 Core，因此 CI 无需启动桌面窗口即可验证高风险配置逻辑。

应用使用未打包、自包含的 x64 发布模型。最终发布物携带 .NET 和 Windows App SDK
运行组件，不要求用户安装 MSIX、.NET Desktop Runtime 或 Windows App Runtime。

## 产品结构

Shell Studio 采用桌面管理工具常见的“固定导航 + 工作区 + 全局操作”布局：

1. **概览**：检测 Shell 安装、版本、主配置路径、导入文件数、结构节点数和语法状态。
2. **菜单管理**：把 `menu`、`item`、`separator`、`modify`、`remove`、`settings`、`theme` 和 `import` 显示为结构树；常用菜单项通过表单创建。
3. **外观主题**：把 `theme` 的风格、视图密度、圆角和阴影映射为可视化控件。
4. **配置源码**：为 NSS 的表达式、变量、函数、图标和复杂筛选提供无损兜底编辑器。
5. **备份记录**：展示每次保存前创建的文件级备份。

## 与 NSS 的映射

| GUI 字段 | 生成的 NSS |
| --- | --- |
| 显示名称 | `title='…'` |
| 执行程序或命令 | `cmd='…'` |
| 参数 | `args='…'` |
| 显示位置 | `type='file\|dir\|back\|*'` |
| 管理员身份 | `admin` |
| 主题风格 | `theme.name` |
| 菜单密度 | `theme.view = view.*` |
| 圆角 | `theme.item.radius` |
| 阴影 | `theme.shadow.enabled` |

生成的新菜单项放在 `imports/shell-studio.nss`，主配置仅追加一次：

```nss
import 'imports/shell-studio.nss'
```

主题设置会优先写入已经存在的 `theme {}` 块，并用成对标记限定 Shell Studio 管理的内容，避免创建多个主题块。

## 保存事务

```text
编辑内容 → 内部结构检查 → 备份原文件 → 写临时文件 → 原子替换 → 可选重启 Explorer
```

检查器理解单引号、双引号、转义符、行注释和块注释；它只承担可靠的结构检查，不假装完整执行 Shell 的动态表达式。Shell 运行期错误仍以安装目录中的 `shell.log` 为最终依据。

## 应用配置

官方文档给出两种刷新方式：按住 Ctrl 触发右键菜单，或重启 Windows Explorer。桌面 GUI 使用有文档支持的命令行参数组合：

```text
shell.exe -restart -silent
```

程序在执行前明确确认，因为重启 Explorer 会暂时关闭并重新打开任务栏和文件管理器窗口。

## 安全边界

- 不解释或重新格式化用户原有 NSS 源码。
- 不自动删除、禁用或移动现有菜单项。
- 不在用户确认前重启 Explorer。
- Program Files 安装使用管理员权限，避免保存进行到一半才失败。
- 备份存放在 `%LOCALAPPDATA%\Shell Studio\Backups`，与安装目录分离。

## 后续迭代

- 完整的 `modify/remove` 规则构建器。
- 菜单树拖放排序与嵌套子菜单向导。
- `image` 图标浏览器和 SVG/glyph 预览。
- 读取 `shell.log` 并将错误定位回源文件行号。
- 备份对比、选择性恢复和配置包导入导出。
