# Deployment Guide - PC-Waechter Kundenportal

> **Version:** 2.1
> **Letzte Aktualisierung:** 2026-03-06

Anleitung zum Deployment des Kundenportals auf dem Produktionsserver.

## 🎯 Deployment-Übersicht

```
GitHub Repository → GitHub Actions → Produktionsserver
                    (Build)          (home.pcwächter.de)
```

## 📋 Voraussetzungen

### 1. Keycloak Setup
- [ ] Keycloak Server läuft unter `https://login.xn--pcwchter-2za.de`
- [ ] Realm `pcwaechter-prod` ist erstellt
- [ ] Client `home-web` ist konfiguriert (siehe KEYCLOAK_SETUP.md)

### 2. Server Setup
- [ ] Webserver (nginx/Apache) konfiguriert
- [ ] Domain `home.xn--pcwchter-2za.de` zeigt auf Server
- [ ] SSL-Zertifikat installiert
- [ ] Node.js installiert (für Build)

### 3. GitHub Setup
- [ ] Repository erstellt
- [ ] GitHub Actions aktiviert
- [ ] Secrets konfiguriert

## 🔐 GitHub Secrets konfigurieren

Gehen Sie zu: **Repository → Settings → Secrets and variables → Actions**

Fügen Sie folgende Secrets hinzu:

```
SERVER_HOST=<IP oder Hostname des Servers>
SERVER_USER=<SSH Username>
SERVER_SSH_KEY=<Privater SSH Key>
DEPLOY_PATH=/var/www/home.pcwaechter.de
```

Optional für Build-Zeit Environment Variables:
```
VITE_KEYCLOAK_URL=https://login.xn--pcwchter-2za.de
VITE_KEYCLOAK_REALM=pcwaechter-prod
VITE_KEYCLOAK_CLIENT_ID=home-web
```

## 🚀 GitHub Actions Workflow

Erstellen Sie `.github/workflows/deploy.yml`:

```yaml
name: Build and Deploy

on:
  push:
    branches: [ main ]
  workflow_dispatch:

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4
    
    - name: Setup Node.js
      uses: actions/setup-node@v4
      with:
        node-version: '20'
    
    - name: Setup pnpm
      uses: pnpm/action-setup@v4
      with:
        version: 8
    
    - name: Install dependencies
      run: pnpm install --frozen-lockfile
    
    - name: Build application
      env:
        VITE_KEYCLOAK_URL: ${{ secrets.VITE_KEYCLOAK_URL }}
        VITE_KEYCLOAK_REALM: ${{ secrets.VITE_KEYCLOAK_REALM }}
        VITE_KEYCLOAK_CLIENT_ID: ${{ secrets.VITE_KEYCLOAK_CLIENT_ID }}
      run: pnpm build
    
    - name: Deploy to server
      uses: appleboy/scp-action@v0.1.7
      with:
        host: ${{ secrets.SERVER_HOST }}
        username: ${{ secrets.SERVER_USER }}
        key: ${{ secrets.SERVER_SSH_KEY }}
        source: "dist/*"
        target: ${{ secrets.DEPLOY_PATH }}
        strip_components: 1
    
    - name: Restart web server
      uses: appleboy/ssh-action@v1.0.3
      with:
        host: ${{ secrets.SERVER_HOST }}
        username: ${{ secrets.SERVER_USER }}
        key: ${{ secrets.SERVER_SSH_KEY }}
        script: |
          sudo systemctl reload nginx
```

## 🌐 Nginx Konfiguration

Erstellen Sie `/etc/nginx/sites-available/home.pcwaechter.de`:

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name home.xn--pcwchter-2za.de home.pcwächter.de;
    
    # Redirect to HTTPS
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name home.xn--pcwchter-2za.de home.pcwächter.de;
    
    # SSL Configuration
    ssl_certificate /etc/letsencrypt/live/home.xn--pcwchter-2za.de/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/home.xn--pcwchter-2za.de/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    
    # Root directory
    root /var/www/home.pcwaechter.de;
    index index.html;
    
    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_types text/plain text/css text/xml text/javascript application/javascript application/json application/xml+rss;
    
    # Security headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Referrer-Policy "no-referrer-when-downgrade" always;
    
    # CSP for Keycloak integration
    add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self' https://login.xn--pcwchter-2za.de https://api.github.com; frame-ancestors 'self';" always;
    
    # SPA routing
    location / {
        try_files $uri $uri/ /index.html;
    }
    
    # Cache static assets
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
    
    # Don't cache index.html
    location = /index.html {
        add_header Cache-Control "no-cache, no-store, must-revalidate";
        expires 0;
    }
}
```

Aktivieren Sie die Konfiguration:

```bash
sudo ln -s /etc/nginx/sites-available/home.pcwaechter.de /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

