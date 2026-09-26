# ShotCab 安装包规划与注意事项

状态：首个按用户安装包已实现；此文保留后续迁移和跨机器验收要求。当前实现与本机测试见 [installer-release.md](installer-release.md)。

## 目标与交付边界

首个安装版提供一个离线安装包，安装 ShotCab 主程序，并让用户在组件页自行选择是否安装完整 OCR 组件。选装 OCR 后，点击编辑器的“识别文字”或开启“进入编辑器时自动识别文字”应直接运行本地 OCR，无需再选择 `ShotCab.Ocr.exe`。保留便携 ZIP 作为另一种交付方式，并为每个公开二进制版本提供对应源码与许可文件。

目前主程序面向 Windows x64、.NET Framework 4.8；OCR 工作进程由 `scripts/build-ocr.ps1` 以 win-x64 自包含方式发布。精简安装载荷中的 OCR 约 128 MB，安装包容量和磁盘余量检查应按每次实际构建产物计算。

## 安装目录与 OCR

- 安装目录包含 `ShotCab.exe`、运行依赖、用户说明和许可声明；只有在用户勾选可选组件时才包含 `ocr/ShotCab.Ocr.exe`、OCR 模型和运行时。模型、字典和原生 DLL 必须保持工作进程预期的相对目录；不能只复制 OCR 可执行文件。
- `OcrService` 已在 OCR 路径为空时查找 `<应用目录>/ocr/ShotCab.Ocr.exe`。安装版应使用这一默认路径；设置中的手动路径仅供便携版、开发调试或自定义组件使用。安装完成后应实际识别一张中英混排图片，而非只检查文件存在。
- 安装过程不应临时从互联网下载模型或运行时。构建阶段校验模型和字典的已固定哈希，安装包生成后再核对清单与哈希；离线机器也必须能安装和识别。
- OCR 组件单独分发前已有待完成的第三方许可核对；放进安装包前须逐项确认模型、字典、RapidOCR、ONNX Runtime、SkiaSharp、运行时文件及其声明文件，形成随包清单。现有 `docs/OCR-NOTICE.md` 是核对起点，不能代替最终清单。

## 用户数据与卸载

安装版通过 `installed.mode` 标识，将新用户的默认历史放在 `%LOCALAPPDATA%/ShotCab/Data`，自选目录指针放在 `%LOCALAPPDATA%/ShotCab/data-location.txt`；便携版仍使用程序旁的 `ShotCab.Data`。更新程序不覆盖历史；自选数据目录继续可用。现有便携版数据迁移到安装版仍需按下文单独验证。

从便携版或旧预览版迁移时，先显示来源、目标、所需空间和可用空间；复制并验证索引及图片后再切换数据指针，失败时保留原目录可回退。升级不得覆盖 `settings.json`、历史数据库、图片和标注文档。卸载默认保留截图历史，只有用户明确选择并看到具体目录时才删除用户数据；自选目录尤其不能被安装器递归清理。微软的 Windows 应用建议也强调按用户安装、可卸载及让用户选择保留数据。[Windows 应用安装与卸载建议](https://learn.microsoft.com/en-us/windows/apps/get-started/best-practices)

## 安装、升级与系统集成

- 优先评估无需管理员权限的按用户安装，允许选择安装位置；安装前检查目标盘空间，尤其同时计入约 148 MB 的 OCR 组件、压缩包展开及更新暂存空间。本机 C 盘空间有限，项目构建和试装不得未经用户确认向 C 盘下载工具或大文件。
- 检测 .NET Framework 4.8 或更高版本，缺失时给出明确说明和可信的安装来源；不要把“系统是 Windows 11”当作已验证的先决条件。微软提供通过 `HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full` 的 `Release` 值进行检测的方法。[.NET Framework 版本检测](https://learn.microsoft.com/en-us/dotnet/framework/install/how-to-determine-which-versions-are-installed)
- 安装开始前处理运行中的 ShotCab、托盘图标和 OCR 子进程；升级应保留快捷键、开机启动选择、侧栏设置和自选数据目录，失败可回滚到旧程序。卸载应移除程序文件、快捷方式和由安装器创建的注册表项，且不误删用户自己的截图。
- 安装版应在“已安装的应用”中可见，支持静默安装与卸载，并验证开始菜单、托盘、全局快捷键、截图、剪贴板和多屏行为。签名证书及 Windows 下载信誉提示需在公开发布前评估。

## 安装技术与开源发布

当前原型采用 NSIS 3.12 的按用户 EXE 安装器，支持可选 OCR 和离线安装。MSIX 仍可作为后续路线评估，但其文件与注册表虚拟化会改变现有行为，须实测后选择。[MSIX 桌面应用运行机制](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes)

选择工具时同时核对其**当前**许可与构建成本，不能假定旧版本条款仍适用；例如 Inno Setup 的官方页面现列出商业许可信息，WiX 也公布了维护费用条件。[Inno Setup 官方许可信息](https://jrsoftware.org/isorder.php)、[WiX 官方费用说明](https://docs.firegiant.com/wix/osmf/)。工具选择不改变 ShotCab 对 ShareX 上游及其他组件的义务：公开二进制时，应提供与该版本对应的源码、构建脚本和许可声明；发布流程需能核对二进制与源码版本。[GNU GPLv3 正文](https://www.gnu.org/licenses/gpl-3.0.html)

## 实施顺序与验收

1. 固定 OCR 依赖、模型、哈希和第三方许可清单；构建可重复的主程序与 OCR 目录。
2. 区分安装版和便携版的数据目录规则，完成旧数据迁移与回退测试。
3. 做安装器原型：把 OCR 放到默认相对路径，检查先决条件、空间、权限与进程占用。
4. 在干净的 Windows 10/11 x64 虚拟机上，分别测试无网络安装、OCR 直接识别、自动识别、升级、修复、卸载后保留历史及重新安装恢复历史。
5. 额外覆盖非 C 盘安装、低磁盘空间、自选数据目录、混合 DPI/多屏、开机启动、全局快捷键和长路径；记录失败时用户看到的提示及回滚结果。
6. 公开发布前核对安装包签名、文件清单、SHA-256、源码包与许可文件，并保存一次完整安装/升级/卸载的测试记录。

完成标准：新用户离线安装后无需选 OCR 路径即可识别；升级和卸载不会意外丢失截图；便携版历史能安全迁移；安装包与对应源码、第三方声明可以相互追溯。
