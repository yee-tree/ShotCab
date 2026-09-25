# ShotCab 自有项目与架构评估

## 结论

可以把 ShotCab 做成自己的品牌、产品和独立维护的开源项目；当前并非只有改名，已有独立的历史、侧栏、OCR 交互和存储代码。但当前发行程序仍直接引用修改后的 ShareX 截图与标注库，不能把这些上游代码声明为完全原创，或仅凭改名、换 UI、拆分 DLL 就改成闭源许可证。

本地 upstream/ShareX/LICENSE.txt 是 GPL v3。GPL 允许修改、再发行和商业销售；发行派生版本时需要保留相关版权/许可说明，并按许可提供相应源码。参考：[GPL v3 正文](https://www.gnu.org/licenses/gpl-3.0.html)、[GNU GPL FAQ](https://www.gnu.org/licenses/gpl-faq.html#GPLCommercially)。本评估是技术与许可边界梳理，不是对所有依赖的完整发行合规审计。

## 现有模块边界

| 模块 | 当前职责 | 与 ShareX 的关系 |
|---|---|---|
| ShotCab.Core | SQLite 索引、独立图片文件、回收站、标签、清理、迁移、配置 | 项目引用不依赖 ShareX，适合保留并扩展 |
| ShotCab.App | WinForms 侧栏、完整历史、预览卡片、剪贴板、快捷键、OCR 界面 | CabinetContext 直接使用 RegionCaptureForm、RegionCaptureOptions、标注类型 |
| ShotCab.Ocr | 独立进程运行离线 OCR | 使用 RapidOcrNet，与 ShareX 截图引擎分离；依赖各自许可仍需核对 |
| upstream/ShareX | 截图遮罩、滚动截图、标注对象、撤销、输出合成 | 当前最重要的上游依赖；ShotCab 扩展仍位于其源码中 |

## 主要技术债

- CabinetContext 同时处理捕获、编辑、存储、窗口生命周期、快捷键、菜单和复制，难以独立替换引擎。
- 编辑文档中的 ShapeType 名称和部分属性直接沿用上游类型，替换引擎必须兼容旧文档或提供迁移器。
- UI 与 WinForms 控件状态耦合较强；原生绘制、透明背景、焦点与 DPI 的行为必须做真实桌面测试。
- 当前针对预览版打包，尚未完成全部依赖/模型的许可清单与公开发行审计。

## 建议的逐步重构路线

1. 建立 ShotCab.Contracts：定义截图结果（位图、原始像素范围）、编辑文档版本、捕获/编辑/剪贴板接口，避免上层直接引用 ShareX 类型。
2. 建立 ShotCab.ShareXAdapter：集中管理上游调用与兼容转换，先保持现有功能；不要先重写已经稳定的历史存储。
3. 拆分 CaptureWorkflow、HistoryService、ClipboardService 和窗口协调职责；侧栏只发出用户命令并订阅结果。
4. 将面板/编辑器设计令牌和控件归入 ShotCab.UI；建立混合 DPI、重绘、复制和快捷键端到端验收。
5. 若未来希望完全摆脱 ShareX，按独立规格逐步实现新的截图/编辑引擎，同时提供旧文档迁移。原有 GPL 派生代码并不会因为搬家或重构而失去许可要求。

继续开源时，可优先沿用兼容的 GPL 路线并独立维护。若目标是闭源发行，需要另行确认授权或重新实现/替换受限制的部分，不能把仅改变进程或 DLL 边界当成自动规避许可的方法。

## 本轮设计参考

只借鉴 Apple ui copy 中的柔和中性色、较大圆角、留白和内容优先原则；没有复制其品牌文案或素材。编辑器保留 ShotCab 工具与工作流，使用两行圆角工具面板。此设计参考不改变源码许可。
