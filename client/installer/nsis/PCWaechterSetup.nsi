Unicode True

!include "MUI2.nsh"
!include "FileFunc.nsh"

Var CleanAppDirFlag
Var RemoveUserDataFlag
Var ExistingInstallDir
Var IsUpdateInstall
Var ExistingInstalledVersion

Function ResolveInstalledVersion
  StrCmp $ExistingInstalledVersion "unbekannt" 0 done
  StrCpy $R8 "$ExistingInstallDir\PC Wächter GUI.exe"
  IfFileExists "$R8" found_path 0
  StrCpy $R8 "$ExistingInstallDir\PCWächter.exe"
  IfFileExists "$R8" found_path 0
  StrCpy $R8 "$ExistingInstallDir\PCWaechter.exe"
  IfFileExists "$R8" found_path done

found_path:
  ClearErrors
  GetDLLVersion "$R8" $R2 $R3
  IfErrors done

  IntOp $R4 $R2 / 0x00010000
  IntOp $R5 $R2 & 0x0000FFFF
  IntOp $R6 $R3 / 0x00010000
  IntOp $R7 $R3 & 0x0000FFFF
  StrCpy $ExistingInstalledVersion "$R4.$R5.$R6.$R7"

done:
FunctionEnd

Function .onInit
  StrCpy $CleanAppDirFlag "0"
  StrCpy $ExistingInstallDir ""
  StrCpy $IsUpdateInstall "0"
  StrCpy $ExistingInstalledVersion "unbekannt"

  ReadRegStr $ExistingInstalledVersion HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\PCWaechter" "DisplayVersion"
  IfErrors +2
    Goto check_existing_install_path

  StrCpy $ExistingInstalledVersion "unbekannt"

check_existing_install_path:

  ReadRegStr $ExistingInstallDir HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\PCWaechter" "InstallLocation"
  IfErrors read_parameters
  IfFileExists "$ExistingInstallDir\*.*" ask_update_confirmation
  Goto read_parameters

ask_update_confirmation:
  Call ResolveInstalledVersion
  IfSilent auto_confirm_update ask_user_update

auto_confirm_update:
  StrCpy $IsUpdateInstall "1"
  Goto read_parameters

ask_user_update:
  StrCmp $ExistingInstalledVersion "${APP_VERSION}" ask_user_reinstall ask_user_install_update

ask_user_reinstall:
  MessageBox MB_ICONQUESTION|MB_YESNO "Es ist bereits die neueste Version installiert.$\r$\n$\r$\nAktuell installierte Version: $ExistingInstalledVersion$\r$\nVersion zur Installation: ${APP_VERSION}$\r$\n$\r$\nSoll die Installation erneut ausgeführt werden?" IDYES confirm_update IDNO cancel_install
  Goto read_parameters

ask_user_install_update:
  MessageBox MB_ICONQUESTION|MB_YESNO "Eine bestehende Installation von PC Wächter wurde gefunden.$\r$\n$\r$\nAktuell installierte Version: $ExistingInstalledVersion$\r$\nNeue Version zur Installation: ${APP_VERSION}$\r$\n$\r$\nSoll das Update auf die neue Version jetzt ausgeführt werden?" IDYES confirm_update IDNO cancel_install

confirm_update:
  StrCpy $IsUpdateInstall "1"
  Goto read_parameters

cancel_install:
  Abort

read_parameters:
  ${GetParameters} $R0
  ${GetOptions} $R0 "/CLEANAPPDIR" $R1
  IfErrors +2
    StrCpy $CleanAppDirFlag "1"

  StrCpy $INSTDIR "$PROGRAMFILES64\PCWächter\versions\${APP_VERSION_CODE}"
FunctionEnd

Function un.onInit
  StrCpy $RemoveUserDataFlag "0"
  ${GetParameters} $R0
  ${GetOptions} $R0 "/PURGEUSERDATA" $R1
  IfErrors +2
    StrCpy $RemoveUserDataFlag "1"
