# 安装包报毒 / SmartScreen 说明

## 结论先说

**没有代码签名证书时，无法保证安装包不被杀毒软件或 SmartScreen 拦截。**  
当前仓库没有可用证书，所以第一个版本是**未签名 MSI**。这不是靠换压缩参数或改文件名能彻底解决的问题。

当前发布形式：只发布 MSI，不再生成 `setup.exe`。

已做的降低误报措施：

- 使用 Windows Installer（MSI）作为安装载体，不自解压、不加壳
- 不使用 UPX、混淆、静默下载或开机自启
- 应用和 MSI 都带完整版本信息、公司名和图标
- 安装向导为简体中文
- 提供 SHA256 校验文件
- 本机 Windows Defender 对当前 MSI 扫描结果：无威胁

仍会出现的情况：

- 双击未签名 MSI 时 SmartScreen 显示“未知发布者”
- 部分杀毒软件对新的、低信誉的未签名安装包做启发式拦截
- 用户选择“仍要运行”后通常可以正常安装

## 根本解决：代码签名

需要一张 Windows Authenticode 代码签名证书：

- OV 证书：便宜一些，但 SmartScreen 信誉需要时间积累
- EV 证书：硬件令牌/云签名，SmartScreen 信誉更好
- 证书必须包含 Code Signing EKU，并且私钥可用

拿到证书后：

```powershell
$env:NINEMROOM_SIGN_PFX_PASSWORD = '...'
powershell -ExecutionPolicy Bypass -File packaging\package.ps1 -Version 1.0.0 `
  -Sign -PfxPath C:\secrets\9mroom.pfx
```

或使用本机证书指纹：

```powershell
powershell -ExecutionPolicy Bypass -File packaging\package.ps1 -Version 1.0.0 `
  -Sign -Thumbprint <thumbprint>
```

签名后再分发 `dist/9mRoom-Setup-1.0.0.msi`，不要分发旧的未签名文件。

## 如果仍被某个杀软拦截

1. 记录杀软名称、版本、拦截文件名和 SHA256
2. 用 `SHA256SUMS.txt` 核对文件未被篡改
3. 向该厂商提交误报（False Positive）样本
4. 不要把文件重新压缩、改名或加壳后再次提交，这会加重启发式评分

## 分发建议

- 优先分发 `9mRoom-Setup-1.0.0.msi`
- 同时提供 SHA256，方便用户校验
- 便携版 `9mRoom-1.0.0-win-x64.zip` 只适合不想安装的用户

## 卸载报错 1926 / D:\Config.Msi

如果 MSI 安装在 **D 盘自定义目录**，卸载时可能连续弹出：

```text
错误 1926：无法设置文件 D:\Config.Msi\xxxx.rbf 的文件安全
错误 5：请确认你有足够的权限修改此文件的安全权限
```

这是 Windows Installer 的 **D:\Config.Msi 权限损坏**，不是 9mRoom 文件本身的问题。MSI 仍会返回成功并完成卸载，但会弹出大量非致命错误。

处理方式（任选其一）：

1. **安装到默认的 C 盘用户目录**，卸载时不会使用 D:\Config.Msi；这是最稳妥的方式。
2. 以管理员身份修复 D 盘安装器缓存：

```powershell
takeown /f D:\Config.Msi /r /d y
icacls D:\Config.Msi /reset /t /c
```

如果修复仍失败，可在管理员权限下删除 `D:\Config.Msi`，Windows Installer 会重新创建。

## 当前测试限制

构建机当前的 Windows Installer 服务在安装测试中被卡住，需要重启一次才能继续做完整点击安装验证。
MSI 本身已完成构建，表已验证：

- `ProductLanguage = 2052`
- 欢迎页文案为中文
- 包含 `InstallDirDlg` 安装路径页
- 开始菜单和桌面快捷方式均已写入 Shortcut 表
