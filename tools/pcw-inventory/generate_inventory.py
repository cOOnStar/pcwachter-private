#!/usr/bin/env python3
"""
PCWächter Inventory Generator
- Scans FastAPI routes (APIRouter / include_router)
- Scans SQLAlchemy models (Declarative mappings)
- Scans Alembic migrations
- Scans frontend calls (fetch/axios) by simple heuristics

This is a best-effort static analyzer. It does not execute project code.
"""

from __future__ import annotations
import argparse, ast, json, os, re, sys, hashlib
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

PY_EXTS = {".py"}
FRONT_EXTS = {".ts", ".tsx", ".js", ".jsx"}

ROUTE_DECORATORS = {"get","post","put","delete","patch","options","head","trace","api_route"}

def read_text(p: Path) -> str:
    return p.read_text(encoding="utf-8", errors="ignore")

def safe_rel(p: Path, root: Path) -> str:
    try:
        return str(p.relative_to(root)).replace("\\","/")
    except Exception:
        return str(p).replace("\\","/")

def iter_files(root: Path, ex: List[str], ex_dirs: List[str], ex_globs: List[str], ex_regex: Optional[re.Pattern], exts: set) -> List[Path]:
    files: List[Path] = []
    for p in root.rglob("*"):
        if not p.is_file(): 
            continue
        if p.suffix not in exts:
            continue
        rel = safe_rel(p, root)
        if any(rel.startswith(d.rstrip("/")+ "/") for d in ex_dirs):
            continue
        if any(g in rel for g in ex):
            continue
        if any(p.match(glob) for glob in ex_globs):
            continue
        if ex_regex and ex_regex.search(rel):
            continue
        files.append(p)
    return files

def node_to_str(node: ast.AST) -> str:
    # best-effort stringification for decorator args etc.
    if isinstance(node, ast.Constant):
        return repr(node.value)
    if isinstance(node, ast.Name):
        return node.id
    if isinstance(node, ast.Attribute):
        return f"{node_to_str(node.value)}.{node.attr}"
    if isinstance(node, ast.Call):
        fn = node_to_str(node.func)
        args = ", ".join(node_to_str(a) for a in node.args)
        kws = ", ".join(f"{k.arg}={node_to_str(k.value)}" for k in node.keywords if k.arg)
        inner = ", ".join([x for x in [args, kws] if x])
        return f"{fn}({inner})"
    if isinstance(node, ast.List):
        return "[" + ", ".join(node_to_str(x) for x in node.elts) + "]"
    if isinstance(node, ast.Dict):
        items = []
        for k,v in zip(node.keys, node.values):
            items.append(f"{node_to_str(k)}: {node_to_str(v)}")
        return "{" + ", ".join(items) + "}"
    if isinstance(node, ast.BinOp) and isinstance(node.op, ast.Add):
        return f"({node_to_str(node.left)}+{node_to_str(node.right)})"
    return ast.dump(node, include_attributes=False)

@dataclass
class ApiEndpoint:
    file: str
    router_var: str
    func: str
    methods: List[str]
    path: str
    name: Optional[str] = None
    tags: Optional[str] = None
    dependencies: Optional[str] = None
    response_model: Optional[str] = None
    status_code: Optional[str] = None

@dataclass
class IncludedRouter:
    file: str
    include_expr: str
    router_ref: str
    prefix: Optional[str] = None
    tags: Optional[str] = None
    dependencies: Optional[str] = None

@dataclass
class DbModel:
    file: str
    class_name: str
    table: Optional[str]
    columns: List[Dict[str, Any]]

@dataclass
class AlembicMigration:
    file: str
    revision: Optional[str]
    down_revision: Optional[str]
    message: Optional[str]

@dataclass
class FrontendCall:
    file: str
    kind: str  # fetch|axios|other
    method: Optional[str]
    url_expr: str
    line: int

