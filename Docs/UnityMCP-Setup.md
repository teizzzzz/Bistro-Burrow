# Unity MCP 接入指南（本机 Claude Code ⇄ Unity 编辑器）

> 目的：让 Claude Code 能直接操作你本机正在运行的 Unity 编辑器——
> 读 Console 报错、跑 EditMode 测试、进 Play Mode、管理场景与资源。
> 方案：[MCP for Unity](https://github.com/CoplayDev/unity-mcp)（开源，支持 Claude Code 自动配置）。

## 前置条件（一次性）

| 需要 | 说明 |
|---|---|
| 本机 Claude Code | 终端运行 `npm install -g @anthropic-ai/claude-code`（或官网安装包），装好后 `claude --version` 验证 |
| Python 3.10+ | MCP 服务端运行时；Windows 从 python.org 安装时勾选 "Add to PATH" |
| Git | 克隆仓库时已装 ✔（Package Manager 拉 git 包也要用它） |
| 本仓库工程 | 已用 Unity 6 打开过一次（Library 已生成） |

## 配置步骤（约 5 分钟）

1. **Unity 里安装桥接包**
   打开本工程 → 顶部菜单 `Window → Package Manager` → 左上 `+` → **Add package from git URL**，粘贴：

   ```
   https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main
   ```

2. **自动配置 Claude Code**
   安装完成后菜单会多出 `Window → MCP for Unity` → 打开面板 → 点 **Configure All Detected Clients**。
   它会自动检测本机的 Claude Code 并写入 MCP 配置（首次会自动下载 Python 服务端依赖）。

3. **验证连接**
   在**仓库根目录**开终端：

   ```bash
   claude          # 启动 Claude Code
   /mcp            # 查看 MCP 状态，unity 应显示 connected
   ```

   然后直接对它说：「读一下 Unity Console 的报错」「跑一下 EditMode 测试」试试。

## 日常用法建议

- Unity 编辑器**保持打开**时 MCP 才可用（桥接的是运行中的编辑器）；
- 改了 C# 后让 Claude「等编译完成后读 Console」，它能自己发现并修编译错误；
- 本项目的测试入口：Test Runner → EditMode（`FormulaAndConfigTests`，与 `webdemo/test.mjs` 是同一组数值契约）。

## 常见问题

- **面板里没检测到 Claude Code**：确认终端能直接运行 `claude`（PATH 问题最常见），重开 Unity 再试；
- **防火墙弹窗**：放行（桥接走本地端口，不出外网）；
- 更多排错见官方 Wiki：<https://github.com/CoplayDev/unity-mcp/wiki/3.-Common-Setup-Problems>。

> 注：云端（Claude Code on the Web）会话连不到你本机编辑器，Unity MCP 只在**本机会话**里生效。
