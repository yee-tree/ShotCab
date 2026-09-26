Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"
!include "Sections.nsh"

!ifndef STAGE
  !error "STAGE must point to the verified installer payload"
!endif
!ifndef OUTFILE
  !error "OUTFILE must point to the installer output"
!endif
!ifndef ROOT
  !error "ROOT must point to the repository"
!endif
!ifndef APP_VERSION
  !error "APP_VERSION is required"
!endif

Name "ShotCab / 图柜"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\ShotCab"
InstallDirRegKey HKCU "Software\ShotCab" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
ShowInstDetails show
ShowUninstDetails show
Icon "${ROOT}\assets\ShotCab-icon.ico"
UninstallIcon "${ROOT}\assets\ShotCab-icon.ico"
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "ShotCab"
VIAddVersionKey "FileDescription" "ShotCab per-user installer"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "LegalCopyright" "ShotCab contributors"

!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${ROOT}\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

LangString MainName ${LANG_SIMPCHINESE} "ShotCab 主程序（必装）"
LangString MainName ${LANG_ENGLISH} "ShotCab application (required)"
LangString OcrName ${LANG_SIMPCHINESE} "离线 OCR（可选，约 130 MB）"
LangString OcrName ${LANG_ENGLISH} "Offline OCR (optional, about 130 MB)"
LangString MainDesc ${LANG_SIMPCHINESE} "截图、图片编辑、侧栏与历史。安装到当前用户，不需要管理员权限。"
LangString MainDesc ${LANG_ENGLISH} "Capture, editor, sidebar and history. Installs for the current user without administrator rights."
LangString OcrDesc ${LANG_SIMPCHINESE} "在本机识别图片文字，包含运行时与模型；安装时不联网下载。"
LangString OcrDesc ${LANG_ENGLISH} "Recognize image text locally. Includes runtime and models; setup does not download files."
LangString NeedX64 ${LANG_SIMPCHINESE} "ShotCab 需要 64 位 Windows。"
LangString NeedX64 ${LANG_ENGLISH} "ShotCab requires 64-bit Windows."
LangString NeedNet48 ${LANG_SIMPCHINESE} "ShotCab 需要 .NET Framework 4.8 或更高版本。请先从微软安装后重试。"
LangString NeedNet48 ${LANG_ENGLISH} "ShotCab needs .NET Framework 4.8 or later. Install it from Microsoft, then retry."
LangString AppRunning ${LANG_SIMPCHINESE} "ShotCab 正在运行。请先从任务栏托盘退出 ShotCab，再继续安装或卸载。"
LangString AppRunning ${LANG_ENGLISH} "ShotCab is running. Exit it from the system tray before installing or uninstalling."

Section "$(MainName)" SEC_MAIN
  SectionIn RO
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File /r "${STAGE}\app\*"
  ; A prior OCR installation is removed when the user leaves the optional box clear.
  !include "${STAGE}\uninstall-ocr.nsh"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\ShotCab"
  CreateShortcut "$SMPROGRAMS\ShotCab\ShotCab.lnk" "$INSTDIR\ShotCab.exe" "" "$INSTDIR\ShotCab.exe" 0
  CreateShortcut "$SMPROGRAMS\ShotCab\Uninstall ShotCab.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\ShotCab" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShotCab" "DisplayName" "ShotCab / 图柜"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShotCab" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShotCab" "Publisher" "ShotCab contributors"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShotCab" "DisplayIcon" "$INSTDIR\ShotCab.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShotCab" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShotCab" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShotCab" "NoRepair" 1
SectionEnd

Section /o "$(OcrName)" SEC_OCR
  SetOutPath "$INSTDIR\ocr"
  File /r "${STAGE}\ocr\*"
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_MAIN} $(MainDesc)
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_OCR} $(OcrDesc)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Function .onInit
  Call CheckNotRunning
  ${IfNot} ${RunningX64}
    IfSilent +2
    MessageBox MB_ICONSTOP "$(NeedX64)"
    SetErrorLevel 2
    Abort
  ${EndIf}
  SetRegView 64
  ClearErrors
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} ${Errors}
  ${OrIf} $0 < 528040
    IfSilent +2
    MessageBox MB_ICONSTOP "$(NeedNet48)"
    SetErrorLevel 2
    Abort
  ${EndIf}
  ${GetParameters} $R0
  ${GetOptions} $R0 "/OCR=" $R1
  ${If} $R1 == "1"
    SectionSetFlags ${SEC_OCR} ${SF_SELECTED}
  ${EndIf}
FunctionEnd

Function CheckNotRunning
  System::Call 'kernel32::OpenMutexW(i 0x00100000, i 0, w "Local\ShotCab.Desktop") p .r0'
  ${If} $0 != 0
    System::Call 'kernel32::CloseHandle(p r0)'
    IfSilent +2
    MessageBox MB_ICONEXCLAMATION "$(AppRunning)"
    SetErrorLevel 2
    Abort
  ${EndIf}
FunctionEnd

Function un.onInit
  System::Call 'kernel32::OpenMutexW(i 0x00100000, i 0, w "Local\ShotCab.Desktop") p .r0'
  ${If} $0 != 0
    System::Call 'kernel32::CloseHandle(p r0)'
    IfSilent +2
    MessageBox MB_ICONEXCLAMATION "$(AppRunning)"
    SetErrorLevel 2
    Abort
  ${EndIf}
FunctionEnd

Section "Uninstall"
  SetShellVarContext current
  !include "${STAGE}\uninstall-ocr.nsh"
  !include "${STAGE}\uninstall-app.nsh"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\ShotCab\ShotCab.lnk"
  Delete "$SMPROGRAMS\ShotCab\Uninstall ShotCab.lnk"
  RMDir "$SMPROGRAMS\ShotCab"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "ShotCab"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShotCab"
  DeleteRegKey HKCU "Software\ShotCab"
SectionEnd
