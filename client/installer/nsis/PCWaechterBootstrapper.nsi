Unicode True

!ifndef APP_BRANDING
  !define APP_BRANDING "PC Wächter"
!endif

!ifndef APP_ICON
  !define APP_ICON "..\..\..\shared\assets\branding\prod\pcwaechter-brand.ico"
!endif

!ifndef OUTPUT_FILE
  !define OUTPUT_FILE "..\..\release\artifacts\bootstrapper\PCWaechter_live_installer.exe"
!endif

!ifndef SETUP_URL
  !define SETUP_URL "https://github.com/cOOnStar/pcwaechter-public-release/releases/latest/download/PCWaechter_offline_installer.exe"
!endif

!ifndef SETUP_FILE
  !define SETUP_FILE "PCWaechter_offline_installer.exe"
!endif

!ifndef RELEASE_VERSION
  !define RELEASE_VERSION "0.0.0"
!endif

Name "${APP_BRANDING} Installer"
OutFile "${OUTPUT_FILE}"
Icon "${APP_ICON}"
RequestExecutionLevel admin
ManifestDPIAware true
SilentInstall normal
ShowInstDetails nevershow
Caption "${APP_BRANDING} Installer"
AutoCloseWindow true
BrandingText "${APP_BRANDING} ${RELEASE_VERSION}"

Section "Bootstrapper"
  HideWindow
  StrCpy $0 "$LOCALAPPDATA\PCWächter\InstallerCache\${RELEASE_VERSION}"
  StrCpy $1 "$0\${SETUP_FILE}"
  CreateDirectory "$0"

  IfFileExists "$1" 0 download_setup
    HideWindow
    Exec '"$1"'
    IfErrors 0 +3
      MessageBox MB_ICONSTOP|MB_OK "Setup konnte nicht gestartet werden.$\r$\nDatei: $1"
      Abort
    Quit

download_setup:
  inetc::get /caption "${APP_BRANDING} Installer ${RELEASE_VERSION}" /banner "Lade die neueste Version von ${APP_BRANDING} herunter.$\r$\nNach dem Download startet das Setup automatisch." "${SETUP_URL}" "$1"
  Pop $2

  StrCmp $2 "OK" +3
    MessageBox MB_ICONSTOP|MB_OK "Download fehlgeschlagen: $2"
    Abort

  Exec '"$1"'
  IfErrors 0 +3
    MessageBox MB_ICONSTOP|MB_OK "Setup konnte nicht gestartet werden.$\r$\nDatei: $1"
    Abort
  Quit
SectionEnd