FunctionEnd

Function CleanupInstallDirectory
  IfFileExists "$INSTDIR\*" 0 done

  FindFirst $R0 $R1 "$INSTDIR\*"
loop:
  StrCmp $R1 "" doneLoop
  StrCmp $R1 "." next
  StrCmp $R1 ".." next

  Delete "$INSTDIR\$R1"
  RMDir /r "$INSTDIR\$R1"

next:
  FindNext $R0 $R1
  IfErrors doneLoop loop

doneLoop:
  FindClose $R0

done:
FunctionEnd

Function CleanupOldVersionDirectories
  StrCpy $R0 "$PROGRAMFILES64\PCWächter\versions"
  IfFileExists "$R0\*" 0 done

  FindFirst $R1 $R2 "$R0\*"
loop:
  StrCmp $R2 "" doneLoop
  StrCmp $R2 "." next
  StrCmp $R2 ".." next
  StrCmp $R2 "${APP_VERSION_CODE}" next

  RMDir /r "$R0\$R2"

next:
  FindNext $R1 $R2
  IfErrors doneLoop loop

doneLoop:
  FindClose $R1

done:
FunctionEnd

Function un.KillRunningPcWaechterProcesses
  DetailPrint "Beende laufende PC-Wächter-Prozesse..."
  nsExec::ExecToStack '"$SYSDIR\taskkill.exe" /F /T /FI "IMAGENAME eq PC Wächter GUI.exe"'
  Pop $0
  Pop $1

  nsExec::ExecToStack '"$SYSDIR\taskkill.exe" /F /T /FI "IMAGENAME eq PCWaechter.Updater.exe"'
  Pop $0
  Pop $1

  nsExec::ExecToStack '"$SYSDIR\taskkill.exe" /F /T /FI "IMAGENAME eq PC Wächter Service.exe"'
  Pop $0
  Pop $1

  nsExec::ExecToStack '"$SYSDIR\sc.exe" stop "PC Wächter Service"'
  Pop $0
  Pop $1
FunctionEnd

; Quote a string for inclusion in schtasks /TR parameter
Function QuoteStringForCmd
  Exch $0
  ; Einfaches Quoting: Pfad in doppelte Anführungszeichen setzen
  StrCpy $0 '"$0"'
  Exch $0
FunctionEnd

!ifndef APP_TITLE
  !define APP_TITLE "PC Wächter"
!endif

!ifndef APP_PUBLISHER
  !define APP_PUBLISHER "PC Wächter"
!endif

!ifndef APP_VERSION
  !define APP_VERSION "1.0.0"
!endif

!ifndef APP_VERSION_4
  !define APP_VERSION_4 "1.0.0.0"
!endif

!ifndef APP_VERSION_CODE
  !define APP_VERSION_CODE "0000"
!endif

!ifndef APP_EXE
  !define APP_EXE "PC Wächter GUI.exe"
!endif

!ifndef UPDATER_WORKER_RELATIVE_PATH
  !define UPDATER_WORKER_RELATIVE_PATH "updater\\PCWaechter.Updater.exe"
!endif

!ifndef UPDATER_TASK_LAUNCHER
  !define UPDATER_TASK_LAUNCHER "run-updater-worker.cmd"
!endif

!ifndef UPDATE_TASK_NAME
  !define UPDATE_TASK_NAME "PCWachter Update"
!endif

!ifndef APP_ICON
  !define APP_ICON "..\..\..\shared\assets\branding\prod\pcwaechter-brand.ico"
!endif

!ifndef APP_HEADER_IMAGE
  !define APP_HEADER_IMAGE "..\..\..\shared\assets\branding\prod\name-source.bmp"
!endif

!ifndef SHORTCUT_ICON_NAME
  !define SHORTCUT_ICON_NAME "pcwaechter-brand.ico"
!endif