## 🔒 SSL-Zertifikat (Let's Encrypt)

```bash
# Certbot installieren
sudo apt install certbot python3-certbot-nginx

# Zertifikat erstellen
sudo certbot --nginx -d home.xn--pcwchter-2za.de

# Auto-Renewal testen
sudo certbot renew --dry-run
```

## 📁 Verzeichnisstruktur auf Server

```
/var/www/home.pcwaechter.de/
├── assets/
│   ├── index-[hash].js
│   ├── index-[hash].css
│   └── ...
├── imports/
│   └── ... (SVGs, Assets)
├── index.html
├── silent-check-sso.html
└── vite.svg
```

## 🔄 Deployment-Prozess

### Automatisches Deployment

1. Code committen und pushen:
   ```bash
   git add .
   git commit -m "Update feature"
   git push origin main
   ```

2. GitHub Actions startet automatisch
3. Build wird erstellt
4. Deployment auf Server erfolgt
5. Nginx wird neu geladen

### Manuelles Deployment

Falls GitHub Actions nicht verfügbar ist:

```bash
# Lokal bauen
pnpm install
pnpm build

# Per SCP hochladen
scp -r dist/* user@server:/var/www/home.pcwaechter.de/

# Nginx neu laden
ssh user@server 'sudo systemctl reload nginx'
```

## ✅ Deployment verifizieren

### 1. Website aufrufen
```
https://home.xn--pcwchter-2za.de
```

### 2. Login testen
- Sollte zu Keycloak redirecten
- Nach Login zurück zum Portal
- User-Daten korrekt angezeigt

### 3. Browser Console prüfen
- Keine Fehler
- Keycloak initialisiert
- Token vorhanden

### 4. API Calls prüfen
- GitHub API für Downloads funktioniert
- Keycloak User Info lädt

## 🐛 Troubleshooting

### Build schlägt fehl

**Problem**: GitHub Actions Build fails

**Lösung**:
```bash
# Lokal testen
pnpm install
pnpm build

# Dependencies prüfen
pnpm list
```

### 404 bei SPA Routes

**Problem**: Reload auf `/licenses` gibt 404

**Lösung**: Nginx `try_files` korrekt konfiguriert (siehe oben)

### Keycloak Redirect Loop

**Problem**: Endless redirect zwischen Portal und Keycloak

**Lösung**:
- Redirect URIs in Keycloak Client prüfen
- `.env` Variablen prüfen
- Browser Cache leeren

### CORS Errors

**Problem**: CORS Fehler bei Keycloak Calls

**Lösung**:
- "Web Origins" in Keycloak Client setzen
- Nginx CSP Header prüfen

## 📊 Monitoring

### Logs überwachen

```bash
# Nginx Access Logs
sudo tail -f /var/log/nginx/access.log

# Nginx Error Logs
sudo tail -f /var/log/nginx/error.log

# GitHub Actions Logs
# Im GitHub Repository unter Actions Tab
```

### Uptime Monitoring

Empfohlene Tools:
- UptimeRobot
- Pingdom
- StatusCake

## 🔄 Updates & Rollback

### Update durchführen

```bash
git pull origin main
# GitHub Actions deployt automatisch
```

### Rollback

```bash
# Zu vorherigem Commit zurück
git revert HEAD
git push origin main

# Oder manuell vorherige Version deployen
git checkout <previous-commit>
pnpm build
# Deploy dist/
```

## 📚 Weitere Ressourcen

- [Nginx Dokumentation](https://nginx.org/en/docs/)
- [Let's Encrypt Docs](https://letsencrypt.org/docs/)
- [GitHub Actions Docs](https://docs.github.com/en/actions)
- [Vite Production Build](https://vitejs.dev/guide/build.html)

---

**Wichtig**: Nach jedem Deployment die Funktionalität testen!