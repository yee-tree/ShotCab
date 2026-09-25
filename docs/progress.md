# ShotCab implementation ledger

## 2026-09-24 first-display and quick-capture fixes

- The editor toolbar is now styled and populated before its separate window first appears. This removes the upstream-theme first-paint flash and makes the first opening use the same layout as later openings.
- F6's rectangular selection always uses quick crop. Completing the drag immediately saves, copies, and switches the sidebar to Recent. With direct editing enabled, the initial screenshot is committed before the editor opens; closing the editor leaves that record intact.
- The sidebar minimum width is 180 logical pixels, including settings, drag resize and docking validation. The narrow variant shortens navigation labels, places switch controls below long labels, and keeps image thumbnails and timestamps visible.
- Release build, core/editor suites and window smoke pass on Windows 11 build 26200. A modal editor first-opening probe completes without showing an unstyled toolbar. Actual F6 dragging and external-app paste on the user's desktop remain to be checked manually.
- Two concept screens in `artifacts/design-mockups` show the proposed post-capture result strip and simplified editor. They are design references, not yet implemented.

Approved specification: user-provided ShotCab complete implementation plan, 2026-09-17.

- [x] Inspect empty workspace, acquire and pin ShareX 17.1.0 source.
- [ ] Build and validate isolated capture libraries.
- [ ] Implement history/settings/storage with behavioral tests.
- [ ] Implement capture/editor persistence and redaction.
- [ ] Implement side panel, clipboard, full history, labels and settings.
- [ ] Implement optional offline OCR worker and selectable overlay.
- [ ] Package, automated smoke/performance checks, independent review.

Ruling: New empty workspace is its own implementation repository; no second worktree needed.
Ruling: Preserve upstream source in the repository for reproducible licensed builds;
retain original clone metadata only in ignored .cache.
Ruling: Performance and cross-OS compatibility are acceptance tests, not assumptions.

## 2026-09-18 verified progress

- App builds in Release with zero warnings/errors (latest `.cache/app-build.log`).
- Eight core behavior groups pass: settings, version-safe OCR, search/tag filters,
  SQL paging, safe orphan-version recovery, migration, trash/expiry, tag maintenance.
- Editor roundtrip, atomic failed restore, unsupported version and retained non-mask
  layers after baking pass. Rendering mask pixels needs stronger cases than white input.
- Smoke render now actually shows controls and rejects a blank result.
- Fixed history preview split sizing, dark input colors, aspect-preserving pins,
  manually hidden sidebar state, click-through recovery and fullscreen-mode resets.
- History query performs SQL LIMIT/OFFSET; sidebar excludes hidden records before paging.
- Thumbnail files are cached inside each version directory and purged with that version.
- Synthetic performance: 1000 1920x1080 images, query50 P95 3.74ms,
  sidebar private bytes 46,329,856, ten-second machine-normalized CPU rounded 0.000%.
  Current Windows 11 build22631, single1920x1080 display, 16 logical CPUs.
  This is an off-screen rendered sidebar test, not a complete performance acceptance.
- Actual optional worker sample recognized Chinese/English and returned per-character polygons.
- Added Chinese README and preview packaging script. Public release must include
  complete corresponding source and all dependency notices.

## Remaining work (scope preserved)

- Complete explicit solid masking/spotlight and verify the full enhanced annotation set.
- Fix monitor result cropping and permanent-bake rendering consistency, retain raster-edit status.
- Hide unused upload UI; scrolling capture integration must hide our overlays and clearly label partial results.
- Broaden clipboard/drag/focus, OCR selection/cancellation, shutdown and low-disk tests.
- Finish settings polish (live theme, readable selectors, max-size filter), DPI handling and pin modes.
- Validate capture P95 and editor/long-capture/10-pin/OCR peak and release memory.
- Windows10, mixed-DPI/negative coordinates/HDR/hotplug and external-app paste/drag acceptance remain unverified.
- Finish sourced competitor/video interaction notes, feature comparison, license inventory and release packaging.
- Independent reviews were attempted through agents; agents stopped with partial work.
  Root inspected and completed the interrupted SQL/recovery and editor-test changes.
  No review approval is claimed.

