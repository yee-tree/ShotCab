# Upstream provenance

ShotCab (Screenshot Cabinet / 图柜) vendors ShareX v17.1.0, commit
`7c0537fd8f55ab865a668aae4e0c915bd350b4f7`, from https://github.com/ShareX/ShareX.
Upstream licenses and copyright notices are preserved in upstream/ShareX.
Only the ScreenCaptureLib, ImageEffectsLib, MediaLib and HelpersLib source trees
needed by ShotCab are included in this repository. The main ShareX application,
uploaders, FFmpeg binary, unrelated installers and recording UI are not included.
ShotCab modifies the vendored capture/editor sources. Original ShotCab changes
are offered under GPL-3.0-or-later; the ShareX portions retain their upstream
license, and the combined program is distributed under GPLv3 with its source.

The Windows Script Host COM reference is replaced by late binding to allow SDK
MSBuild to compile without Visual Studio COM wrapper generation. Runtime shortcut
behavior is unchanged. Build caches and downloads are redirected to F:.
