# Audit A) Project Scan

## Vorprüfung (unterbrochene Vorarbeit)
- `docs/audit/` war zu Beginn **nicht vorhanden** (Status-Check per `Test-Path docs/audit`).
- Es wurden keine früheren Audit-Outputs mit den Zielnamen (`00-project-map.md` ... `99-executive-summary.md`) gefunden.
- Vorhandene relevante Vorarbeit: `scripts/smoke.sh`, `scripts/smoke.ps1`, bestehende Architektur-/API-/DB-Dokumente unter `docs/`.

## Monorepo-Struktur (Ist, gekürzt)

### Top-Level
```text
├─ .claude
│  ├─ settings.json
│  └─ settings.local.json
├─ .github
│  └─ workflows
├─ client
│  ├─ api
│  ├─ gui
│  ├─ installer
│  ├─ runlogs
│  ├─ service
│  ├─ Directory.Build.props
│  └─ pcwaechter-client.sln
├─ docs
│  ├─ 07_release
│  ├─ audit
│  ├─ releases
│  ├─ 00-overview.md
│  ├─ 01-architecture.md
│  ├─ 02-keycloak.md
│  ├─ 03-env.md
│  ├─ 04-docker.md
│  ├─ 05-api.md
│  ├─ 06-db.md
│  ├─ 07-frontends.md
│  ├─ 08-release-updates.md
│  ├─ 09-backend-settings.md
│  ├─ console.md
│  ├─ deploy.md
│  ├─ deploy-checklist.md
│  ├─ keycloak-setup.md
│  ├─ README.md
│  ├─ services.md
│  ├─ settings.md
│  ├─ smoke-tests.md
│  └─ TOC.md
├─ logos
│  ├─ dev
│  └─ prod
├─ release
│  ├─ artifacts
│  └─ installer-manifest.json
├─ scripts
│  ├─ db_schema_dump.md
│  ├─ generate_docs.py
│  ├─ smoke.ps1
│  └─ smoke.sh
├─ server
│  ├─ api
│  ├─ console
│  ├─ home
│  ├─ infra
│  ├─ keycloak
│  ├─ openapi
│  ├─ zammad
│  └─ deploy.sh
├─ shared
│  ├─ assets
│  ├─ contracts
│  └─ libs
├─ src
│  └─ components
├─ Vorlage
│  ├─ docs
│  ├─ scripts
│  ├─ server
│  ├─ .env.example
│  └─ README.md
├─ .env
├─ .env.example
├─ .mcp.json
├─ Makefile
├─ package.json
├─ package-lock.json
└─ README.md
```