## 2026-09-18 annotation safety follow-up

- Fixed monitor/active-monitor capture base cropping and document coordinate origin.
- Removed the unused upload button from the editor toolbar.
- Permanent bake now creates its base/document once and renders the saved composite
  from that new base plus remaining layers, rather than reusing a pre-bake image.
- Persisted RasterEdited metadata prevents reopening a cropped/raster-edited record
  from incorrectly returning its automatic status to Original.
- Magnifier output samples an effect-applied snapshot instead of the original canvas.
  Snapshot allocation occurs only when a magnifier exists, and is disposed after rendering.
- Render failures restore shape offsets and rendering flags and dispose temporary output.
- Added patterned-input regression checking ordinary masked+magnified pixels against
  reopened baked output, and checking that sensitive source pixels actually change.
- Release app build and expanded editor tests pass. These changes are newer than
  the preview ZIP from 01:35; rebuild the preview before testing these fixes.

## 2026-09-18 solid mask and spotlight

- Added named toolbar tools 实心遮挡 and 聚光灯, appended enum values to preserve
  existing stored enum numbers, and integrated shape construction/document persistence.
- Solid mask is opaque black, covers fractional boundaries outwards, is classified
  as redaction and participates in permanent baking.
- Spotlight shades outside the selected ellipse, remains editable after baking other
  masks, and is not classified as redaction.
- Pixel regressions verify center/outside lighting, mask edge coverage, baked pixels,
  and remaining layer identity/state. Expanded editor tests pass.
- Rendered the effects together in `artifacts/smoke/editor-effects.png`; inspected
  output confirms spotlight and solid mask are visible. Toolbar is a separate window
  and is not captured in that form-only image.
- Updated capture/editor window titles to ShotCab · 图柜.
- Style customization for these two new tools still needs a complete user-facing control.

## 2026-09-18 effect styles

- Added 遮挡/聚光样式 editor action with color picker and darkness control.
  Changes update the selected effect and defaults; cancel leaves values unchanged.
- Added named settings properties for default mask color and spotlight darkness.
- Explicit style undo/redo pixel regression passes, including transparent-color input
  normalized to opaque mask output and darkness clamping.
- Settings editor now edits a separate annotation draft so Cancel preserves defaults.
- Corrected fullscreen-setting explanation and sidebar logical-pixel description.

## 2026-09-18 scrolling capture

- Added capture lifecycle callback to hide/restore other ShotCab forms during selection
  and scrolling, and replaced UI-thread sleep with asynchronous delay.
- Long capture window uses ShotCab title and editing/saving actions; copied results
  pass through the editor/history workflow instead of an untracked clipboard route.
- Added textual complete/partial/failure status and instructions to inspect seams.
- Closing while actively capturing requests stop and preserves the preview.
- Removed previous-scroll-distance guessing when a frame has no credible overlap.
  An uncertain join stops with partial status and retains the accumulated image.
- Added synthetic scrolling test: two 200-row frames offset80 produce exactly280
  correct rows; a subsequent unrelated frame is rejected without altering retained pixels.
- Expanded editor/scrolling tests pass. Real browser/app scrolling and lifecycle
  integration still require runtime acceptance; synthetic matching is narrower evidence.

## 2026-09-18 OCR interaction and record lifecycle

- Replaced shared-record boolean protection with reference counting across pins,
  editors and OCR windows. An OCR view now prevents expiry, purge, migration and
  concurrent editing/baking until closed; one closed view cannot release other users.
- Added in-process interaction tests for cross-line and reverse OCR selection at
  50%,100%,200% scale, whitespace preservation, select-all and last-user release.
