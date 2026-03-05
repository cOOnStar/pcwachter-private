import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  getSubscriptions, patchSubscription, type Subscription,
} from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import DataTable, { type Column } from "../components/shared/DataTable";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import { useAuth } from "../context/auth-context";
import { formatDate } from "@/lib/utils";
import { toast } from "@/components/ui/use-toast";

const PAGE_SIZE = 50;

function statusBadge(status: string) {
  const map: Record<string, "success" | "warning" | "danger" | "neutral"> = {
    active: "success",
    grace: "warning",
    cancelled: "danger",
    expired: "neutral",
  };
  return <Badge variant={map[status] ?? "neutral"}>{status}</Badge>;
}

export default function SubscriptionsPage() {
  const { isAdmin } = useAuth();
  const queryClient = useQueryClient();
  const [offset, setOffset] = useState(0);
  const [toggling, setToggling] = useState<string | null>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: ["subscriptions", offset],
    queryFn: () => getSubscriptions({ limit: PAGE_SIZE, offset }),
  });

  const err = error as (Error & { status?: number }) | null;

  async function toggleSelfCancel(sub: Subscription) {
    setToggling(sub.id);
    try {
      await patchSubscription(sub.id, { allow_self_cancel: !sub.allow_self_cancel });
      toast({ title: `Self-Cancel ${!sub.allow_self_cancel ? "aktiviert" : "deaktiviert"}`, variant: "success" });
      queryClient.invalidateQueries({ queryKey: ["subscriptions"] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    } finally {
      setToggling(null);
    }
  }

  const columns: Column<Subscription>[] = [
    {
      key: "user",
      header: "Nutzer-ID",
      render: (s) => (
        <span className="font-mono text-[0.72rem] text-[var(--text-secondary)] truncate max-w-[160px] block">
          {s.keycloak_user_id}
        </span>
      ),
    },
    { key: "plan", header: "Plan", render: (s) => <span className="text-sm">{s.plan_id ?? "–"}</span> },
    { key: "status", header: "Status", render: (s) => statusBadge(s.status) },
    {
      key: "period_end",
      header: "Läuft ab",
      render: (s) => (
        <span className="text-xs text-[var(--text-muted)]">
          {s.current_period_end ? formatDate(s.current_period_end) : "∞"}
        </span>
      ),
    },
    {
      key: "stripe_sub",
      header: "Stripe-Abo",
      render: (s) => (
        <span className="font-mono text-[0.72rem] text-[var(--text-muted)] truncate max-w-[140px] block">
          {s.stripe_subscription_id ?? "–"}
        </span>
      ),
    },
    {
      key: "self_cancel",
      header: "Self-Cancel",
      render: (s) => (
        <Badge variant={s.allow_self_cancel ? "success" : "neutral"}>
          {s.allow_self_cancel ? "Ja" : "Nein"}
        </Badge>
      ),
    },
    {
      key: "actions",
      header: "",
      render: (s) =>
        isAdmin() ? (
          <Button
            variant="ghost"
            size="sm"
            disabled={toggling === s.id}
            onClick={() => toggleSelfCancel(s)}
          >
            {s.allow_self_cancel ? "Kündigung sperren" : "Kündigung erlauben"}
          </Button>
        ) : null,
      className: "w-40",
    },
  ];

  return (
    <div>
      <PageHeader
        title="Abonnements"
        subtitle="Alle Nutzer-Subscriptions und Self-Cancel-Einstellungen"
      />

      {err && <div className="mb-4"><ErrorBanner message={err.message} status={err.status} /></div>}

      <Card>
        <CardHeader>
          <CardTitle>Abonnements ({data?.total ?? 0})</CardTitle>
        </CardHeader>
        <CardContent>
          <DataTable
            columns={columns}
            data={data?.items ?? []}
            total={data?.total ?? 0}
            loading={isLoading}
            pageSize={PAGE_SIZE}
            offset={offset}
            onPageChange={setOffset}
            rowKey={(s) => s.id}
          />
        </CardContent>
      </Card>
    </div>
  );
}
