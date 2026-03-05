import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { BookOpen } from "lucide-react";
import { getKnowledgeBase, type KbArticle } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import DataTable, { type Column } from "../components/shared/DataTable";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import EmptyState from "../components/shared/EmptyState";
import { formatDateTime } from "@/lib/utils";

const PAGE_SIZE = 50;

export default function KnowledgeBasePage() {
  const [offset, setOffset] = useState(0);
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");

  function handleSearch(value: string) {
    setSearch(value);
    clearTimeout((handleSearch as { _t?: ReturnType<typeof setTimeout> })._t);
    (handleSearch as { _t?: ReturnType<typeof setTimeout> })._t = setTimeout(() => {
      setDebouncedSearch(value);
      setOffset(0);
    }, 300);
  }

  const { data, isLoading, error } = useQuery({
    queryKey: ["knowledge-base", debouncedSearch, offset],
    queryFn: () => getKnowledgeBase({ search: debouncedSearch || undefined, limit: PAGE_SIZE, offset }),
  });

  const err = error as (Error & { status?: number }) | null;

  const columns: Column<KbArticle>[] = [
    {
      key: "title",
      header: "Titel",
      render: (a) => <span className="font-medium text-[var(--text-primary)]">{a.title}</span>,
    },
    {
      key: "category",
      header: "Kategorie",
      render: (a) => <Badge variant="neutral">{a.category}</Badge>,
    },
    {
      key: "tags",
      header: "Tags",
      render: (a) => (
        <div className="flex flex-wrap gap-1">
          {(a.tags ?? []).map((tag) => (
            <span
              key={tag}
              className="text-[0.65rem] px-1.5 py-0.5 bg-[var(--bg-hover)] text-[var(--text-muted)] rounded"
            >
              {tag}
            </span>
          ))}
        </div>
      ),
    },
    {
      key: "updated_at",
      header: "Aktualisiert",
      render: (a) => <span className="font-mono text-xs">{formatDateTime(a.updated_at)}</span>,
    },
  ];

  return (
    <div>
      <PageHeader title="Knowledge Base" subtitle="Artikel und Dokumentation (nur Lesen)" />

      {err && (
        <div className="mb-4">
          <ErrorBanner message={err.message} status={err.status} />
        </div>
      )}

      <div className="mb-4 flex gap-3">
        <Input
          placeholder="Artikel suchen…"
          value={search}
          onChange={(e) => handleSearch(e.target.value)}
          className="max-w-xs"
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Artikel ({data?.total ?? 0})</CardTitle>
        </CardHeader>
        <CardContent>
          {!isLoading && (data?.items ?? []).length === 0 ? (
            <EmptyState message="Keine Artikel gefunden" icon={<BookOpen className="w-10 h-10 opacity-40" />} />
          ) : (
            <DataTable
              columns={columns}
              data={data?.items ?? []}
              total={data?.total ?? 0}
              loading={isLoading}
              pageSize={PAGE_SIZE}
              offset={offset}
              onPageChange={setOffset}
              rowKey={(a) => a.id}
            />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