- Clipboard/drag payloads now own and dispose their temporary bitmaps/streams after
  clipboard flush or completed drag, including exception paths.
- Added bitmap/PNG data-format and pixel checks without altering the user's clipboard.
- Full external-app clipboard/drag acceptance is still unverified.

## 2026-09-18 real OCR integration regression

- Actual worker-to-app test exposed System.Drawing.Point JSON incompatibility;
  added an integer-coordinate object converter. Prior worker-only evidence did not
  establish a working app integration; this test now does.
- Added real worker success, in-flight cancellation, pre-cancellation, missing-component
  and temporary-job cleanup tests via `ShotCab.exe --ocr-test <output> <worker>`.
- Exiting now closes OCR windows to trigger owned worker cancellation.
- Fixed fit/shown callbacks referencing a caller-owned image after its disposal.
- Added real OCR window rendering and fit-button regression using a disposed caller image.
- Visual inspection exposed split-block reading order; rows now sort blocks by position
  and remove spatially overlapping duplicate boundary glyphs. Actual sample asserts
  the complete phrase `Screenshot Cabinet 2026` in reading order.
- Runtime suite passes; latest sample recognition 1625ms (one small sample, not broad OCR benchmark).
- Screenshot `artifacts/ocr-tests/ocr-window.png` inspected. Complex columns, rotated text,
  ambiguous OCR block boundaries and broad recognition accuracy still need evaluation.

## 2026-09-18 history size range

- Added optional maximum internal size alongside minimum size in the full-history UI.
  Disabling the maximum removes its SQL limit. Filter area auto-sizes across rows.
- Added interaction checks using the actual form query and database count, including
  zero maximum and disabling the limit. Smoke rendered the updated layout.
- Native combo selected labels remain absent in DrawToBitmap artifacts; actual
  on-screen selector readability needs independent verification before UI sign-off.
- Renamed OCR copy/select controls to describe the current image rather than imply
  recognition of an unmodified original.

## 2026-09-18 desktop verification and packaging isolation

- Initialized updated computer-use sky runtime and attempted desktop verification.
  Tool list_windows/list_apps did not expose ShotCab after launch, although the owned
  process existed and diagnostic trace reached startup maintenance complete.
  No native desktop screenshot or selector-readability approval was obtained.
  Only the test processes launched in this attempt were stopped afterward.
- Added `--history` to open full history and opt-in `--diagnose` startup tracing.
- Packaging now copies only compiled root assemblies/config and runtime/culture folders,
  excludes history/settings/pointers/logs, and checks ZIP entries for runtime-data leakage.
  This prevents a build-directory test run from contaminating a later portable package.

## User feedback fixes: sidebar and immediate capture

- Removed inherited CenterScreen startup from sidebar; manual positioning plus OnShown
  position ensures right-edge placement. Regression asserts real bounds against working area.
- Default width320 logical pixels; old220-or-narrower default migrates to320. Added visible
  header close button (hide, reopen F8), and Exit in inline basic settings.
- Recent/History/Settings now switch content inside the sidebar. Basic controls edit
  history enabled, retention, recent count, width and format; detailed dialog remains available.
  Settings allow activation for keyboard entry while image browsing stays non-activating.
- Removed second editor confirmation after completed capture. Confirmation now immediately
  persists/refreshes/copies the composed image; later edits are initiated from history.
- Newest record resets list scroll. Regression asserts immediate count increment, change
  event and independent image file, without modifying the user's clipboard.
- Added actual-file location action and explained indexed-file storage in UI/README.
- Smoke rendered both tabs and visible close button; build and interaction checks pass.

## 2026-09-18 right-click lifecycle and navigation

- Regression reproduced the reported ContextMenuStrip ObjectDisposedException inside
  WinForms SetVisibleCore/Close before the fix. Menus now have application lifetime;
  entries are disposed/rebuilt on the next invocation and the menu is disposed at shutdown.
