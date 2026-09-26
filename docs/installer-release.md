# ShotCab 0.1.0 按用户安装包

状态：本机可用的安装包候选版已作为 [v0.1.0 开发预览版](https://github.com/yee-tree/ShotCab/releases/tag/v0.1.0) 发布；安装包尚未签名，跨机器验收尚未完成。本机生成物在 `artifacts/ShotCab-0.1.0-win-x64-setup.exe`，以发布页提供的 SHA-256 和构建时的 `payload-manifest.tsv` 为准。

## 用户安装行为

- 安装器使用 NSIS 3.12，默认安装到当前用户的 `%LOCALAPPDATA%/Programs/ShotCab`，不请求管理员权限；用户可在向导中更改安装位置。
- 主程序为必装组件。“离线 OCR”为独立可选组件，默认不勾选；组件页解释其作用和约 130 MB 的安装体积。安装器本身离线包含 OCR 所需文件，不在安装时下载。
- 安装版以 `installed.mode` 区分便携版。新安装用户的历史与设置默认位于 `%LOCALAPPDATA%/ShotCab/Data`，自选数据目录指针位于 `%LOCALAPPDATA%/ShotCab/data-location.txt`。便携版仍使用程序旁的目录。
- 卸载只移除安装清单中的程序文件、开始菜单快捷方式和当前用户的卸载注册表项，保留用户历史、设置和安装目录中未知的用户文件。再次安装后仍可读取原数据。
- 安装前检查 64 位 Windows 和 .NET Framework 4.8；运行中的 ShotCab 应先从托盘退出。

## 严格载荷

`scripts/build-installer.ps1` 从 Release 输出按文件名白名单复制 21 个主程序文件和 4 个中文资源 DLL，再加入用户说明、`installed.mode` 和实际组件的许可声明。OCR 从全新 `dotnet publish --self-contained win-x64` 目录筛选运行所需 EXE、DLL 和 JSON，只复制三个 v6 模型/字典与一个 v5 方向分类模型，逐个核对固定 SHA-256。旧的 v5 检测/识别模型、PDB、LIB、崩溃诊断工具、所有源码/测试/缓存、开发者截图与设置均不进入安装载荷。Blob Emoji 贴纸不进入安装包。

每次构建使用新的 `artifacts/ShotCab-installer-stage-*` 目录，生成 `payload-manifest.tsv` 记录相对路径、字节数和 SHA-256。NSIS 只读取已验证的 `app` 与 `ocr` 子目录；OCR 未选中时不会复制到用户电脑。源代码对应包由 `scripts/package-source.ps1` 生成，并包含安装脚本、构建脚本和许可文本。

## 本机验证

在 Windows x64 本机、F 盘测试安装目录执行：

- 9 项核心测试通过，安装版/便携版数据目录分离测试通过；主程序 Release 构建完成。上游 `.resx` 仍有重复资源名警告，构建没有错误。
- 精简载荷的 OCR 中英混排、单字坐标、取消、选字窗口与编辑器自动识别测试通过。该测试曾揭示 v5 方向分类模型是实际依赖，现已纳入 OCR 白名单。
- 静默安装默认选项后，只有主程序，没有 OCR 或历史数据；使用 `/OCR=1` 后包含 OCR。安装后 233 个载荷文件与清单逐个 SHA-256 匹配；从已安装目录运行完整 OCR 测试通过。
- 升级时取消 OCR 选择会移除旧 OCR 文件，同时保留测试用户文件；静默卸载后主程序和卸载注册表项消失，测试用户文件保留。
- 持有 ShotCab 运行互斥锁时，静默安装立即返回错误码 2，不会停在提示框，也没有安装任何文件。

核心迁移测试在一次运行中遇到 Windows 临时目录的间歇性“访问被拒绝”；随后的三次独立重跑均通过。未在本轮改动历史迁移实现，该环境问题仍需在其他机器复核。

尚需在干净 Windows 10/11 x64 机器上进行交互式组件页、快捷方式、缺失 .NET、正在运行时的提示、低磁盘空间、真实用户历史迁移与卸载后恢复验证。安装包当前未签名，Windows 可能显示未知发布者提示。预览版已随附同一提交的源码包、SHA-256 和完整第三方声明；这份本机记录不代替跨机器或法律验收。

## 构建

使用官方 NSIS 3.12 `makensis.exe`，将工具和 NuGet/OCR 缓存放在 F 盘：

```powershell
./scripts/build-installer.ps1 -NsisPath 'F:\path\to\nsis-3.12\Bin\makensis.exe'
./scripts/package-source.ps1
```

模型、字典及运行依赖的来源见 [OCR-NOTICE.md](OCR-NOTICE.md)；后续迁移、升级及发布验收清单见 [installer-plan.md](installer-plan.md)。