!ifndef ROOT_LAUNCHER_FILE
  !define ROOT_LAUNCHER_FILE "PCWaechter.cmd"
!endif

!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\..\apps\desktop\bin\Release\net10.0-windows\win-x64\publish"
!endif

!ifndef OUTPUT_FILE
  !define OUTPUT_FILE "..\..\release\artifacts\PCWaechter_offline_installer_${APP_VERSION_CODE}.exe"
!endif

Name "${APP_TITLE} ${APP_VERSION}"
OutFile "${OUTPUT_FILE}"
Caption "${APP_TITLE} ${APP_VERSION}"
InstallDir "$PROGRAMFILES64\PCWächter\versions\${APP_VERSION_CODE}"
RequestExecutionLevel admin
Icon "${APP_ICON}"
UninstallIcon "${APP_ICON}"
ShowInstDetails nevershow
ShowUninstDetails nevershow
BrandingText "${APP_TITLE} ${APP_VERSION}"

VIProductVersion "${APP_VERSION_4}"
VIAddVersionKey /LANG=1031 "ProductName" "${APP_TITLE}"
VIAddVersionKey /LANG=1031 "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey /LANG=1031 "FileDescription" "Installationsprogramm für ${APP_TITLE}"
VIAddVersionKey /LANG=1031 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1031 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "ProductName" "${APP_TITLE}"
VIAddVersionKey /LANG=1033 "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey /LANG=1033 "FileDescription" "Installer for ${APP_TITLE}"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "ProductVersion" "${APP_VERSION}"

!define MUI_ABORTWARNING
!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!define MUI_BRANDINGTEXT "${APP_TITLE} ${APP_VERSION}"
!define MUI_WELCOMEPAGE_TITLE "Willkommen beim ${APP_TITLE} Setup"
!define MUI_WELCOMEPAGE_TEXT "Dieses Setup installiert oder aktualisiert ${APP_TITLE}.$\r$\n$\r$\nFalls Sie ueber den Live-Installer gestartet haben, wurde bereits die neueste Version geladen und wird jetzt installiert."
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "${APP_HEADER_IMAGE}"
!define MUI_HEADERIMAGE_RIGHT

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "${APP_TITLE} starten"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "German"
!insertmacro MUI_LANGUAGE "English"

Section "PC Wächter (erforderlich)" SEC_MAIN
  SectionIn RO
  SetShellVarContext all

  StrCmp $CleanAppDirFlag "1" 0 +2
    Call CleanupInstallDirectory

  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*"

  ; Updater muss stabil unter InstallRoot\updater liegen (nicht in current/versioned App-Ordnern).
  CreateDirectory "$PROGRAMFILES64\PCWächter\updater"
  IfFileExists "$INSTDIR\native-updater-worker\PCWaechter.Updater.exe" 0 +3
    CopyFiles /SILENT "$INSTDIR\native-updater-worker\PCWaechter.Updater.exe" "$PROGRAMFILES64\PCWächter\updater\PCWaechter.Updater.exe"
    Goto updater_copy_done
  IfFileExists "$INSTDIR\updater\PCWaechter.Updater.exe" 0 updater_copy_failed
    CopyFiles /SILENT "$INSTDIR\updater\PCWaechter.Updater.exe" "$PROGRAMFILES64\PCWächter\updater\PCWaechter.Updater.exe"
    Goto updater_copy_done

updater_copy_failed:
  MessageBox MB_ICONEXCLAMATION|MB_OK "Die Updater-Datei konnte nicht gefunden werden.$\r$\n$\r$\nErwartet: $INSTDIR\native-updater-worker\PCWaechter.Updater.exe"