- Interaction regression performs 12 repeated open/close/reopen/action cycles, checks
  that closing does not dispose the menu and that favorite state is refreshed each time.
- Sidebar navigation uses equal-width columns, selected-page color and accessibility
  description. Switching back from basic settings restores the recent-page highlight.
- Fresh core suite (8 groups), annotation suite, UI smoke and actual offline OCR suite
  passed. These checks do not replace manual Windows 10/11 and external-application acceptance.
- Added source packaging with explicit source roots and archive checks excluding runtime
  history, caches and build output; includes modified upstream and build scripts.
- Pinned SHA256 for the OCR dictionary in addition to detection/recognition model hashes.
  Full dependency-notice audit and optional OCR redistribution packaging remain open.

## 2026-09-18 frosted sidebar and dock resizing

- Replaced whole-window translucency with a self-drawn opaque frosted-style surface,
  cached gradient/grain texture, rounded buttons and high-contrast navigation. This is
  a decorative frosted finish, not live desktop blur; underlying application text no
  longer bleeds through. The old opacity field remains readable for config compatibility.
- Added left/right dock choice to inline settings and the detailed property grid.
  AppBar reservation chooses the same edge. A seven-pixel inward-facing drag grip
  adjusts width between 280 and 640 logical pixels and saves on release/capture loss.
- Basic settings fit available width; labels wrap and numeric controls shrink. Full
  opacity preserves control readability. Dragging/focused settings prevent auto-fold.
- Passed left/right placement, drag endpoint limits, settings clipping at minimum width,
  selected-tab styling, menu regression, settings side/width persistence and 8 core suites.
- Inspected rendered sidebar settings. Live mouse drag, mixed-DPI and reserved work area
  remain manual acceptance cases. No native desktop blur capability is claimed.

## 2026-09-19 panel menu and detachable cards

- Fixed native ButtonBase rectangular background painting beneath the rounded custom
  buttons by painting the parent surface first at the proper offset. Sidebar rendered
  without the prior square/double edge. Detailed settings styling is unchanged.
- Added shared panel-colored context-menu rendering, rounded hover selection, padding
  and separators for history image menus and pin/card menus.
- Alt + drag from either sidebar photo tab creates an independent borderless preview
  card. Ctrl/Shift alternatives are configurable in basic settings. Ordinary drag
  keeps the existing copy/file payload. Each card starts at 420x320 and fits its image.
- Cards have caption drag, minimize/taskbar restore, maximize/restore, close, edge/corner
  resizing, wheel scaling and existing image actions. Record references protect open
  cards from cleanup, and close releases those references.
- Smoke tests check card chrome, minimize/maximize/close and reference lifecycle;
  rendered card/menu/sidebar inspected. Settings tests verify modifier persistence.
  Actual held-key native drag and mixed-DPI resizing still need manual acceptance.

## 2026-09-19 settings layout and capture visibility

- Settings controls are now constructed while hidden with layout suspended, then shown
  in one completed pass. Opening resets scrolling to the top. A buffered opaque content
  surface avoids transparent scrolling artifacts and repeated ancestor-background painting.
- Simple setting changes save preferences without repositioning the sidebar, registering
  hotkeys, rebuilding thumbnails or refreshing all history. Width/docking still apply layout.
- Added default-on HideSidebarDuringCapture to basic settings. Both regular and scrolling
  captures filter the hidden-window snapshot through this preference, so restoration only
  applies to windows actually hidden for that capture.
- Regression: five settings switches completed in 335ms total on this machine; initial
  scroll position, hide preference on/off and persisted default/override passed. Core
  suite (8 groups), UI smoke and screenshot render passed. Native desktop repaint behavior
  on the user's running instance still needs confirmation after updating.

## 2026-09-19 panel paint and editor usability

- Rounded panel controls now inherit directly from Control, removing native BUTTON
  painting rather than attempting to paint over its borders. Keyboard Enter/Space and
  accessible push-button role are retained. Render checks pass; native repaint artifacts
  still require verification in the user's running desktop.
- Added default-off EditAfterCapture and EditorCursorPreview settings, with inline
  checkboxes. Successful captures can enter the editable document workflow before save;
  the existing immediate-save flow remains the default. Cursor magnifier and coordinates
  toggle together, and editor options are cloned so capture magnification is unaffected.
- Localized the unsaved-changes prompt and title to Simplified Chinese/ShotCab.
- Grouped editor tools into shapes, text, redaction and helpers, with icon/text descriptions
  of each effect. Completion remains directly visible; secondary commands use More actions.
- Core settings round-trip (8 groups), editor safety suite, smoke/render suite and actual
  grouped pixelate selection pass. Live capture-to-editor and native popup interactions
  remain manual acceptance cases. Detailed settings visual design remains unchanged.

## 2026-09-19 soft UI, preset settings and copy compatibility

- Read local Apple ui copy design tokens as reference only. The editor now uses rounded
  tool pills with icons and Chinese labels in a wrapping two-row palette; advanced tools
  remain grouped. Theme-aware neutral canvas and rounded toolbar host replace the dense row.
- Automatic state badges use distinct colors in a dedicated area below the thumbnail;
  image pixels, state and date/dimensions have separate vertical regions.
- Settings now have two rows of six categories. Numeric inputs use common presets and
  a small custom text field, with range validation and no spin buttons.
- F6 region capture now applies the same magnifier/info preference as the editor.
- Right-click selects the actual item under the pointer in the strip and full-history list.
- Clipboard payload now includes an explicit DIB as well as PNG/bitmap, retries busy access
  and checks image presence before claiming ownership. Eight actual system clipboard
  readback cycles passed, restoring the prior clipboard afterward. External target-app
  acceptance and the user's intermittent failure are not assumed fully reproduced.
- UI smoke, category/preset/custom-value tests, right-click selection, core and editor
  regression suites passed. Native held-key gestures and mixed-DPI verification remain open.
- Added docs/architecture-ownership.md with the current dependency map, refactoring stages
  and GPL redistribution boundaries. This does not change the license or publish the project.

## 2026-09-19 controls, date groups and languages

- Added owner-drawn rounded toggle switches and dropdowns, plus rounded shortcut input
  containers. Sidebar native checkbox/radio/dropdown chrome is replaced. Choice menus have
  control-owned lifetime, avoiding disposal inside the close transition.
- Sidebar History groups visible records by local creation date. Clicking a date toggles
  its records; counts remain visible and collapsed dates survive list refresh in the session.
  Recent stays a flat list; complete-history search/filter behavior is preserved.
- Replaced Cards category with General & hotkeys. Added keyboard shortcut recording with
  duplicate/OS-registration checks and rollback on failure. Language, launch at startup,
  theme, fullscreen behavior and card modifier are here. Removed duplicate advanced tab.
- Added persisted zh-CN/en preference and immediate translation of sidebar/menu controls;
  new editor sessions use the selected culture and English tool labels. Main history,
  OCR, preview and property labels have translation infrastructure. Some legacy specialized
  dialogs, descriptions and diagnostic strings still need a full language-coverage audit;
  this is not a claim that every upstream dialog is fully localized.
- Verified grouped collapse/expand, duplicate-shortcut rollback, language persistence,
  preset/custom inputs, core tests and editor regression. Rendered Chinese and English
  settings, general/hotkeys and grouped history; corrected English label clipping.

## 2026-09-21 无边框编辑器窗口与工具栏排版

- 编辑模式的 RegionCaptureForm 不再使用系统边框（`FormBorderStyle.None`），改为自绘标题栏：
  左侧显示窗口标题（含图片尺寸、缩放比例与文件名），右侧是三枚圆角窗口按钮
  （最小化、最大化/还原、关闭），配色与编辑器工具面板、预览卡片共用同一套柔和中性令牌。
