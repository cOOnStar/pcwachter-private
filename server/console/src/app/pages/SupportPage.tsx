import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { HeadphonesIcon, AlertTriangle, Info, Terminal } from "lucide-react";
import {
  listSupportTickets,
  diagZammadRoles,
  diagZammadUser,
  type SupportTicket,
} from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import DataTable, { type Column } from "../components/shared/DataTable";
import PageHeader from "../components/shared/PageHeader";
import EmptyState from "../components/shared/EmptyState";
import { useAuth } from "../context/auth-context";
import { formatRelative } from "@/lib/utils";

const PAGE_SIZE = 50;

function stateVariant(state: string): "success" | "warning" | "danger" | "neutral" {
  const map: Record<string, "success" | "warning" | "danger" | "neutral"> = {
    open: "warning",
    new: "warning",
    pending: "neutral",
    closed: "success",
    resolved: "success",
  };
  return map[state.toLowerCase()] ?? "neutral";
}

function priorityVariant(priority: string): "danger" | "warning" | "info" | "neutral" {
  const map: Record<string, "danger" | "warning" | "info" | "neutral"> = {
    high: "danger",
    urgent: "danger",
    normal: "info",
    low: "neutral",
  };
  return map[priority.toLowerCase()] ?? "neutral";
}

// ---------------------------------------------------------------------------
// Hintbox components
// ---------------------------------------------------------------------------

function HintboxNotConfigured({ isAdmin }: { isAdmin: boolean }) {
  if (!isAdmin) {
    return (
      <div className="flex items-start gap-3 rounded-lg border border-[var(--border)] bg-[var(--bg-card)] text-[var(--text-secondary)] px-4 py-3 text-sm mb-4">
        <Info className="w-4 h-4 mt-0.5 shrink-0" />
        <p>Support ist momentan nicht verfügbar. Bitte kontaktiere den Administrator.</p>
      </div>
    );
  }
  return (
    <div className="flex items-start gap-3 rounded-lg border border-yellow-700/50 bg-yellow-900/10 text-yellow-300 px-4 py-3 text-sm mb-4">
      <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
      <div>
        <p className="font-semibold mb-1">Support nicht konfiguriert (503)</p>
        <p className="mb-2 text-yellow-400/80">Folgende Umgebungsvariablen fehlen im API-Container:</p>
        <ul className="list-disc list-inside space-y-1 font-mono text-xs text-yellow-200">
          <li><code>ZAMMAD_BASE_URL</code> – z.B. <code>https://support.example.com</code></li>
          <li><code>ZAMMAD_API_TOKEN</code> – Zammad HTTP Token (Einstellungen → API)</li>
        </ul>
        <p className="mt-2 text-yellow-400/70 text-xs">
          Nach dem Setzen der Variablen muss der <code>api</code>-Container neu gestartet werden.
        </p>
      </div>
    </div>
  );
}

function HintboxZammadUnreachable({ isAdmin, onDiag }: { isAdmin: boolean; onDiag?: () => void }) {
  if (!isAdmin) {
    return (
      <div className="flex items-start gap-3 rounded-lg border border-[var(--border)] bg-[var(--bg-card)] text-[var(--text-secondary)] px-4 py-3 text-sm mb-4">
        <Info className="w-4 h-4 mt-0.5 shrink-0" />
        <p>Support ist momentan nicht erreichbar. Bitte versuche es später erneut.</p>
      </div>
    );
  }
  return (
    <div className="flex items-start gap-3 rounded-lg border border-red-700/50 bg-red-900/10 text-red-300 px-4 py-3 text-sm mb-4">
      <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
      <div className="flex-1">
        <p className="font-semibold mb-1">Zammad nicht erreichbar (502)</p>
        <p className="text-red-400/80 mb-2">
          Der API-Server kann Zammad nicht erreichen. Prüfe ob der Zammad-Container läuft
          und <code className="text-xs">ZAMMAD_BASE_URL</code> korrekt gesetzt ist.
        </p>
        {onDiag && (
          <Button size="sm" variant="ghost" className="text-red-300 border-red-700/40 border" onClick={onDiag}>
            <Terminal className="w-3.5 h-3.5 mr-1" />
            Diag: Zammad Roles
          </Button>
        )}
      </div>
    </div>
  );
}

