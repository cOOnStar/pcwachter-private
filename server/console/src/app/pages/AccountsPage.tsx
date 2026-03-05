import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { getAccounts, updateAccountRole, type Account } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@/components/ui/select";
import DataTable, { type Column } from "../components/shared/DataTable";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import { useAuth } from "../context/auth-context";
import { formatDate, initials } from "@/lib/utils";
import { toast } from "@/components/ui/use-toast";

const ROLES = ["owner", "admin", "user", "pcw_admin", "pcw_console", "pcw_support"];
const PAGE_SIZE = 50;

export default function AccountsPage() {
  const { isAdmin } = useAuth();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [roleFilter, setRoleFilter] = useState("all");
  const [offset, setOffset] = useState(0);

  const { data, isLoading, error } = useQuery({
    queryKey: ["accounts", search, roleFilter, offset],
    queryFn: () => getAccounts({ search: search || undefined, role: roleFilter === "all" ? undefined : roleFilter, limit: PAGE_SIZE, offset }),
  });

  const err = error as (Error & { status?: number }) | null;

  async function handleRoleChange(account: Account, newRole: string) {
    if (newRole === account.role) return;
    if (!confirm(`Rolle von ${account.name} zu "${newRole}" ändern?`)) return;
    try {
      await updateAccountRole(account.id, newRole);
      toast({ title: `Rolle von ${account.name} aktualisiert`, variant: "success" });
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    }
  }

  const columns: Column<Account>[] = [
    {
      key: "avatar",
      header: "",
      render: (a) => (
        <div className="w-7 h-7 bg-[var(--accent-subtle)] border border-[var(--border)] rounded-full flex items-center justify-center text-[0.68rem] font-semibold text-[var(--accent-hover)]">
          {initials(a.name || a.email)}
        </div>
      ),
      className: "w-10",
    },
    { key: "name", header: "Name", render: (a) => <span className="font-medium text-[var(--text-primary)]">{a.name || "–"}</span> },
    { key: "email", header: "E-Mail", render: (a) => <span className="font-mono text-xs">{a.email}</span> },
    {
      key: "role",
      header: "Rolle",
      render: (a) => isAdmin() ? (
        <Select value={a.role.toLowerCase()} onValueChange={(v) => handleRoleChange(a, v)}>
          <SelectTrigger className="h-7 w-32 text-xs">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {ROLES.map((r) => <SelectItem key={r} value={r}>{r}</SelectItem>)}
          </SelectContent>
        </Select>
      ) : (
        <Badge variant="accent">{a.role}</Badge>
      ),
    },
    {
      key: "status",
      header: "Status",
      render: (a) => <Badge variant={a.status === "active" ? "success" : "neutral"}>{a.status}</Badge>,
    },
    { key: "created", header: "Erstellt", render: (a) => <span className="text-xs text-[var(--text-muted)]">{formatDate(a.created)}</span> },
    { key: "lastLogin", header: "Letzter Login", render: (a) => <span className="text-xs text-[var(--text-muted)]">{a.lastLogin ? formatDate(a.lastLogin) : "–"}</span> },
  ];

  return (
    <div>
      <PageHeader title="Accounts" subtitle="Keycloak-Nutzer und ihre Rollen" />

      {err && <div className="mb-4"><ErrorBanner message={err.message} status={err.status} /></div>}

      <Card>
        <CardHeader>
          <CardTitle>Accounts ({data?.total ?? 0})</CardTitle>
        </CardHeader>
        <CardContent>
          <DataTable
            columns={columns}
            data={data?.items ?? []}
            total={data?.total ?? 0}
            loading={isLoading}
            pageSize={PAGE_SIZE}
            offset={offset}
            onSearch={(q) => { setSearch(q); setOffset(0); }}
            onPageChange={setOffset}
            searchPlaceholder="Name oder E-Mail suchen…"
            rowKey={(a) => a.id}
            filters={
              <Select value={roleFilter} onValueChange={(v) => { setRoleFilter(v); setOffset(0); }}>
                <SelectTrigger className="w-36">
                  <SelectValue placeholder="Alle Rollen" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">Alle Rollen</SelectItem>
                  {ROLES.map((r) => <SelectItem key={r} value={r}>{r}</SelectItem>)}
                </SelectContent>
              </Select>
            }
          />
        </CardContent>
      </Card>
    </div>
  );
}