### server/
```text
├─ api
│  ├─ alembic
│  │  ├─ versions
│  │  ├─ env.py
│  │  └─ script.py.mako
│  ├─ app
│  │  ├─ routers
│  │  ├─ __init__.py
│  │  ├─ db.py
│  │  ├─ main.py
│  │  ├─ models.py
│  │  ├─ schemas.py
│  │  ├─ security.py
│  │  ├─ security_jwt.py
│  │  └─ settings.py
│  ├─ .dockerignore
│  ├─ .env.example
│  ├─ alembic.ini
│  ├─ Dockerfile
│  ├─ README.md
│  └─ requirements.txt
├─ console
│  ├─ public
│  │  └─ openapi.json
│  ├─ scripts
│  │  └─ sync-openapi.ps1
│  ├─ src
│  │  ├─ app
│  │  ├─ assets
│  │  ├─ components
│  │  ├─ lib
│  │  ├─ App.tsx
│  │  ├─ index.css
│  │  └─ main.tsx
│  ├─ .dockerignore
│  ├─ .env
│  ├─ .gitignore
│  ├─ Dockerfile
│  ├─ eslint.config.js
│  ├─ index.html
│  ├─ nginx.conf
│  ├─ package.json
│  ├─ package-lock.json
│  ├─ README.md
│  ├─ tsconfig.app.json
│  ├─ tsconfig.json
│  ├─ tsconfig.node.json
│  └─ vite.config.ts
├─ home
│  ├─ public
│  │  └─ .gitkeep
│  ├─ src
│  │  ├─ app
│  │  ├─ components
│  │  ├─ lib
│  │  └─ auth.ts
│  ├─ .env.local
│  ├─ .gitignore
│  ├─ Dockerfile
│  ├─ next.config.ts
│  ├─ next-env.d.ts
│  ├─ package.json
│  ├─ package-lock.json
│  ├─ postcss.config.mjs
│  └─ tsconfig.json
├─ infra
│  ├─ caddy
│  │  ├─ Caddyfile
│  │  └─ docker-compose.yml
│  └─ compose
│     └─ docker-compose.yml
├─ keycloak
│  ├─ keycloak-gateway
│  │  └─ default.conf
│  ├─ keycloak-theme
│  │  ├─ custom-v2
│  │  ├─ keycloak.v2
│  │  ├─ login
│  │  ├─ minimal-debug
│  │  ├─ pcwaechter
│  │  ├─ pcwaechter-v1
│  │  ├─ theme2
│  │  ├─ theme3
│  │  ├─ theme4
│  │  └─ keycloak.v2.zip
│  ├─ providers
│  │  └─ license-key-authenticator
│  ├─ copy-user-to-realm.ps1
│  ├─ Dockerfile
│  ├─ provision-keycloak-local.ps1
│  ├─ provision-realm.sh
│  ├─ README.md
│  └─ setup-clients.sh
├─ openapi
│  ├─ openapi.json
│  ├─ README.md
│  └─ update-openapi.ps1
├─ zammad
│  ├─ docker-compose.yml
│  └─ setup.sh
└─ deploy.sh
```

