#!/usr/bin/env python3
"""Generate PCWächter documentation from a monorepo root.

Best-effort scanner:
- ENV usage (.env*, process.env.*, import.meta.env.*)
- Next.js/Vite configs
- docker-compose yaml
- FastAPI routers: decorator scan for @router.get/post...
- Keycloak realm export JSON: clients/toggles/redirects/origins/mappers
- optional schema.sql (pg_dump --schema-only) embeds into DB doc

Usage:
  python scripts/generate_docs.py --root ./pcwaechter --out ./generated-docs --keycloak ./inputs/keycloak-realm.json --schema ./inputs/schema.sql
"""

import argparse, re, json
from collections import defaultdict
from pathlib import Path

ENV_WORD = re.compile(r'\b([A-Z][A-Z0-9_]{2,})\b')

def iter_files(root: Path, exts=None, max_size=3_000_000):
    for p in root.rglob('*'):
        if not p.is_file():
            continue
        try:
            if p.stat().st_size > max_size:
                continue
        except:
            continue
        if exts and p.suffix.lower() not in exts:
            continue
        yield p

def scan_env(root: Path):
    found = defaultdict(set)
    exts = {'.ts','.tsx','.js','.jsx','.py','.cs','.json','.yml','.yaml','.env','.md','.toml'}
    for p in iter_files(root, exts=exts):
        txt = p.read_text('utf-8', errors='ignore')
        for m in re.finditer(r'process\.env\.([A-Z0-9_]+)', txt):
            found[m.group(1)].add(str(p.relative_to(root)))
        for m in re.finditer(r'import\.meta\.env\.([A-Z0-9_]+)', txt):
            found[m.group(1)].add(str(p.relative_to(root)))
        if p.name.startswith('.env'):
            for line in txt.splitlines():
                line = line.strip()
                if not line or line.startswith('#') or '=' not in line:
                    continue
                k = line.split('=',1)[0].strip()
                if re.fullmatch(r'[A-Z][A-Z0-9_]+', k):
                    found[k].add(str(p.relative_to(root)))
        for k in set(ENV_WORD.findall(txt)):
            if k.startswith(('VITE_','NEXT_PUBLIC_','KEYCLOAK_','STRIPE_','ZAMMAD_','DATABASE_','KC_','POSTGRES_','REDIS_','CORS_','LOG_','UPLOAD_','EXPORT_','RATE_')):
                found[k].add(str(p.relative_to(root)))
    return found

def parse_keycloak_export(path: Path) -> str:
    data = json.loads(path.read_text('utf-8'))
    realm = data.get('realm') or data.get('id') or 'unknown'
    out = []
    out.append(f"# Keycloak – Export: {realm}\n\n")

    out.append("## Realm Basics\n")
    out.append(f"- realm: `{realm}`\n")
    if 'passwordPolicy' in data:
        out.append(f"- passwordPolicy: `{data['passwordPolicy']}`\n")

    ra = data.get('requiredActions', [])
    if ra:
        out.append("\n## Required Actions\n")
        for a in ra:
            out.append(f"- {a.get('alias')} (enabled={a.get('enabled')})\n")

    roles = (data.get('roles', {}).get('realm') or [])
    if roles:
        out.append("\n## Realm Roles\n")
        for r in roles:
            out.append(f"- {r.get('name')}\n")

    clients = data.get('clients', [])
    out.append("\n## Clients\n")
    for c in clients:
        out.append(f"\n### {c.get('clientId')}\n")
        for k in ['publicClient','bearerOnly','standardFlowEnabled','implicitFlowEnabled','directAccessGrantsEnabled','serviceAccountsEnabled','frontchannelLogout','fullScopeAllowed']:
            if k in c:
                out.append(f"- {k}: `{c.get(k)}`\n")
        for label, key in [('redirectUris','redirectUris'), ('webOrigins','webOrigins'), ('postLogoutRedirectUris','postLogoutRedirectUris')]:
            vals = c.get(key) or []
            if vals:
                out.append(f"- {label}:\n")
                for v in vals:
                    out.append(f"  - `{v}`\n")
        mappers = c.get('protocolMappers') or []
        if mappers:
            out.append("- protocolMappers (first 200):\n")
            for m in mappers[:200]:
                out.append(f"  - {m.get('name')} ({m.get('protocol')})\n")
    return ''.join(out)

