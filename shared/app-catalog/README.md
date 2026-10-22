# shared/app-catalog

把「平台原始标识」归一化成跨端一致的 `appId` 与类别。

| 平台 | 原始标识 | 示例 |
| --- | --- | --- |
| Windows | 进程名（小写） | `code.exe` → `vscode` |
| Android | 包名 | `com.tencent.tim` → `tim` |

## 为什么重要

- **隐私**：映射在客户端本地完成。服务端与其他用户只收到 `appId` 和类别，
  收不到进程名、包名，更收不到窗口标题。
- **跨端一致**：同一个人在 PC 和手机上用同一个应用时，房间里显示同一个图标与名称。
- **规避包可见性限制**：Android 11+ 查询其他应用信息受限，内置目录可覆盖常见应用，
  未收录的归为「其他」。

## 字段草案

```json
{
  "id": "vscode",
  "name": "Visual Studio Code",
  "category": "coding",
  "aliases": {
    "windows": ["code.exe"],
    "android": ["com.microsoft.vscode"]
  }
}
```

类别建议收敛为 8 个：

`coding` / `writing` / `reading` / `meeting` / `media` / `game` / `browsing` / `other`

## 维护方式

- 第一版只收 30–50 个高频应用，够用即可
- 同一 `appId` 只允许一个类别，避免两端打架
- 不收录带隐私含义的应用（医疗、银行等），统一归 `other`

## 状态

空。
