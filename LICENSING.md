# PCLO 许可范围

PCLO 的完整代码公开，但整个仓库不是单一 MIT 许可项目。

## PCLO 独立新增内容：MIT

以下从本项目新增、独立编写的文件使用 `LICENSE-PCLO-MIT`：

- `BUILD-PREVIEW.ps1`
- `PACK-PREVIEW.ps1`
- `BUILD-ICON.ps1`
- `PCLCS/Network/OpenNetwork.cs`、`tests/NetworkHarness/` 与 `NETWORK-DESIGN.md`
- `PCLCS/Open/`、`tests/OpenHarness/`、`tests/NativeUiHarness/`、`OPEN-FEATURES.md`
- `Plain Craft Launcher 2/Modules/OpenAppearance.vb`、`Plain Craft Launcher 2/Pages/PageOpen.vb`、`Plain Craft Launcher 2/Pages/OpenAppearanceSettings.vb`、`Plain Craft Launcher 2/Modules/OpenLoaderInstall.vb`、`Plain Craft Launcher 2/Pages/PageDownload/OpenLoaderSelection.vb` 的独立实现；其调用的原版控件和其余改动文件继续适用上游许可
- `PREVIEW-README.md`、`VALIDATION.md`、`LICENSING.md`

## PCL 上游内容：保留上游许可

从 PCL 复制或修改的源文件、资源、历史记录及原有文档继续保留上游 `LICENCE`，
不会因为本项目附带 MIT 文件而改为 MIT。修改后的 XAML、VB 文件亦不被整体重新授权为 MIT。
README 中保留的上游文本继续适用其原有许可。

0.1.3 起使用的蓝色六边形 PCLO 图标由上游 PCL 图标修改而来。`assets/pclo-source.png`、
对应各尺寸 PNG、ICO，以及 `Images/pclo.ico`、`Images/Heads/PCLO.png`、`Images/Heads/OpenSplash.png` 应用副本均保留上游资源的权利与许可，
不列入独立新增内容的 MIT 范围。原始图标仍保留在 `Images/icon.ico` 与 `Images/Heads/Logo.png`。

上游：<https://github.com/Meloong-Git/PCL>

本项目在源码、README 和许可文件中保留上游署名及第三方身份说明，并保留上游代码和资源的许可限制。关于页首位提供原作者署名和赞助链接；启动时按上游指南提供正版购买与赞助提示。不展示投票入口。
完整分发许可链接在 `LICENCE` 中；此文件的存在不扩大上游授予的权利。

## 依赖与第三方资源

英文文本表 `Plain Craft Launcher 2/Resources/Open/en-US.json` 从 PCL-CE 语言表提取和修改，
适用 Apache-2.0，不归入 MIT 独立文件范围。来源、修改说明与许可证见 `THIRD-PARTY-NOTICES.md` 和 `licenses/`。

`MeloongCore/` 是固定提交的上游子模块，适用其 `LICENSE.txt`（Apache-2.0）。
其余 NuGet 包、预编译库、图片和 Java 工具等继续适用各自上游许可，PCLO 不对它们重新授权。

服务凭据、用户数据、账号令牌、运行日志和本地构建缓存不属于发布源码内容。
