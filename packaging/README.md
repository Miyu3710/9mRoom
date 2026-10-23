# 9mRoom 打包（T17）

## 产物

`packaging/package.ps1` 生成：

| 文件 | 用途 |
| --- | --- |
| `dist/9mRoom-Setup-1.0.0.msi` | Windows 安装包，发布主产物 |
| `dist/9mRoom-1.0.0-win-x64.zip` | Windows 便携版，解压后运行 `9mRoom.cmd` |
| `dist/9mRoom-Server-1.0.0-linux-x64.tar.gz` | Linux 服务端，自包含 .NET 运行时 |
| `dist/update-windows.json` | Windows 独立更新清单（SHA256、资产名） |
| `dist/update-server.json` | 服务端独立更新清单 |
| `dist/update.json` | 兼容旧客户端的合并清单 |
| `dist/SHA256SUMS.txt` | 全部产物的 SHA256 |

Android APK 与 `update-android.json` 由 Android 构建/发布流程单独产出。

发布通道、tag 规则与清单更新步骤见 [docs/releases.md](../docs/releases.md)。

**发布只使用 MSI**。不再生成 `setup.exe` 启动器。

## 安装行为

- 按用户安装，不需要管理员权限
- 中文安装向导，可选择安装路径
- 默认安装到 `%LOCALAPPDATA%\9mRoom`
- 创建开始菜单快捷方式
- 创建桌面快捷方式
- 卸载走“设置 → 应用 → 9mRoom”，或运行 MSI 的卸载

## 构建

```powershell
powershell -ExecutionPolicy Bypass -File packaging\package.ps1 -Version 1.0.0
```

首次构建需要 WiX 5 和 UI 扩展：

```powershell
dotnet tool install wix --tool-path .tools/wix5
.\.tools\wix5\wix.exe extension add WixToolset.UI.wixext/5.0.2
```

WiX UI 扩展会缓存到仓库根目录的 `.wix/`（已 gitignore）。
构建脚本会自动使用 `packaging/WixUI_zh-CN.wxl`，安装向导为简体中文。

## 签名

正式发布应签名：

```powershell
$env:NINEMROOM_SIGN_PFX_PASSWORD = '...'
powershell -ExecutionPolicy Bypass -File packaging\package.ps1 -Version 1.0.0 `
  -Sign -PfxPath C:\secrets\9mroom.pfx
```

也可以使用证书指纹：

```powershell
-Sign -Thumbprint <thumbprint>
```

签名会覆盖 MSI 和 MSI 内嵌的应用 exe（先签应用，再打 MSI）。

## 报毒 / SmartScreen

详细说明见 [docs/installer-antivirus.md](../docs/installer-antivirus.md)。

当前情况：

- 仓库没有代码签名证书，**产物未签名**
- Windows Defender 对当前 MSI 扫描未报威胁
- 未签名 MSI 仍可能触发 SmartScreen“未知发布者”；这不能靠改打包参数彻底解决
- 根本解决方式是购买代码签名证书并重新签名；EV 证书更有利于 SmartScreen 信誉

## 已知限制

- 自动更新尚未接入：Windows 通过 GitHub Releases 检查更新并手动下载；Linux 通过 `update-server.sh` 检查
- 安装向导为 Windows Installer 标准界面；中文文案由 `WixUI_zh-CN.wxl` 提供
- 首次安装前如果系统 Windows Installer 服务异常，需要重启一次再安装