### client/
```text
├─ api
│  ├─ AgentIdentity.cs
│  ├─ ApiClient.cs
│  ├─ pcwaechter-agent.csproj
│  ├─ Program.cs
│  └─ README.md
├─ gui
│  ├─ Converters
│  │  ├─ BoolToCheckmarkConverter.cs
│  │  ├─ BoolToVisibilityConverter.cs
│  │  ├─ LessThanConverter.cs
│  │  ├─ NullOrEmptyToVisibilityConverter.cs
│  │  ├─ SeverityToBrushConverter.cs
│  │  └─ StringToVisibilityConverter.cs
│  ├─ Resources
│  │  └─ tray.ico
│  ├─ Services
│  │  ├─ AppUiStateStore.cs
│  │  ├─ DemoReportFactory.cs
│  │  ├─ DesktopActionRunner.cs
│  │  ├─ DesktopRuntimeOptions.cs
│  │  ├─ IpcClientService.cs
│  │  ├─ IpcClientService.Protocol.cs
│  │  ├─ KeycloakAuthService.cs
│  │  ├─ LicenseService.cs
│  │  ├─ ReportStore.cs
│  │  ├─ TrayIconService.cs
│  │  └─ UpdaterIntegrationService.cs
│  ├─ Themes
│  │  ├─ Theme.Fluent.Dark.xaml
│  │  └─ Theme.xaml
│  ├─ ViewModels
│  │  ├─ AccountViewModel.cs
│  │  ├─ AsyncRelayCommand.cs
│  │  ├─ DashboardViewModel.cs
│  │  ├─ FindingCardViewModel.cs
│  │  ├─ FluentHostPageViewModel.cs
│  │  ├─ HelpViewModel.cs
│  │  ├─ HistoryViewModel.cs
│  │  ├─ MainViewModel.cs
│  │  ├─ NavItemViewModel.cs
│  │  ├─ NetworkViewModel.cs
│  │  ├─ NotificationItemViewModel.cs
│  │  ├─ ObservableObject.cs
│  │  ├─ OptionsViewModel.cs
│  │  ├─ PageViewModelBase.cs
│  │  ├─ RelayCommand.cs
│  │  ├─ RemediationQueueItemViewModel.cs
│  │  ├─ ReportPageViewModelBase.cs
│  │  ├─ SecurityViewModel.cs
│  │  ├─ StorageViewModel.cs
│  │  ├─ WindowsUpdatesViewModel.cs
│  │  └─ WindowsViewModel.cs
│  ├─ Views
│  │  ├─ Controls
│  │  ├─ Pages
│  │  └─ Windows
│  ├─ App.xaml
│  ├─ App.xaml.cs
│  ├─ appsettings.json
│  ├─ GlobalTypeAliases.cs
│  ├─ MainWindow.xaml
│  ├─ MainWindow.xaml.cs
│  ├─ PCWächter.csproj
│  ├─ PCWachter.Desktop.csproj
│  └─ README.md
├─ installer
│  ├─ bootstrapper
│  │  ├─ InstallerBootstrapper.csproj
│  │  ├─ installer-manifest.sample.json
│  │  ├─ Program.cs
│  │  └─ README.md
│  ├─ inno
│  │  └─ PCWaechterSetup.iss
│  ├─ manifests
│  │  └─ installer-manifest.json
│  └─ nsis
│     ├─ PCWaechterBootstrapper.nsi
│     └─ PCWaechterSetup.nsi
├─ runlogs
│  ├─ screenshots
│  │  ├─ dashboard-no-service.png
│  │  ├─ dashboard-no-service-14s.png
│  │  ├─ dashboard-no-service-18s.png
│  │  ├─ dashboard-no-service-30s.png
│  │  ├─ dashboard-no-service-after-fix.png
│  │  ├─ dashboard-no-service-fastconnect.png
│  │  ├─ dashboard-no-service-final.png
│  │  ├─ dashboard-no-service-footer-retry.png
│  │  ├─ dashboard-no-service-footer-retry-final.png
│  │  ├─ dashboard-no-service-timeoutfix.png
│  │  ├─ dashboard-no-service-timeoutfix2.png
│  │  ├─ dashboard-with-service.png
│  │  ├─ dashboard-with-service-fastconnect2.png
│  │  ├─ dashboard-with-service-final.png
│  │  ├─ dashboard-with-service-timeoutfix.png
│  │  ├─ desktop-no-service.png
│  │  ├─ desktop-with-service.png
│  │  ├─ help-no-service-skeleton.png
│  │  └─ options-no-service-skeleton.png
│  ├─ ui-skeleton-check-20260301-034550
│  │  ├─ desktop-no-service.png
│  │  ├─ desktop-with-service.png
│  │  ├─ desktop-with-service-25s.png
│  │  └─ desktop-with-service-ready.png
│  ├─ desktop-20260228-154730.err.log
│  ├─ desktop-20260228-154730.out.log
│  ├─ desktop-20260228-154936.err.log
│  ├─ desktop-20260228-154936.out.log
│  ├─ desktop-full-20260301-013656.png
│  ├─ desktop-smoke-current.png
│  ├─ desktop-smoke-dashboard.png
│  ├─ desktop-smoke-history.png
│  ├─ desktop-ui-state.backup.json
│  ├─ desktop-ui-state.nav.backup.after.json
│  ├─ desktop-ui-state.nav.backup.after2.json
│  ├─ desktop-ui-state.nav.backup.json
│  ├─ nav-after.png
│  ├─ nav-before.png
│  ├─ service-20260228-154730.err.log
│  ├─ service-20260228-154730.out.log
│  ├─ service-20260301-001435.err.log
│  ├─ service-20260301-001435.out.log
│  └─ ui-client-20260301-013149.png
├─ service
│  ├─ Interop
│  │  └─ IpcServerHostedService.cs
│  ├─ Properties
│  │  └─ launchSettings.json
│  ├─ Remediations
│  │  ├─ AppsUpdateSelectedRemediation.cs
│  │  ├─ DefenderEnableRealtimeRemediation.cs
│  │  ├─ DefenderUpdateSignaturesRemediation.cs
│  │  ├─ FirewallEnableAllRemediation.cs
│  │  ├─ NetworkDisableProxyRemediation.cs
│  │  ├─ NetworkFlushDnsRemediation.cs
│  │  ├─ NetworkResetAdaptersRemediation.cs
│  │  ├─ StartupDisableRemediation.cs
│  │  ├─ StartupUndoRemediation.cs
│  │  ├─ WindowsInstallAllUpdatesRemediation.cs
│  │  ├─ WindowsInstallOptionalUpdatesRemediation.cs
│  │  └─ WindowsInstallSecurityUpdatesRemediation.cs
│  ├─ Rules
│  │  ├─ AppUpdatesRule.cs
│  │  ├─ BitLockerRule.cs
│  │  ├─ DefenderRule.cs
│  │  ├─ EventLogRule.cs
│  │  ├─ FirewallRule.cs
│  │  ├─ NetworkDiagnosticsRule.cs
│  │  ├─ PendingRebootRule.cs
│  │  ├─ PerformanceWatchRule.cs
│  │  ├─ RuleHelpers.cs
│  │  ├─ StartupRule.cs
│  │  ├─ StorageRule.cs
│  │  └─ WindowsUpdatesRule.cs
│  ├─ Runtime
│  │  ├─ ActionIds.cs
│  │  ├─ DeviceContextProvider.cs
│  │  ├─ FindingFingerprint.cs
│  │  ├─ HealthScoreCalculator.cs
│  │  ├─ JsonStateStore.cs
│  │  ├─ PowerShellRunner.cs
│  │  ├─ PriorityCalculator.cs
│  │  ├─ ProcessRunner.cs
│  │  ├─ RuntimePaths.cs
│  │  ├─ ScanCoordinator.cs
│  │  ├─ SensorPayloads.cs
│  │  └─ UxIntelligenceOptions.cs
│  ├─ Sensors
│  │  ├─ AppUpdatesSensor.cs
│  │  ├─ BitLockerSensor.cs
│  │  ├─ DefenderSensor.cs
│  │  ├─ EventLogSensor.cs
│  │  ├─ FirewallSensor.cs
│  │  ├─ NetworkDiagnosticsSensor.cs
│  │  ├─ PendingRebootSensor.cs
│  │  ├─ PerformanceWatchSensor.cs
│  │  ├─ SecurityHardeningSensor.cs
│  │  ├─ StartupAppsSensor.cs
│  │  ├─ StorageSensor.cs
│  │  └─ WindowsUpdatesSensor.cs
│  ├─ AgentService.csproj
│  ├─ appsettings.Development.json
│  ├─ appsettings.json
│  ├─ Program.cs
│  ├─ README.md
│  └─ Worker.cs
├─ Directory.Build.props
└─ pcwaechter-client.sln
```

