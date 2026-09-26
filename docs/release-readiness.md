# ShotCab 源码首发审查（2026-09-26）

当前结论：**经筛选的源码已公开；1.0.0 按用户安装包已发布并通过本机验证，跨机器验收与签名仍待完成。**
本页记录首次源码上传的本地审查，不代表完整法律意见或最终兼容性验收。

## 已具备

- 主程序为 WinForms / .NET Framework 4.8 x64；`scripts/build.ps1` 在项目内的 `.cache` 使用 NuGet、临时和 SDK 缓存。
- `scripts/package.ps1` 可生成便携 ZIP，包含程序、README、上游 GPL 文本、上游第三方许可证和项目文档；脚本拒绝把历史数据库及运行时数据装入包。
- `scripts/package-source.ps1` 可生成对应源码 ZIP，包含应用、测试、构建脚本、ShareX 固定版本源码与版权声明；设计参考、构建输出和缓存不在其中。
- `docs/UPSTREAM.md` 固定 ShareX 17.1.0 的提交与来源；可选离线 OCR 的来源单列于 `docs/OCR-NOTICE.md`。

## 首次源码上传审查结果

1. **Git 与文件范围。** 暂存清单为 1,814 个文件；新生成的源码 ZIP 也是 1,814 项，路径集合与暂存清单完全一致。`.cache`、`artifacts`、用户历史、个人设置、`.workbuddy` 与 UI 参考资料未进入仓库。未暂存 `.pfx`、可执行文件、DLL、OCR 模型、数据库或 ZIP。对暂存文本的常见令牌、私钥和硬编码密码模式扫描没有命中；未来每次提交仍须复查。
2. **许可与来源。** 根目录 [LICENSE](../LICENSE) 为 GPLv3 正文，[NOTICE.md](../NOTICE.md) 明确 ShotCab 自有贡献采用 GPL-3.0-or-later、ShareX 原有部分保留上游 GPLv3 条款，组合程序以 GPLv3 分发。只上传实际链接的四个 ShareX 库与上游声明。上游未使用的上传器模块带有嵌入式证书，已整体排除。
3. **构建与测试。** 本机 Release 构建 0 警告、0 错误；核心历史测试、编辑器测试和窗口烟测通过。“关于”窗口已渲染检查，可点击 ShotCab 和 ShareX 地址。自有源码暂存差异空白检查通过；上游资源文件已有的尾随空格没有为清理格式而改写。
4. **二进制后续要求。** 每个预览 ZIP 都需有同版本的可验证源码、构建脚本、上游来源、许可声明和 SHA-256。当前便携包仅在本机完成打包检查，没有随首次源码上传公开发布。`assets/icons` 设计参考未使用并已排除；OCR 增强包独立分发前仍需逐项核对模型、运行时、字典和许可证。
5. **环境验收仍未完成。** 当前烟测覆盖本机主要窗口、历史、编辑与剪贴板数据格式；Windows 10、多屏混合 DPI、HDR、微信/QQ/Word 实际粘贴、拖放及升级/迁移仍需实机验收。目录迁移测试曾遇一次 Windows“访问被拒绝”，立即重跑通过；需要在干净目录和其他机器复核。演示素材只能使用可公开的非私人数据。

## 安装包进度

按用户安装包在 F 盘构建，OCR 为安装时单独勾选的组件，默认不勾选。[v1.0.0 发行页](https://github.com/yee-tree/ShotCab/releases/tag/v1.0.0) 提供安装包、对应源码包和校验值；本版核对范围见 [`release-1.0.0.md`](release-1.0.0.md)。先前 [v0.1.0 开发预览版](https://github.com/yee-tree/ShotCab/releases/tag/v0.1.0) 的安装、OCR、升级和卸载测试见 [`installer-release.md`](installer-release.md)。安装版将新用户数据放在 LocalAppData，便携版仍用程序旁的数据目录；完整后续验收清单见 [`installer-plan.md`](installer-plan.md)。

便携 ZIP 与对应源码包继续保留，供免安装使用和版本核对；安装包不能替代源码交付。

参考：
- GNU GPL 常见问题：https://www.gnu.org/licenses/gpl-faq.html
- .NET Framework 部署指南：https://learn.microsoft.com/en-us/dotnet/framework/deployment/deployment-guide-for-developers
- WinForms 的 MSIX 打包：https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/dotnet/package-app
- GitHub 仓库许可说明：https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository
