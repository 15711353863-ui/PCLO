# Plain Craft Launcher (PCL) Open 修改版（PCLO） 0.1.2

这是第三方基于 PCL 独立制作的本地预览构建，不代表 PCL 官方。
原作者：龙腾猫跃及上游贡献者。赞助原作者：https://meloong.com/afd/a/LTCat

简称 PCLO，O 代表 Open。项目公开全部衍生版源码、构建脚本和修改记录，
每个发行版附带对应源码包。上游与第三方依赖保留各自许可，服务凭据通过环境变量配置。
PCLO 独立新增文件采用 MIT，具体范围见 `LICENSING.md`；不对整个上游项目重新授权。

## 来源

- 官方仓库：https://github.com/Meloong-Git/PCL
- 基准提交：6b9a50df911c9d68b69b24c979efba477fb14b67
- MeloongCore：98157d65c7627721c816c02e8be96ac4fe61d615
- 原项目版本：2.99.0.0

## 本次修改

- 保留上游 WPF 页面、蓝色主题、布局、图片和动画。
- 窗口标题及关于页标明第三方预览身份，保留原作者首位署名和赞助链接。
- 设置注册表分支改为 PCLLocalPreview，避免与原版设置分支混用。
- 添加本地构建脚本。程序集内部名称保持原样，以兼容原版 pack 资源路径。
- 新增 PCLO 开放圆环和启动箭头图标，包含 SVG、PNG、多尺寸 ICO 与可重复生成脚本。

## 使用

双击 `PCLO.exe`。需要 Windows 与 .NET Framework 4.8。
程序会在所在目录创建 PCL 数据目录，建议保留在独立文件夹中使用。
首次运行会显示上游使用协议及开源版本说明。

## 当前功能边界

- 微软登录需要自行申请的 Client ID，可通过 PCL_MS_CLIENT_ID 环境变量配置。
- CurseForge 需要自行申请的 API Key，可通过 PCL_CURSEFORGE_API_KEY 环境变量配置。
- 保留上游开源版的主题、更新、下载等限制，不包含官方私有凭据。
- 本地预览不等于完整功能验收。真实账号登录、游戏下载、游戏启动及联机需要另行验证。

## 构建

安装 Visual Studio 2022 Build Tools、.NET SDK 9（C# 13）与 .NET Framework 4.8 目标包，
或使用 NuGet 还原得到的 referenceassemblies.net48 包。运行：

```powershell
.\BUILD-PREVIEW.ps1 -DotnetRoot 'D:\Other\Codex\.tools\dotnet'
```

## 以后公开发布

此目录的 LICENCE 包含上游使用指南及独立分发许可链接，需保留并继续遵守。
正式发布前仍需核对链接指向的完整分发许可、提供对应源码，
并配置属于衍生版的服务凭据和更新渠道。源码仓库： https://github.com/15711353863-ui/PCLO 。