- 窗口按钮继承自 Control（与侧栏圆角按钮同样的做法，避免原生矩形底色），带悬停态、
  提示气泡与无障碍名称；关闭走既有 FormClosing 流程，因此“未保存修改”确认仍然生效。
- 无边框后自行应答 `WM_NCHITTEST`（7 逻辑像素、按 DPI 缩放），四边四角仍可缩放；
  最大化尺寸限制在当前屏幕工作区，避免盖住任务栏。拖动标题栏沿用工具栏既有的
  ReleaseCapture + WM_SYSCOMMAND 方式，最大化状态下不拖动。
- 工具面板按 image#1 的紧凑圆角样式重排：每个工具显示“中文标签 + 图标（图标在右）”，
  主操作“完成并复制”带绿色对勾与强调描边；常用工具与分组（图形/文字/遮挡/辅助）、
  更多操作、样式与文件之间用留白分隔，窗口宽度不足时自动换行为两到三行。
  不再重复截取工具标签，分组按钮会显示该组当前工具的图标。
- 标题栏高度计入 `ToolbarHeight`，工具面板与画布居中位置一并下移，避免压住标题栏。
- 验证：Release 构建零警告/错误；交互检查断言无边框状态、标题栏位置与三个按钮不越界不重叠、
  工具面板不与标题栏重叠、五处边缘命中测试返回对应缩放光标且客户区不返回缩放命中、
  最大化（含工作区高度与还原图标切换）、还原、最小化，以及工具面板 ≥12 项、
  均为“标签 + 尾部图标”、行数为 2–3 行、主操作按钮存在。
  渲染 `artifacts/smoke-borderless/editor-effects.png`、`editor-toolbar.png`、
  `editor-toolbar-narrow.png`、`editor-light.png` 并人工检查深色/浅色两套配色。
- 仍待人工验收：真实鼠标拖动标题栏与边缘缩放、Aero Snap（无边框窗口默认不参与贴边吸附）、
  混合 DPI 与多屏下的命中区域、HDR/负坐标显示。

## 2026-09-19 设置排版与标签细化
- 设置值统一为标题下方显示；侧栏宽度辅助说明独立显示，停靠方向不再使用混合横排。
- 两行分类整合为圆角导航面板，增加轻量悬停、选中指示、分类标题及说明，支持键盘操作。
- 自动状态与自定义标签分别绘制；自定义标签统一紫色描边并展示名称，不再合并成 +N。卡片根据标签换行动态增高，缩放宽度时重新排版。
- 验证：Release 构建零警告/错误；smoke-settings-polish-final 交互及窗体渲染通过；新增自定义标签数量、边界及不重叠检查；检查中文设置、外观、日期历史和英文设置渲染。
- 此次为预览 UI 更新，实际多屏/DPI 和第三方粘贴完整验收仍按原待办执行。
# 2026-09-24 编辑器与截图结果条

- 编辑器常用标注收在与窗口同宽的底部工具排，颜色与样式在展开后的第二行；“更多”直接展开第二行，不再弹出多层工具菜单。底栏以单色图标呈现，并包含撤销、重做和完成复制。
- 根据反馈移除了图片外的半透明遮罩，恢复为实色背景。顶部只保留标题与窗口控制。
- F6 截图仍先完成保存与复制，再显示可关闭的短时结果条；条中有编辑、贴图、识别文字和打开文件操作。侧栏截图设置可关闭结果条。

## 2026-09-25 设置归并与样式即时应用

- 绘图样式面板改为点击色块或粗细后立即更新，不再显示“应用/取消”按钮；选中对象的样式变化仍进入撤销历史。
- 原独立详细设置的应用设置和默认标注属性归入侧栏既有六类。默认标注样式放在“外观”内按需展开，支持滚轮浏览；OCR 路径和预览位于“更多”。托盘“设置”打开同一侧栏。
- 中文界面的“默认标注样式”及其子项改用明确的中文名称，不再依赖上游属性名。窗口渲染及交互测试覆盖迁移项目、滚轮、即时保存和样式实时生效。
- 本机自动构建、历史测试、编辑器测试和窗口渲染已运行；真实桌面上的多屏、HDR 和第三方粘贴手感仍需人工验收。