def scan_fastapi_endpoints(root: Path):
    # Best-effort regex: @router.get("/path")
    endpoints = []
    dec = re.compile(r'@(?P<router>[a-zA-Z0-9_]+)\.(?P<meth>get|post|put|patch|delete|options|head)\(\s*[\'\"](?P<path>[^\'\"]+)')
    for p in iter_files(root, exts={'.py'}):
        txt = p.read_text('utf-8', errors='ignore')
        for m in dec.finditer(txt):
            endpoints.append((m.group('meth').upper(), m.group('path'), str(p.relative_to(root))))
    return sorted(set(endpoints))

def scan_compose(root: Path):
    return sorted({str(p.relative_to(root)) for p in root.rglob('docker-compose*.y*ml')})

def scan_configs(root: Path):
    names = ['next.config.js','next.config.mjs','next.config.ts','vite.config.ts','vite.config.js','tailwind.config.js','tailwind.config.ts']
    out=set()
    for n in names:
        for p in root.rglob(n):
            out.add(str(p.relative_to(root)))
    return sorted(out)

def write_md(path: Path, content: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, 'utf-8')

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--root', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--keycloak', default=None)
    ap.add_argument('--schema', default=None)
    args = ap.parse_args()

    root = Path(args.root).resolve()
    out = Path(args.out).resolve()
    out.mkdir(parents=True, exist_ok=True)

    # ENV
    env = scan_env(root)
    lines = ['# ENV Variablen (auto)\n\n| Variable | Vorkommen |\n|---|---|\n']
    for k in sorted(env.keys()):
        files = ', '.join(sorted(env[k])[:10])
        more = '' if len(env[k]) <= 10 else f" (+{len(env[k])-10} mehr)"
        lines.append(f"| `{k}` | {files}{more} |\n")
    write_md(out/'03-env.md', ''.join(lines))

    # Keycloak
    if args.keycloak:
        write_md(out/'02-keycloak.md', parse_keycloak_export(Path(args.keycloak)))
    else:
        write_md(out/'02-keycloak.md', '# Keycloak\n\n⚠️ Kein Realm Export angegeben.\n')

    # API endpoints
    eps = scan_fastapi_endpoints(root)
    api = ['# API Endpoints (auto, best-effort)\n\n| Method | Path | File |\n|---|---|---|\n']
    for meth, path, file in eps:
        api.append(f"| {meth} | `{path}` | `{file}` |\n")
    write_md(out/'05-api.md', ''.join(api))

    # Docker/config files
    compose = scan_compose(root)
    cfgs = scan_configs(root)
    d = ['# Docker/Infra (auto)\n\n## Compose files\n']
    for c in compose:
        d.append(f"- `{c}`\n")
    d.append('\n## Frontend configs\n')
    for c in cfgs:
        d.append(f"- `{c}`\n")
    write_md(out/'04-docker.md', ''.join(d))

    # Schema
    if args.schema:
        sql = Path(args.schema).read_text('utf-8', errors='ignore')
        write_md(out/'06-db.md', '# DB Schema\n\n```sql\n'+sql+'\n```\n')
    else:
        write_md(out/'06-db.md', '# DB Schema\n\n⚠️ Kein schema.sql angegeben. (pg_dump --schema-only empfohlen)\n')

    # Placeholders
    for name in ['00-overview.md','01-architecture.md','07-frontends.md','08-release-updates.md','TOC.md']:
        src = Path(__file__).resolve().parent.parent/'docs'/name
        if src.exists():
            write_md(out/name, src.read_text('utf-8', errors='ignore'))
        else:
            write_md(out/name, f'# {name}\n')

    print(f'OK. Docs generated at: {out}')

if __name__ == '__main__':
    main()
