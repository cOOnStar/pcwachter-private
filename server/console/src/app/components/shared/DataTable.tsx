import { useState } from "react";
import { Search, ChevronLeft, ChevronRight } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table, TableHeader, TableBody, TableRow, TableHead, TableCell,
} from "@/components/ui/table";
import EmptyState from "./EmptyState";

export interface Column<T> {
  key: string;
  header: string;
  render: (row: T) => React.ReactNode;
  className?: string;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  data: T[];
  total: number;
  loading: boolean;
  pageSize?: number;
  onSearch?: (q: string) => void;
  onPageChange?: (offset: number) => void;
  offset?: number;
  searchPlaceholder?: string;
  filters?: React.ReactNode;
  rowKey: (row: T) => string;
}

export default function DataTable<T>({
  columns,
  data,
  total,
  loading,
  pageSize = 50,
  onSearch,
  onPageChange,
  offset = 0,
  searchPlaceholder = "Suchen…",
  filters,
  rowKey,
}: DataTableProps<T>) {
  const [q, setQ] = useState("");

  function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    onSearch?.(q);
  }

  const colCount = columns.length;
  const currentPage = Math.floor(offset / pageSize) + 1;
  const totalPages = Math.ceil(total / pageSize);

  return (
    <div>
      {(onSearch || filters) && (
        <form onSubmit={handleSearch} className="flex items-center gap-2 mb-4 flex-wrap">
          {onSearch && (
            <div className="relative flex-1 min-w-[200px]">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-[var(--text-muted)] pointer-events-none" />
              <Input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={searchPlaceholder}
                className="pl-9"
              />
            </div>
          )}
          {filters}
          {onSearch && (
            <Button type="submit" size="sm">Suchen</Button>
          )}
        </form>
      )}

      <div className="overflow-x-auto rounded-lg" style={{ border: "1px solid rgba(255,255,255,0.04)" }}>
        <Table>
          <TableHeader>
            <TableRow>
              {columns.map((col) => (
                <TableHead key={col.key} className={col.className}>{col.header}</TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading ? (
              Array.from({ length: 5 }).map((_, i) => (
                <TableRow key={i}>
                  {columns.map((col) => (
                    <TableCell key={col.key}>
                      <Skeleton className="h-4 w-full" style={{ background: "rgba(255,255,255,0.04)" }} />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : data.length === 0 ? (
              <TableRow>
                <TableCell colSpan={colCount} className="p-0">
                  <EmptyState />
                </TableCell>
              </TableRow>
            ) : (
              data.map((row) => (
                <TableRow key={rowKey(row)}>
                  {columns.map((col) => (
                    <TableCell key={col.key} className={col.className}>
                      {col.render(row)}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {total > pageSize && onPageChange && (
        <div className="flex items-center justify-between mt-4 pt-3">
          <span className="text-xs text-[var(--text-muted)]">
            Seite <span className="text-[var(--text-secondary)] font-medium">{currentPage}</span> von{" "}
            <span className="text-[var(--text-secondary)] font-medium">{totalPages}</span>
            <span className="ml-2 text-[var(--text-muted)]">({total} Einträge)</span>
          </span>
          <div className="flex gap-1">
            <Button
              variant="ghost"
              size="sm"
              disabled={offset === 0}
              onClick={() => onPageChange(Math.max(0, offset - pageSize))}
            >
              <ChevronLeft className="w-3.5 h-3.5" />
              Zurück
            </Button>
            <Button
              variant="ghost"
              size="sm"
              disabled={offset + pageSize >= total}
              onClick={() => onPageChange(offset + pageSize)}
            >
              Weiter
              <ChevronRight className="w-3.5 h-3.5" />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