## 2026-09-24 编辑器首帧、光标和步骤历史

- 底部工具窗在首次显示前即定位到编辑器底边；编辑器激活后主动刷新自绘标题栏，避免首次打开时标题区域等待点击才完成绘制。
- 常用图标使用独立悬停提示，选择工具恢复系统箭头光标，绘图工具保留绘图光标。
- 底栏增加历史图标，按需展开右侧步骤面板；步骤来自编辑器现有撤销/重做快照，点击可跳转，改动历史分支时清除旧后续步骤。展开时如画布放不下则临时缩小，收起后在用户未自行缩放的情况下恢复原缩放。
- 编辑器步骤历史仅在本次编辑会话有效；图片和可编辑标注文档仍照常保存在应用历史中。

## 2026-09-25 编辑器样式与取样覆盖核验

- 编辑器默认进入“选择”工具，工具提示按中英文界面语言显示；常见绘图工具可右键打开统一的样式面板，调整边框/线条颜色、粗细，矩形、椭圆和气泡框还能设置透明填充。底栏“标注颜色与线条”现在直接打开该面板。
- 已选标注的样式变更加入编辑器撤销历史；后续新建标注记住工具样式。
- 原“智能擦除”实际上读取起点像素的颜色，并以该单色填满整个矩形，没有内容识别、周围纹理采样或修复算法。它仅适合纯色背景上的小块覆盖，因此暂保留在高级工具中并改名“取样覆盖”，避免与隐私遮挡或真正的智能修复混淆。其采样颜色现在写入可编辑标注文档，重新打开时保持一致。
- 自动编辑测试覆盖默认选择、矩形和气泡框透明填充、取样覆盖颜色持久化；窗口渲染测试覆盖中文提示、样式面板与右键入口。多 DPI 和应用间实际点击手感仍需在用户桌面核验。

## 2026-09-25 历史窗口与图标

- 深色侧栏开关打开时改为浅灰轨道、深色滑块，保持黑白对比；关闭时仍为深灰轨道、白色滑块。
- 完整历史分成筛选、列表与预览、批量操作和存储管理四块。日期输入改用固定 `yyyy-MM-dd`，列表时间列优先保证完整；窗口变窄时预览列相应收窄。
- 托盘双击现在只呼出侧栏，不再打开完整历史。完整历史保持在侧栏和托盘菜单中。
- 用户选定“相片抽屉”方向后制作了矢量图标，并生成多尺寸 `.ico`；程序窗口、托盘和可执行文件使用同一图标。图标源文件与生成脚本随源码包提供。
- 自动检查覆盖侧栏重新呼出、日期控件、完整历史中英文与最小窗口渲染；仍需在真实任务栏上核验托盘交互和不同 DPI 的控件尺寸。

## 2026-09-25 侧栏拖放与样式细节

- 关闭桌面空间预留后，覆盖式侧栏以显示器物理边缘定位，避免 Windows 工作区更新延迟导致右侧留缝。保留用户选择的左/右停靠方向。
- “近期”和“历史”内容区支持拖入图片文件或位图，保存到应用历史的独立图片文件；关闭历史保存时不接收拖入。
- “修改历史时生成新记录”“为侧栏预留桌面空间”等开关增加圆形 i 悬停说明。
- 样式面板去重颜色预设，线条粗细支持 0–50 像素自定义输入；切换工具时收起旧面板，序号图标数字居中。
- 本机离线 OCR 增强包仍独立于基础便携包，实际中英混排识别、逐字位置、取消、缺失组件与窗口渲染回归通过；复杂版式准确率仍待扩充样本。