updater_copy_done:

  Call CleanupOldVersionDirectories

  StrCpy $5 "$PROGRAMFILES64\PCWächter"
  StrCpy $6 "$5\${ROOT_LAUNCHER_FILE}"
  CreateDirectory "$5"
  FileOpen $7 $6 w
  FileWrite $7 "@echo off$\r$\n"
  FileWrite $7 "set ROOT=%~dp0$\r$\n"
  FileWrite $7 "set TARGET=%ROOT%current$\r$\n"
  FileWrite $7 "if not exist $\"%TARGET%\\${APP_EXE}$\" ($\r$\n"
  FileWrite $7 "  echo Aktuelle Version nicht gefunden.$\r$\n"
  FileWrite $7 "  exit /b 1$\r$\n"
  FileWrite $7 ")$\r$\n"
  FileWrite $7 "start $\"$\" $\"%TARGET%\\${APP_EXE}$\" %*$\r$\n"
  FileWrite $7 "exit /b 0$\r$\n"
  FileClose $7

  nsExec::ExecToStack '"$SYSDIR\cmd.exe" /c if exist "$PROGRAMFILES64\PCWächter\current" rmdir "$PROGRAMFILES64\PCWächter\current"'
  Pop $0
  Pop $1
  nsExec::ExecToStack '"$SYSDIR\cmd.exe" /c mklink /J "$PROGRAMFILES64\PCWächter\current" "$INSTDIR"'
  Pop $0
  Pop $1
  StrCmp $0 "0" +2
    MessageBox MB_ICONEXCLAMATION|MB_OK "Die current-Junction konnte nicht erstellt werden.$\r$\n$\r$\nFehler: $1"

  WriteRegStr HKLM "Software\PCWaechter" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "Software\PCWaechter" "InstallRoot" "$PROGRAMFILES64\PCWächter"
  WriteRegStr HKLM "Software\PCWaechter" "LauncherPath" "$6"
  WriteRegDWORD HKLM "Software\PCWaechter" "AutomaticAppUpdatesDefault" 1

  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\PCWaechter" "DisplayName" "${APP_TITLE}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\PCWaechter" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\PCWaechter" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\PCWaechter" "DisplayIcon" "$6,0"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\PCWaechter" "InstallLocation" "$PROGRAMFILES64\PCWächter"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\PCWaechter" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\PCWaechter" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\PCWaechter" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\PCWaechter" "EstimatedSize" $0

  CreateDirectory "$SMPROGRAMS\PC Wächter"
  CreateShortcut "$SMPROGRAMS\PC Wächter\PC Wächter.lnk" "$6" "" "$INSTDIR\${APP_EXE}" 0


  CreateDirectory "$COMMONAPPDATA\PCWächter\logs\task"
  StrCpy $2 "$TEMP\PCWaechter-generated-task.xml"

  IfFileExists "$PROGRAMFILES64\PCWächter\updater\PCWaechter.Updater.exe" +2 0
    Goto updater_task_missing

  FileOpen $3 $2 w
  FileWrite $3 "<?xml version=$\"1.0$\" encoding=$\"UTF-8$\"?>$\r$\n"
  FileWrite $3 "<Task version=$\"1.4$\" xmlns=$\"http://schemas.microsoft.com/windows/2004/02/mit/task$\">$\r$\n"
  FileWrite $3 "  <RegistrationInfo>$\r$\n"
  FileWrite $3 "    <Description>PCWaechter geplanter Updater</Description>$\r$\n"
  FileWrite $3 "  </RegistrationInfo>$\r$\n"
  FileWrite $3 "  <Triggers>$\r$\n"
  FileWrite $3 "    <BootTrigger>$\r$\n"
  FileWrite $3 "      <Enabled>true</Enabled>$\r$\n"
  FileWrite $3 "      <Delay>PT5M</Delay>$\r$\n"
  FileWrite $3 "    </BootTrigger>$\r$\n"
  FileWrite $3 "    <TimeTrigger>$\r$\n"
  FileWrite $3 "      <StartBoundary>2026-01-01T00:00:00</StartBoundary>$\r$\n"
  FileWrite $3 "      <Enabled>true</Enabled>$\r$\n"
  FileWrite $3 "      <Repetition>$\r$\n"
  FileWrite $3 "        <Interval>PT20H</Interval>$\r$\n"
  FileWrite $3 "        <StopAtDurationEnd>false</StopAtDurationEnd>$\r$\n"
  FileWrite $3 "      </Repetition>$\r$\n"
  FileWrite $3 "    </TimeTrigger>$\r$\n"
  FileWrite $3 "  </Triggers>$\r$\n"
  FileWrite $3 "  <Principals>$\r$\n"
  FileWrite $3 "    <Principal id=$\"Author$\">$\r$\n"
  FileWrite $3 "      <UserId>S-1-5-18</UserId>$\r$\n"
  FileWrite $3 "      <LogonType>ServiceAccount</LogonType>$\r$\n"
  FileWrite $3 "      <RunLevel>HighestAvailable</RunLevel>$\r$\n"
  FileWrite $3 "    </Principal>$\r$\n"
  FileWrite $3 "  </Principals>$\r$\n"
  FileWrite $3 "  <Settings>$\r$\n"
  FileWrite $3 "    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>$\r$\n"
  FileWrite $3 "    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>$\r$\n"
  FileWrite $3 "    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>$\r$\n"
  FileWrite $3 "    <AllowHardTerminate>true</AllowHardTerminate>$\r$\n"
  FileWrite $3 "    <StartWhenAvailable>true</StartWhenAvailable>$\r$\n"
  FileWrite $3 "    <RunOnlyIfNetworkAvailable>true</RunOnlyIfNetworkAvailable>$\r$\n"
  FileWrite $3 "    <AllowStartOnDemand>true</AllowStartOnDemand>$\r$\n"
  FileWrite $3 "    <Enabled>true</Enabled>$\r$\n"
  FileWrite $3 "    <Hidden>true</Hidden>$\r$\n"
  FileWrite $3 "    <RunOnlyIfIdle>false</RunOnlyIfIdle>$\r$\n"
  FileWrite $3 "    <WakeToRun>false</WakeToRun>$\r$\n"
  FileWrite $3 "    <ExecutionTimeLimit>PT2H</ExecutionTimeLimit>$\r$\n"
  FileWrite $3 "    <Priority>7</Priority>$\r$\n"
  FileWrite $3 "  </Settings>$\r$\n"
  FileWrite $3 "  <Actions Context=$\"Author$\">$\r$\n"
  FileWrite $3 "    <Exec>$\r$\n"
  FileWrite $3 "      <Command>$PROGRAMFILES64\PCWächter\updater\PCWaechter.Updater.exe</Command>$\r$\n"
  FileWrite $3 "      <Arguments>--update</Arguments>$\r$\n"
  FileWrite $3 "      <WorkingDirectory>$PROGRAMFILES64\PCWächter\updater</WorkingDirectory>$\r$\n"
  FileWrite $3 "    </Exec>$\r$\n"
  FileWrite $3 "  </Actions>$\r$\n"
  FileWrite $3 "</Task>$\r$\n"
  FileClose $3

  nsExec::ExecToStack '"$SYSDIR\schtasks.exe" /Create /TN "${UPDATE_TASK_NAME}" /XML "$2" /F'
  Pop $0
  Pop $1
  FileOpen $3 "$COMMONAPPDATA\PCWächter\logs\task\schtasks-create.out.txt" w
  FileWrite $3 "$1$\r$\n"
  FileClose $3
  StrCmp $0 "0" +2
    MessageBox MB_ICONEXCLAMATION|MB_OK "Die geplante Aufgabe für automatische Updates konnte nicht erstellt werden.$\r$\n$\r$\nFehler: $1"
  Goto task_setup_done

