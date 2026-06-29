<p align="right">
  <a href="README.md">
    <img src="https://img.shields.io/badge/English-Read-blue?style=for-the-badge" alt="English" />
  </a>
</p>

# Spelunx Web Controller Toolkit

一个 Unity 工具包，让手机浏览器成为游戏控制器，实现「主机大屏 + 多人手机遥控」的本地联机体验。由卡内基梅隆大学 Entertainment Technology Center（ETC）开发，采用 MIT 许可证。

## 概述

本项目解决的是派对游戏、展览互动、教学演示等场景中的常见需求：**Unity 游戏运行在主机（PC / 大屏）上，玩家用自己的手机通过网页加入房间并操控游戏**，无需安装 App。

整体采用三层架构：

```
┌─────────────────┐     WebSocket      ┌──────────────────┐     WebSocket      ┌─────────────────┐
│  Unity 主机      │ ◄────────────────► │  Node.js 中继服务  │ ◄────────────────► │  手机浏览器控制器 │
│  (HostClient)   │   role=host        │  (server.js)     │   role=client      │  (join.html 等)  │
└─────────────────┘                    └──────────────────┘                    └─────────────────┘
```

1. **Unity 主机**：运行游戏逻辑，通过 `HostClient` 连接中继服务，创建房间并接收玩家输入。
2. **Node.js 中继服务**：基于 Express + WebSocket，负责房间管理、消息转发，并托管手机端控制器网页（默认端口 `3010`）。
3. **手机浏览器**：玩家扫码或输入房间码加入，进入等待队列；主机开始游戏后，按加入顺序分配控制器槽位。

通信协议使用**管道符分隔的纯文本消息**（如 `slider|id|slot|73.5`），而非 JSON，以降低延迟和解析开销。

## 控制器槽位

最多支持 4 名玩家，按加入顺序自动分配槽位，每名玩家对应不同的手机 UI：

| 槽位 | 控制器类型 | 输入内容 |
|------|-----------|---------|
| P1 | 滑块 (Slider) | 0–100 的连续值 |
| P2 | 信使 (Messenger) | 文本消息（自动转发给 P4 显示） |
| P3 | 动作按钮 (Action) | 单次按压 / 释放 |
| P4 | 显示屏 (Display) | 只读，展示 P2 发送的文本 |

此外还保留了一套**传统方向键 + 跳跃**的 D-pad 输入接口，供自定义场景使用。

## 典型流程

1. 在 Unity 编辑器中进入 Play 模式，`NodeAutoRunner` 会自动启动 Node 中继服务；打包后的构建则由 `NodeRuntimeStarter` 负责启动。
2. `HostClient` 连接中继服务，获得 4 位房间码（如 `AB3K`）。
3. `LanAddressDisplay` 在屏幕上显示局域网地址和二维码，玩家用手机扫码访问 `join.html`。
4. 玩家输入昵称加入，进入等待页面；主机可在 Unity 或网页端查看排队人数。
5. 主机调用 `HostClient.AssignAndStart()` 分配槽位并开始游戏。
6. 手机端的输入经中继服务实时转发到 Unity，游戏逻辑通过继承 `PlayerInputRouter` 来响应。

## 项目结构

```
spelunx-web-controller-toolkit/
└── Spelunx Web Multiplayer Toolkit/     # Unity 6 项目
    ├── Assets/
    │   ├── Scripts/
    │   │   ├── Web/
    │   │   │   ├── HostClient.cs          # WebSocket 客户端，房间与输入状态管理
    │   │   │   ├── PlayerInputRouter.cs   # 输入路由基类（继承并实现游戏逻辑）
    │   │   │   ├── PlayerListUI.cs        # 玩家列表 UI
    │   │   │   ├── LanAddressDisplay.cs   # 局域网地址与二维码显示
    │   │   │   └── NodeRuntimeStarter.cs  # 构建版本中自动启动 Node 服务
    │   │   └── Sample Scene/
    │   │       └── SampleGameplay.cs      # 示例：滑块控力、按钮弹射球体
    │   ├── Editor/
    │   │   └── NodeAutoRunner.cs          # 编辑器 Play 模式自动启动 Node 服务
    │   └── StreamingAssets/
    │       └── server/
    │           ├── server.js              # 中继服务主程序
    │           ├── package.json
    │           └── public/                # 手机端控制器网页
    │               ├── join.html          # 加入房间
    │               ├── waiting.html       # 等待大厅
    │               ├── controller_p1.html # P1 滑块界面
    │               ├── controller_p2.html # P2 信使界面
    │               ├── controller_p3.html # P3 按钮界面
    │               └── controller_p4.html # P4 显示界面
    └── Packages/
        └── manifest.json                  # 依赖（含 NativeWebSocket 等）
```

## 快速开始

### 环境要求

- [Unity 6](https://unity.com/)（项目版本：`6000.1.0f1`）
- [Node.js](https://nodejs.org/)（用于运行中继服务）

### 运行步骤

1. 用 Unity 打开 `Spelunx Web Multiplayer Toolkit` 目录。
2. 在 `Assets/Editor/NodeAutoRunnerConfig.asset` 中将 `server.js` 拖入 `serverJs` 字段（首次打开项目时编辑器会自动创建该配置）。
3. 确保本机已安装 Node.js 且可在终端中执行 `node` 命令。
4. 进入 Play 模式——中继服务会自动启动，控制台输出 `Relay on http://localhost:3010`。
5. 打开示例场景，屏幕上会显示局域网地址和房间码；用手机（需与主机在同一局域网）访问该地址即可加入。

### 集成到自己的游戏

1. 在场景中挂载 `HostClient` 组件。
2. 创建一个继承 `PlayerInputRouter` 的脚本，重写 `OnSliderInput`、`OnActionButton`、`OnTextMessage` 等方法处理输入。
3. 将子类组件赋值给 `HostClient.router`。
4. 在合适的时机调用 `hostClient.AssignAndStart()` 开始游戏。

可参考 `SampleGameplay.cs` 中的示例：P1 的滑块值控制力度，P3 的按钮按下时给球体施加向上的冲量。

## 远程部署

`HostClient` 提供 `isRemoted` 和 `relayHost` 字段。将 `isRemoted` 设为 `true` 并指向远程中继服务器地址时，Unity 不会尝试在本地启动 Node 服务，而是直接连接远端 WebSocket 端点。适用于将中继服务部署到云端的场景。

## 许可证

[MIT License](LICENSE) — Copyright (c) 2026 Entertainment Technology Center at Carnegie Mellon University
