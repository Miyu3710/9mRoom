# 9mRoom 发布与更新通道

三端共用同一个仓库，但**独立发布、独立检查更新**。GitHub 页面上的 `Latest` 徽标只代表某一个 Release，客户端不依赖它。

## 下载中心

面向用户的入口是 GitHub Pages 下载中心：

<https://miyu3710.github.io/9mRoom/releases/>

页面只展示 Windows / Server / Android 三个入口，点击后进入该端的版本历史、更新说明与下载资产。

- 页面：`docs/releases/`
- 版本数据：`docs/releases/releases.json`
- 发布后重新生成数据：

  ```powershell
  powershell -ExecutionPolicy Bypass -File packaging\generate-release-index.ps1
  ```

  `releases.json` 只包含已发布的 Release，draft 不会出现在页面中。

## 通道与 Tag

| 端 | Tag 格式 | 更新清单 | 资产 |
| --- | --- | --- | --- |
| Windows | `windows-vX.Y.Z` | `update-windows.json` | MSI、便携 zip、SHA256 |
| Android | `android-vX.Y.Z` | `update-android.json` | APK、SHA256 |
| 服务端 | `server-vX.Y.Z` | `update-server.json` | Linux tar.gz、SHA256 |

清单放在仓库根目录，客户端读取 `main` 分支上的原始文件：

```text
https://raw.githubusercontent.com/Miyu3710/9mRoom/main/update-windows.json
https://raw.githubusercontent.com/Miyu3710/9mRoom/main/update-android.json
https://raw.githubusercontent.com/Miyu3710/9mRoom/main/update-server.json
```

旧客户端使用的 `update.json` 保留为兼容清单，只指向当前 Windows / Linux 资产。

## 发布流程

1. 更新对应端的版本号。
2. 构建该端资产。
3. 打 tag 并推送：

   ```bash
   git tag -a windows-v1.2.0 -m "9mRoom Windows 1.2.0"
   git push origin windows-v1.2.0
   ```

4. 创建 Release，标题写 `9mRoom Windows 1.2.0`，只上传本端资产。
5. 更新仓库根目录对应清单，提交并推送 `main`。
6. 重新生成下载中心的版本数据并提交：

   ```powershell
   powershell -ExecutionPolicy Bypass -File packaging\generate-release-index.ps1
   ```

7. 验证：

   ```bash
   curl -fsSL https://raw.githubusercontent.com/Miyu3710/9mRoom/main/update-windows.json
   curl -fsSL https://raw.githubusercontent.com/Miyu3710/9mRoom/main/update-android.json
   curl -fsSL https://raw.githubusercontent.com/Miyu3710/9mRoom/main/update-server.json
   ```

## 清单示例

### Windows

```json
{
  "version": "1.1.0",
  "tag": "windows-v1.1.0",
  "release": "https://github.com/Miyu3710/9mRoom/releases/tag/windows-v1.1.0",
  "windows": {
    "asset": "9mRoom-Setup-1.1.0.msi",
    "sha256": "..."
  }
}
```

### Android

```json
{
  "version": "1.1.1",
  "tag": "android-v1.1.1",
  "release": "https://github.com/Miyu3710/9mRoom/releases/tag/android-v1.1.1",
  "android": {
    "asset": "9mRoom-Android-1.1.1.apk",
    "sha256": "..."
  }
}
```

### 服务端

```json
{
  "version": "1.1.0",
  "tag": "server-v1.1.0",
  "release": "https://github.com/Miyu3710/9mRoom/releases/tag/server-v1.1.0",
  "linux": {
    "asset": "9mRoom-Server-1.1.0-linux-x64.tar.gz",
    "sha256": "...",
    "entry": "run-server.sh"
  }
}
```

## 客户端行为

- Windows：优先读取 `update-windows.json`；失败时回退到 Release Atom，并只接受 `windows-v*`。
- Android：优先读取 `update-android.json`；失败时回退到 Atom，并只接受 `android-v*`。
- 服务端：`update-server.sh` 优先读取 `update-server.json`；失败时只接受 `server-v*`。

因此三端版本号可以不同，不需要让 GitHub 的全局 `Latest` 同时代表三端。

## 历史发布

早期合并包（`v1.0.0`、`android-v1.1.0`）的 tag 仍然保留，用于源码历史；在线 Release 列表只保留当前三端通道。历史 Release 如需重新提供下载，可由维护者恢复。