updater_task_missing:
  MessageBox MB_ICONEXCLAMATION|MB_OK "Die geplante Aufgabe für automatische Updates konnte nicht erstellt werden.$\r$\n$\r$\nFehler: Updater nicht gefunden unter:$\r$\n$PROGRAMFILES64\PCWächter\updater\PCWaechter.Updater.exe"

task_setup_done:

  nsExec::ExecToStack '"$SYSDIR\schtasks.exe" /Query /TN "${UPDATE_TASK_NAME}" /V /FO LIST'
  Pop $0
  Pop $1
  FileOpen $3 "$COMMONAPPDATA\PCWächter\logs\task\schtasks-query.txt" w
  FileWrite $3 "$1$\r$\n"
  FileClose $3

  nsExec::ExecToStack '"$SYSDIR\schtasks.exe" /Query /TN "${UPDATE_TASK_NAME}" /XML'
  Pop $0
  Pop $1
  FileOpen $3 "$COMMONAPPDATA\PCWächter\logs\task\schtasks-query.xml" w
  FileWrite $3 "$1$\r$\n"
  FileClose $3

  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section /o "Desktop-Symbol erstellen" SEC_DESKTOP
  SetShellVarContext all
  CreateShortcut "$DESKTOP\PC Wächter.lnk" "$PROGRAMFILES64\PCWächter\${ROOT_LAUNCHER_FILE}" "" "$INSTDIR\${APP_EXE}" 0