def parse_fastapi_routes(py_file: Path, root: Path) -> Tuple[List[ApiEndpoint], List[IncludedRouter]]:
    text = read_text(py_file)
    try:
        tree = ast.parse(text)
    except SyntaxError:
        return [], []
    endpoints: List[ApiEndpoint] = []
    includes: List[IncludedRouter] = []

    # Find function defs with decorators that look like router.get("/x")
    for node in ast.walk(tree):
        if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            for dec in node.decorator_list:
                if isinstance(dec, ast.Call) and isinstance(dec.func, ast.Attribute):
                    attr = dec.func.attr
                    if attr in ROUTE_DECORATORS:
                        router_var = node_to_str(dec.func.value)
                        path = dec.args[0] if dec.args else None
                        path_s = node_to_str(path) if path else ""
                        methods = [attr.upper()] if attr != "api_route" else ["API_ROUTE"]
                        # Pull select kwargs
                        kw = {k.arg: k.value for k in dec.keywords if k.arg}
                        ep = ApiEndpoint(
                            file=safe_rel(py_file, root),
                            router_var=router_var,
                            func=node.name,
                            methods=methods,
                            path=path_s.strip("'\""),
                            name=node_to_str(kw["name"]) if "name" in kw else None,
                            tags=node_to_str(kw["tags"]) if "tags" in kw else None,
                            dependencies=node_to_str(kw["dependencies"]) if "dependencies" in kw else None,
                            response_model=node_to_str(kw["response_model"]) if "response_model" in kw else None,
                            status_code=node_to_str(kw["status_code"]) if "status_code" in kw else None,
                        )
                        endpoints.append(ep)

        # app.include_router(...)
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute):
            if node.func.attr == "include_router":
                include_expr = node_to_str(node)
                router_ref = node_to_str(node.args[0]) if node.args else ""
                kw = {k.arg: k.value for k in node.keywords if k.arg}
                inc = IncludedRouter(
                    file=safe_rel(py_file, root),
                    include_expr=include_expr,
                    router_ref=router_ref,
                    prefix=node_to_str(kw["prefix"]) if "prefix" in kw else None,
                    tags=node_to_str(kw["tags"]) if "tags" in kw else None,
                    dependencies=node_to_str(kw["dependencies"]) if "dependencies" in kw else None,
                )
                includes.append(inc)

    return endpoints, includes

def parse_sqlalchemy_models(py_file: Path, root: Path) -> List[DbModel]:
    text = read_text(py_file)
    try:
        tree = ast.parse(text)
    except SyntaxError:
        return []
    models: List[DbModel] = []

    # heuristic: class inherits from Base / DeclarativeBase and has __tablename__
    for node in tree.body:
        if isinstance(node, ast.ClassDef):
            bases = [node_to_str(b) for b in node.bases]
            if not any(b.endswith("Base") or "Declarative" in b or b.endswith("Model") for b in bases):
                # still allow if __tablename__ exists
                pass

            tablename = None
            cols: List[Dict[str, Any]] = []
            for stmt in node.body:
                if isinstance(stmt, ast.Assign) and len(stmt.targets)==1 and isinstance(stmt.targets[0], ast.Name):
                    name = stmt.targets[0].id
                    if name == "__tablename__":
                        tablename = node_to_str(stmt.value).strip("'\"")
                    # Column / mapped_column heuristics
                    if isinstance(stmt.value, ast.Call):
                        fn = node_to_str(stmt.value.func)
                        if fn.endswith("Column") or fn.endswith("mapped_column"):
                            col = {
                                "attr": name,
                                "call": fn,
                                "args": [node_to_str(a) for a in stmt.value.args],
                                "kwargs": {k.arg: node_to_str(k.value) for k in stmt.value.keywords if k.arg},
                            }
                            cols.append(col)

                # annotated assignment e.g. name: Mapped[str] = mapped_column(...)
                if isinstance(stmt, ast.AnnAssign) and isinstance(stmt.target, ast.Name) and isinstance(stmt.value, ast.Call):
                    name = stmt.target.id
                    fn = node_to_str(stmt.value.func)
                    if fn.endswith("mapped_column") or fn.endswith("Column"):
                        col = {
                            "attr": name,
                            "annotation": node_to_str(stmt.annotation),
                            "call": fn,
                            "args": [node_to_str(a) for a in stmt.value.args],
                            "kwargs": {k.arg: node_to_str(k.value) for k in stmt.value.keywords if k.arg},
                        }
                        cols.append(col)

            if tablename or cols:
                models.append(DbModel(
                    file=safe_rel(py_file, root),
                    class_name=node.name,
                    table=tablename,
                    columns=cols
                ))
    return models