function SupportErrorHintbox({
  err,
  isAdmin,
  onDiag,
}: {
  err: Error & { status?: number };
  isAdmin: boolean;
  onDiag: () => void;
}) {
  const detail = err.message ?? "";
  if (err.status === 503 || detail.includes("support_not_configured")) {
    return <HintboxNotConfigured isAdmin={isAdmin} />;
  }
  if (err.status === 502 || detail.includes("zammad_unreachable")) {
    return <HintboxZammadUnreachable isAdmin={isAdmin} onDiag={isAdmin ? onDiag : undefined} />;
  }
  // Generic fallback
  return (
    <div className="flex items-start gap-3 rounded-lg border bg-[var(--danger-subtle)] border-[#6e1f28] text-[var(--danger)] px-4 py-3 text-sm mb-4">
      <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
      <p>{err.message}</p>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Diag panel
// ---------------------------------------------------------------------------

function DiagPanel() {
  const [rolesResult, setRolesResult] = useState<string | null>(null);
  const [rolesLoading, setRolesLoading] = useState(false);
  const [rolesError, setRolesError] = useState<string | null>(null);

  const [userEmail, setUserEmail] = useState("");
  const [userResult, setUserResult] = useState<string | null>(null);
  const [userLoading, setUserLoading] = useState(false);
  const [userError, setUserError] = useState<string | null>(null);

  async function handleDiagRoles() {
    setRolesLoading(true);
    setRolesResult(null);
    setRolesError(null);
    try {
      const res = await diagZammadRoles();
      setRolesResult(JSON.stringify(res, null, 2));
    } catch (e: unknown) {
      setRolesError(String(e));
    } finally {
      setRolesLoading(false);
    }
  }

  async function handleDiagUser() {
    if (!userEmail.trim()) return;
    setUserLoading(true);
    setUserResult(null);
    setUserError(null);
    try {
      const res = await diagZammadUser(userEmail.trim());
      setUserResult(JSON.stringify(res, null, 2));
    } catch (e: unknown) {
      setUserError(String(e));
    } finally {
      setUserLoading(false);
    }
  }

  return (
    <Card className="mb-4">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Terminal className="w-4 h-4" />
          Zammad Diagnose (Admin)
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {/* Roles diag */}
        <div>
          <Button size="sm" variant="ghost" onClick={handleDiagRoles} disabled={rolesLoading}
            className="border border-[var(--border)] text-[var(--text-secondary)]">
            <Terminal className="w-3.5 h-3.5 mr-1" />
            {rolesLoading ? "Lädt…" : "Diag: Zammad Roles"}
          </Button>
          {rolesResult && (
            <pre className="mt-2 text-xs bg-[var(--bg-surface)] rounded-lg p-3 text-[var(--text-secondary)] overflow-x-auto whitespace-pre-wrap">
              {rolesResult}
            </pre>
          )}
          {rolesError && (
            <p className="mt-2 text-xs text-[var(--danger)]">{rolesError}</p>
          )}
        </div>

        {/* User diag */}
        <div>
          <p className="text-xs text-[var(--text-muted)] mb-1.5">Diag: Zammad User (nach E-Mail)</p>
          <div className="flex items-center gap-2">
            <input
              type="email"
              placeholder="user@example.com"
              value={userEmail}
              onChange={(e) => setUserEmail(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && handleDiagUser()}
              className="flex-1 rounded-lg border border-[var(--border)] bg-[var(--bg-surface)] text-sm text-[var(--text-primary)] px-3 py-1.5 placeholder:text-[var(--text-muted)] focus:outline-none focus:ring-2 focus:ring-[var(--accent)]"
            />
            <Button size="sm" variant="ghost" onClick={handleDiagUser}
              disabled={userLoading || !userEmail.trim()}
              className="border border-[var(--border)] text-[var(--text-secondary)]">
              {userLoading ? "Lädt…" : "Prüfen"}
            </Button>
          </div>
          {userResult && (
            <pre className="mt-2 text-xs bg-[var(--bg-surface)] rounded-lg p-3 text-[var(--text-secondary)] overflow-x-auto whitespace-pre-wrap">
              {userResult}
            </pre>
          )}
          {userError && (
            <p className="mt-2 text-xs text-[var(--danger)]">{userError}</p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Main page
// ---------------------------------------------------------------------------

export default function SupportPage() {
  const { isAdmin } = useAuth();
  const [offset, setOffset] = useState(0);
  const [showAll, setShowAll] = useState(false);
  const [showDiag, setShowDiag] = useState(false);

  const { data, isLoading, error } = useQuery({
    queryKey: ["support-tickets", showAll, offset],
    queryFn: () => listSupportTickets({ all: showAll, page: Math.floor(offset / PAGE_SIZE) + 1, perPage: PAGE_SIZE }),
  });

  const err = error as (Error & { status?: number }) | null;

  const columns: Column<SupportTicket>[] = [
    {
      key: "id",
      header: "ID",
      render: (t) => (
        <Link
          to={`/support/${t.id}`}
          className="font-mono text-xs text-[var(--accent)] hover:underline"
        >
          #{t.id}
        </Link>
      ),
    },
    {
      key: "subject",
      header: "Betreff",
      render: (t) => (
        <Link to={`/support/${t.id}`} className="text-sm font-medium text-[var(--text-primary)] hover:underline truncate max-w-[280px] block">
          {t.subject}
        </Link>
      ),
    },
    {
      key: "state",
      header: "Status",
      render: (t) => <Badge variant={stateVariant(t.state)}>{t.state}</Badge>,
    },
    {
      key: "priority",
      header: "Priorität",
      render: (t) => <Badge variant={priorityVariant(t.priority)}>{t.priority}</Badge>,
    },
    ...(showAll
      ? [
          {
            key: "customer_name" as keyof SupportTicket,
            header: "Kunde",
            render: (t: SupportTicket) => <span className="text-xs">{t.customer_name || t.customer_email || "–"}</span>,
          },
        ]
      : []),
    {
      key: "updated_at",
      header: "Aktualisiert",
      render: (t) => <span className="text-xs text-[var(--text-muted)]">{formatRelative(t.updated_at)}</span>,
    },
  ];

  return (
    <div>
      <PageHeader
        title="Support"
        subtitle="Ticket-Übersicht"
        action={
          isAdmin() ? (
            <div className="flex items-center gap-2">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setShowDiag((v) => !v)}
                className="border border-[var(--border)] text-[var(--text-muted)]"
              >
                <Terminal className="w-3.5 h-3.5 mr-1" />
                Diag
              </Button>
              <Button
                variant={showAll ? "default" : "ghost"}
                size="sm"
                onClick={() => {
                  setShowAll((v) => !v);
                  setOffset(0);
                }}
              >
                {showAll ? "Alle Tickets" : "Nur meine Tickets"}
              </Button>
            </div>
          ) : undefined
        }
      />

      {err && (
        <SupportErrorHintbox
          err={err}
          isAdmin={isAdmin()}
          onDiag={() => setShowDiag(true)}
        />
      )}

      {isAdmin() && showDiag && <DiagPanel />}

      <Card>
        <CardHeader>
          <CardTitle>
            {showAll ? "Alle Tickets" : "Meine Tickets"} ({data?.total ?? 0})
          </CardTitle>
        </CardHeader>
        <CardContent>
          {!isLoading && (data?.items ?? []).length === 0 ? (
            <EmptyState
              message="Keine Tickets gefunden"
              icon={<HeadphonesIcon className="w-10 h-10 opacity-40" />}
            />
          ) : (
            <DataTable
              columns={columns}
              data={data?.items ?? []}
              total={data?.total ?? 0}
              loading={isLoading}
              pageSize={PAGE_SIZE}
              offset={offset}
              onPageChange={setOffset}
              rowKey={(t) => t.id}
            />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
