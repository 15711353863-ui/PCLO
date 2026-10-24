# PCLO 许可范围

PCLO 的完整代码公开，但整个仓库不是单一 MIT 许可项目。

## PCLO 独立新增内容：MIT

以下从本项目新增、独立编写的文件使用 `LICENSE-PCLO-MIT`：

- `BUILD-PREVIEW.ps1`
- `PACK-PREVIEW.ps1`
- `BUILD-ICON.ps1`
- `assets/` 内的 PCLO SVG、PNG 与 ICO 图标
- `Plain Craft Launcher 2/Images/pclo.ico` 与 `Plain Craft Launcher 2/Images/Heads/PCLO.png`（上述图标的应用副本）
- `PREVIEW-README.md`、`VALIDATION.md`、`LICENSING.md`

## PCL 上游内容：保留上游许可

从 PCL 复制或修改的源文件、资源、历史记录及原有文档继续保留上游 `LICENCE`，
不会因为本项目附带 MIT 文件而改为 MIT。修改后的 XAML、VB 文件亦不被整体重新授权为 MIT。
README 中保留的上游文本继续适用其原有许可。

上游：<https://github.com/Meloong-Git/PCL>

本项目保留原作者首位署名和赞助链接、第三方身份说明、蓝色主题及上游现有的功能限制。
完整分发许可链接在 `LICENCE` 中；此文件的存在不扩大上游授予的权利。

## 依赖与第三方资源

`MeloongCore/` 是固定提交的上游子模块，适用其 `LICENSE.txt`（Apache-2.0）。
其余 NuGet 包、预编译库、图片和 Java 工具等继续适用各自上游许可，PCLO 不对它们重新授权。

服务凭据、用户数据、账号令牌、运行日志和本地构建缓存不属于发布源码内容。