def parse_alembic(alembic_dir: Path, root: Path) -> List[AlembicMigration]:
    migrations: List[AlembicMigration] = []
    if not alembic_dir.exists():
        return migrations
    for p in alembic_dir.rglob("*.py"):
        if "versions" not in p.parts:
            continue
        text = read_text(p)
        rev = re.search(r"revision\s*=\s*['\"]([^'\"]+)['\"]", text)
        down = re.search(r"down_revision\s*=\s*['\"]([^'\"]+)['\"]", text)
        msg = re.search(r"\"\"\"(.*?)\"\"\"", text, re.S)
        migrations.append(AlembicMigration(
            file=safe_rel(p, root),
            revision=rev.group(1) if rev else None,
            down_revision=down.group(1) if down else None,
            message=(msg.group(1).strip().splitlines()[0][:120] if msg else None),
        ))
    return migrations

def parse_frontend_calls(front_root: Path, root: Path) -> List[FrontendCall]:
    calls: List[FrontendCall] = []
    if not front_root.exists():
        return calls
    for p in front_root.rglob("*"):
        if not p.is_file() or p.suffix not in FRONT_EXTS:
            continue
        text = read_text(p)
        lines = text.splitlines()
        for i, line in enumerate(lines, start=1):
            # fetch("...")
            m = re.search(r"\bfetch\s*\(\s*([^,\)]+)", line)
            if m:
                calls.append(FrontendCall(
                    file=safe_rel(p, root),
                    kind="fetch",
                    method=None,
                    url_expr=m.group(1).strip(),
                    line=i
                ))
            # axios.get/post/put/delete("...")
            m2 = re.search(r"\baxios\.(get|post|put|delete|patch)\s*\(\s*([^,\)]+)", line)
            if m2:
                calls.append(FrontendCall(
                    file=safe_rel(p, root),
                    kind="axios",
                    method=m2.group(1).upper(),
                    url_expr=m2.group(2).strip(),
                    line=i
                ))
    return calls

