# 架构决策记录（ADR）

## 什么时候写

- 选技术栈、选传输方式、定数据模型边界
- 改变目录结构或部署方式
- 推翻之前的一条决策

小改动、纯实现细节不写。ADR 记录的是「为什么这样选」，不是「怎么用」。

## 怎么写

文件名：`NNNN-短标题.md`（四位递增序号，例如 `0001-scope-and-direction.md`）。

```markdown
# NNNN 标题

- 日期：YYYY-MM-DD
- 状态：提议 / 已接受 / 已废弃（被 NNNN 取代）

## 背景

## 方案对比

## 决定

## 后果

好的方面、坏的方面、以后什么情况下应该重新考虑。
```

## 当前

| 序号 | 标题 | 状态 |
| --- | --- | --- |
| 0001 | [产品范围与界面路线](0001-scope-and-direction.md) | 已接受 |
| 0002 | [服务端技术栈与首版通信方式](0002-server-stack.md) | 已接受 |
| 0003 | [Windows 客户端结构：Core + CLI 探针 + 后续 WPF](0003-windows-client-structure.md) | 已接受 |
| 0004 | [视觉唯一来源：tokens.json 生成 WPF 资源](0004-design-tokens.md) | 已接受 |

尚未决定的技术选型见 `docs/development-approach.md` 第 15 节。
