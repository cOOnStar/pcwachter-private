import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { HeadphonesIcon } from "lucide-react";
import { listSupportTickets, type SupportTicket } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import DataTable, { type Column } from "../components/shared/DataTable";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
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

export default function SupportPage() {
  const { isAdmin } = useAuth();
  const [offset, setOffset] = useState(0);
  const [showAll, setShowAll] = useState(false);

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
          ) : undefined
        }
      />

      {err && (
        <div className="mb-4">
          <ErrorBanner message={err.message} status={err.status} />
        </div>
      )}

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