def md_table(rows: List[List[str]], headers: List[str]) -> str:
    out = []
    out.append("| " + " | ".join(headers) + " |")
    out.append("|" + "|".join(["---"]*len(headers)) + "|")
    for r in rows:
        out.append("| " + " | ".join((c.replace("\n"," ") if c else "") for c in r) + " |")
    return "\n".join(out)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", required=True, help="Path to PCWächter repo root")
    ap.add_argument("--out", required=True, help="Output directory for generated docs")
    ap.add_argument("--frontend", default="", help="Optional frontend directory to scan (e.g. server/apps/console)")
    ap.add_argument("--exclude-dir", action="append", default=[".git","node_modules","dist","build",".venv","venv","__pycache__"], help="Excluded directories (relative prefix)")
    args = ap.parse_args()

    repo = Path(args.repo).resolve()
    out = Path(args.out).resolve()
    out.mkdir(parents=True, exist_ok=True)

    py_files = []
    for p in repo.rglob("*.py"):
        rel = str(p.relative_to(repo)).replace("\\","/")
        if any(rel.startswith(d.rstrip("/") + "/") for d in args.exclude_dir):
            continue
        py_files.append(p)

    endpoints: List[ApiEndpoint] = []
    includes: List[IncludedRouter] = []
    models: List[DbModel] = []

    for p in py_files:
        eps, incs = parse_fastapi_routes(p, repo)
        if eps:
            endpoints.extend(eps)
        if incs:
            includes.extend(incs)
        ms = parse_sqlalchemy_models(p, repo)
        if ms:
            models.extend(ms)

    # Alembic: try common paths
    alembic_candidates = [
        repo / "server" / "apps" / "api" / "alembic",
        repo / "apps" / "api" / "alembic",
        repo / "alembic",
    ]
    migrations: List[AlembicMigration] = []
    for cand in alembic_candidates:
        mig = parse_alembic(cand, repo)
        if mig:
            migrations = mig
            break

    # Frontend
    frontend_calls: List[FrontendCall] = []
    if args.frontend:
        front_root = (repo / args.frontend).resolve()
        frontend_calls = parse_frontend_calls(front_root, repo)

    # Write JSON
    data_dir = out / "data"
    docs_dir = out / "docs"
    data_dir.mkdir(exist_ok=True)
    docs_dir.mkdir(exist_ok=True)

    (data_dir / "api_endpoints.json").write_text(json.dumps([asdict(x) for x in endpoints], indent=2, ensure_ascii=False), encoding="utf-8")
    (data_dir / "included_routers.json").write_text(json.dumps([asdict(x) for x in includes], indent=2, ensure_ascii=False), encoding="utf-8")
    (data_dir / "db_models.json").write_text(json.dumps([asdict(x) for x in models], indent=2, ensure_ascii=False), encoding="utf-8")
    (data_dir / "alembic.json").write_text(json.dumps([asdict(x) for x in migrations], indent=2, ensure_ascii=False), encoding="utf-8")
    (data_dir / "frontend_calls.json").write_text(json.dumps([asdict(x) for x in frontend_calls], indent=2, ensure_ascii=False), encoding="utf-8")

    # Markdown docs
    # API endpoints
    rows = []
    for e in sorted(endpoints, key=lambda x: (x.path, ",".join(x.methods), x.file)):
        rows.append([",".join(e.methods), e.path, e.router_var, e.func, e.tags or "", e.dependencies or "", e.response_model or "", e.status_code or "", e.file])
    api_md = "# API Endpoints (FastAPI)\n\n" + md_table(rows, ["Methods","Path","Router","Func","Tags","Dependencies","ResponseModel","Status","File"]) + "\n"
    (docs_dir / "API_ENDPOINTS.md").write_text(api_md, encoding="utf-8")

    # Included routers
    rows = []
    for r in includes:
        rows.append([r.router_ref, r.prefix or "", r.tags or "", r.dependencies or "", r.file, r.include_expr[:140]+"..." if len(r.include_expr)>140 else r.include_expr])
    inc_md = "# Router Includes (app.include_router)\n\n" + md_table(rows, ["RouterRef","Prefix","Tags","Dependencies","File","Expr"]) + "\n"
    (docs_dir / "ROUTER_INCLUDES.md").write_text(inc_md, encoding="utf-8")

    # DB Models
    db_lines = ["# DB Models (SQLAlchemy)\n"]
    for m in models:
        db_lines.append(f"## {m.class_name}")
        db_lines.append(f"- File: `{m.file}`")
        db_lines.append(f"- Table: `{m.table or '(unknown)'}`\n")
        if m.columns:
            crow = []
            for c in m.columns:
                ann = c.get("annotation","")
                crow.append([c.get("attr",""), ann, c.get("call",""), " ".join(c.get("args",[])[:3]), json.dumps(c.get("kwargs",{}), ensure_ascii=False)])
            db_lines.append(md_table(crow, ["Attr","Annotation","Call","Args (first 3)","Kwargs"]))
        db_lines.append("")
    (docs_dir / "DB_MODELS.md").write_text("\n".join(db_lines), encoding="utf-8")

    # Alembic
    arows = []
    for m in migrations:
        arows.append([m.revision or "", m.down_revision or "", m.message or "", m.file])
    alembic_md = "# Alembic Migrations\n\n" + md_table(arows, ["Revision","DownRevision","Message","File"]) + "\n"
    (docs_dir / "ALEMBIC.md").write_text(alembic_md, encoding="utf-8")

    # Frontend Calls
    frows = []
    for c in frontend_calls:
        frows.append([c.kind, c.method or "", c.url_expr, str(c.line), c.file])
    front_md = "# Frontend API Calls (heuristic)\n\n" + md_table(frows, ["Kind","Method","URL Expr","Line","File"]) + "\n"
    (docs_dir / "FRONTEND_CALLS.md").write_text(front_md, encoding="utf-8")

    # Features doc (template)
    features_md = """# Features (Registry Template)

Diese Datei ist ein **Template**. Den echten Abgleich erzeugen wir, indem wir:
- Feature Keys aus Code/Console Registry ziehen
- Frontend Calls ↔ API Endpoints matchen
- API Endpoints ↔ DB Models matchen
- und daraus eine Matrix bauen.

## Feature Control Plane (Remote Config / Flags)
- Percent Rollout (stable hash per device)
- Kill-Switch (global off, highest priority)
- Allow/Deny list
- Tier/OS/Version rules
- Audit log
"""
    (docs_dir / "FEATURES.md").write_text(features_md, encoding="utf-8")

    page_matrix = """# Page Matrix (Console + Desktop)

| Area | Page | Subpages | Feature Keys | API Calls | Notes |
|---|---|---|---|---|---|
| Console | Features | Overrides / Rollouts | platform.feature_flags | GET/PUT /api/v1/console/ui/features/... | Control Plane UI |
| Desktop | Dashboard | Score / Report | dashboard.security_score | GET /api/v1/agent/... | local cache |
"""
    (docs_dir / "PAGE_MATRIX.md").write_text(page_matrix, encoding="utf-8")

    print(f"✅ Inventory generated to: {out}")

if __name__ == "__main__":
    main()
