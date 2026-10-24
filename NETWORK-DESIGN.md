# Plain Craft Launcher Open 0.3.1 下载说明

0.3.1：CurseForge 默认通过 MCIM 公益镜像请求元数据，无需配置 API Key。
Modrinth 仍为社区资源默认平台。自行配置官方 Key 并关闭镜像优先时，继续使用官方/镜像竞速。
微软公共 Live Client ID 与自有现代 GUID Client ID 分别使用对应的设备授权、轮询及刷新 scope。

本轮修改只保存在本地，尚未推送或发布。

## 数据来源

- Minecraft：实时获取 `https://piston-meta.mojang.com/mc/game/version_manifest_v2.json`；根据返回的版本 URL、大小和 SHA1 下载版本描述、客户端、资源索引、资源和依赖。
- Forge：Forge 官方目录和 Maven；NeoForge：NeoForge 官方版本 API 和 Maven；Fabric：Fabric Meta API 和 Maven；保留现有 LiteLoader、OptiFine 的上游解析。
- 国内公益镜像：BMCLAPI，社区模组资源使用 MCIM。镜像目录保持对应官方的相对路径；不经过原作者下载/配置服务。
- 不再合并原版缓存中的“特供”版本列表。

## 竞速与失败切换

版本列表的官方解析任务与镜像解析任务同时启动，首个成功解析的结果胜出。动态 JSON 请求使用完整 JSON 解析来排除错误网页。

文件对候选源同时发出真实 GET，读取首批正文后选择先返回有效数据的源。竞争数最多为 4，通常为官方与镜像两个；取消并释放其他连接。
后续分段优先使用胜出源，保留原有断点续传和失败重试。响应错误、长度不符、传输中断或最终校验失败都会触发重试/备用源。
“竞速”表示本次先返回有效数据，不承诺任何时刻该源都有最高吞吐量。

CurseForge 凭据仅发送到 `api.curseforge.com`，不附在跨域镜像或 CDN 请求上。

## 文件校验

- 每个下载文件都检查长度（有提供时），完成后计算 SHA256。
- 元数据提供的 MD5 / SHA1 / SHA256 / SHA512 都会核对。Mojang 不可变 URL 中的摘要和版本/资源清单摘要会传递到下载任务。
- Maven 依赖若未提供摘要，尝试从官方 Maven 获取 `.sha256` / `.sha1` 文件并缓存结果。
- 受管理的游戏/加载器安装产物若缺少可验证的摘要，停止下载并说明原因。
- JSON 还会实际解析。没有公布摘要的封面图检查格式特征、传输长度，WebP 还检查 RIFF 长度；这些检查不能等同于发布方哈希认证。
- 重新使用已有文件前也会校验。校验失败的下载不会标成完成；错误文件被删除，并走备用源/重试。

顶层动态版本目录、部分动态接口和封面图没有发布方摘要，因此使用 HTTPS、长度与格式检查；生成的本地 SHA256 用于记录，不冒充远端发布方摘要。

## 与作者服务器的关系

移除作者 Referer、配置轮询、遥测、作者新闻主页和 EasyTier 作者中转目录。
两个 HTTP 入口及文件下载入口均使用主机检查，跟随重定向时逐跳检查，阻止 `meloong.com` 及其子域、原 PCL 服务域名。
原作者署名与赞助链接保留在关于页，仅在用户主动点击后交给外部浏览器打开；启动器后台不访问它们。
Open 版的单实例锁和默认下载缓存与原版分开，可同时运行。

## 验证

`tests/NetworkHarness/` 在 .NET Framework 4.8 目标下编译，并在本机 CLR 执行。
覆盖竞速正文无损、失败源、错误长度、无效 JSON、取消、作者域名和重定向阻止、摘要校验、损坏文件拒绝、镜像与官方路径、安装产物严格校验及封面图例外。
`--live` 另外请求 Mojang 清单和 1.21.1 版本 JSON、核对 SHA1，读取实时 Fabric 列表、获取官方摘要并实际下载加载器 JAR。

```powershell
D:\Other\Codex\.tools\dotnet\dotnet.exe build tests\NetworkHarness\NetworkHarness.csproj -c Release
.\tests\NetworkHarness\bin\Release\net48\NetworkHarness.exe --live
```

尚未验收完整游戏安装/启动、微软账号登录、全部历史版本加载器、所有镜像故障组合和联机。