SectionEnd

Section /o "Schnellstart-Symbol erstellen" SEC_QUICKLAUNCH
  SetShellVarContext current
  CreateDirectory "$QUICKLAUNCH"
  CreateShortcut "$QUICKLAUNCH\PC Wächter.lnk" "$PROGRAMFILES64\PCWächter\${ROOT_LAUNCHER_FILE}" "" "$INSTDIR\${APP_EXE}" 0
SectionEnd

Section "Bei Systemstart starten" SEC_AUTOSTART
  SetShellVarContext current
  CreateShortcut "$SMSTARTUP\PC Wächter.lnk" "$PROGRAMFILES64\PCWächter\${ROOT_LAUNCHER_FILE}" "--start-in-tray" "$INSTDIR\${APP_EXE}" 0
SectionEnd

Section "Uninstall"
  Call un.KillRunningPcWaechterProcesses

  SetShellVarContext current
  Delete "$SMSTARTUP\PC Wächter.lnk"
  Delete "$QUICKLAUNCH\PC Wächter.lnk"

  SetShellVarContext all
  Delete "$DESKTOP\PC Wächter.lnk"
  Delete "$SMPROGRAMS\PC Wächter\PC Wächter.lnk"
  RMDir "$SMPROGRAMS\PC Wächter"

  nsExec::ExecToStack '"$SYSDIR\schtasks.exe" /Delete /TN "${UPDATE_TASK_NAME}" /F'
  Pop $0
  Pop $1

  ; Gesamtes Installationsverzeichnis inkl. aller Versionsunterordner entfernen.
  RMDir /r "$PROGRAMFILES64\PCWächter"
  IfFileExists "$PROGRAMFILES64\PCWächter\*.*" 0 +4
    nsExec::ExecToStack '"$SYSDIR\cmd.exe" /c rd /s /q "$PROGRAMFILES64\PCWächter"'
    Pop $0
    Pop $1

  StrCmp $RemoveUserDataFlag "1" purge_user_data ask_purge_user_data
ask_purge_user_data:
  IfSilent skip_purge_user_data
  MessageBox MB_ICONQUESTION|MB_YESNO "Sollen auch Benutzerdaten (Einstellungen, Logs und Update-Cache) gelöscht werden?" IDYES purge_user_data IDNO skip_purge_user_data

purge_user_data:
  RMDir /r "$LOCALAPPDATA\PCWaechter"
  RMDir /r "$LOCALAPPDATA\PCWächter"

skip_purge_user_data:

  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\PCWaechter"
  DeleteRegKey HKLM "Software\PCWaechter"
SectionEnd