### shared/
```text
├─ assets
│  └─ branding
│     ├─ dev
│     └─ prod
├─ contracts
│  ├─ IpcModels.cs
│  ├─ Models.cs
│  └─ PCWachter.Contracts.csproj
└─ libs
   └─ PCWachter.Core
      ├─ Interfaces.cs
      └─ PCWachter.Core.csproj
```

### docs/
```text
├─ 07_release
│  ├─ 00_release-process.md
│  └─ 01_versioning.md
├─ audit
│  └─ _generated
│     ├─ api_endpoints_inventory.csv
│     ├─ db_columns.csv
│     ├─ db_constraints.csv
│     ├─ db_relationships.csv
│     ├─ db_tables.csv
│     ├─ dead_calls.csv
│     ├─ dead_endpoints.csv
│     ├─ endpoint_frontend_usage.csv
│     ├─ frontend_api_callgraph.csv
│     ├─ frontend_service_calls.csv
│     ├─ migrations_inventory.csv
│     ├─ page_matrix.csv
│     ├─ page_service_usage.csv
│     └─ router_dependency_matrix.csv
├─ releases
│  ├─ api
│  │  ├─ README.md
│  │  └─ v1.md
│  ├─ console
│  │  ├─ README.md
│  │  └─ v1.md
│  ├─ gui
│  │  ├─ README.md
│  │  ├─ v0.0.1.md
│  │  ├─ v0.0.31.md
│  │  ├─ v0.0.32.md
│  │  ├─ v0.0.33.md
│  │  ├─ v0.0.34.md
│  │  ├─ v0.0.35.md
│  │  ├─ v0.0.36.md
│  │  ├─ v0.0.37.md
│  │  ├─ v0.0.38.md
│  │  ├─ v0.0.39.md
│  │  ├─ v0.0.40.md
│  │  ├─ v0.0.41.md
│  │  ├─ v0.0.42.md
│  │  ├─ v0.0.43.md
│  │  ├─ v0.0.44.md
│  │  ├─ v0.0.45.md
│  │  ├─ v0.0.48.md
│  │  ├─ v0.0.49.md
│  │  ├─ v0.0.50.md
│  │  ├─ v0.0.51.md
│  │  ├─ v0.0.52.md
│  │  ├─ v0.0.53.md
│  │  ├─ v0.0.54.md
│  │  ├─ v0.0.55.md
│  │  ├─ v0.0.56.md
│  │  ├─ v0.0.57.md
│  │  ├─ v0.0.58.md
│  │  ├─ v0.0.59.md
│  │  ├─ v0.0.60.md
│  │  ├─ v0.0.61.md
│  │  ├─ v0.0.62.md
│  │  └─ v0.0.63.md
│  ├─ service
│  │  ├─ README.md
│  │  └─ v0.0.1.md
│  ├─ updater
│  │  ├─ README.md
│  │  ├─ v0.0.66.md
│  │  ├─ v0.0.67.md
│  │  ├─ v0.0.68.md
│  │  ├─ v0.0.69.md
│  │  ├─ v0.0.70.md
│  │  ├─ v0.0.71.md
│  │  └─ v0.0.72.md
│  ├─ webseite
│  │  └─ README.md
│  └─ README.md
├─ 00-overview.md
├─ 01-architecture.md
├─ 02-keycloak.md
├─ 03-env.md
├─ 04-docker.md
├─ 05-api.md
├─ 06-db.md
├─ 07-frontends.md
├─ 08-release-updates.md
├─ 09-backend-settings.md
├─ console.md
├─ deploy.md
├─ deploy-checklist.md
├─ keycloak-setup.md
├─ README.md
├─ services.md
├─ settings.md
├─ smoke-tests.md
└─ TOC.md
```

## Build/Deploy-Artefakte (gefunden)

| Kategorie | Artefakte | Nachweis |
|---|---|---|
| Compose | server/infra/compose/docker-compose.yml, server/infra/caddy/docker-compose.yml, server/zammad/docker-compose.yml | Datei-Scan (Get-ChildItem/g --files) |
| Deploy Script | server/deploy.sh | Datei vorhanden |
| CI Workflow | .github/workflows/build.yml | Datei vorhanden |
| Smoke Scripts | scripts/smoke.sh, scripts/smoke.ps1 | Datei vorhanden |
| Env Files | .env, .env.example, server/api/.env.example, server/console/.env, server/home/.env.local | Datei-Scan |
| Build Helper | Makefile | Datei vorhanden |

### Vollständige Artefaktliste (prüfbar)
```text
.github/workflows/build.yml
server/infra/compose/docker-compose.yml
server/infra/caddy/docker-compose.yml
server/zammad/docker-compose.yml
server/deploy.sh
scripts/smoke.sh
scripts/smoke.ps1
.env
.env.example
server/api/.env.example
server/console/.env
server/home/.env.local
Makefile
```
