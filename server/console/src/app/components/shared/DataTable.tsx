import { useState } from "react";
import { Search } from "lucide-react";
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

  return (
    <div>
      {(onSearch || filters) && (
        <form onSubmit={handleSearch} className="flex items-center gap-2 mb-3 flex-wrap">
          {onSearch && (
            <div className="relative flex-1 min-w-[200px]">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-[var(--text-muted)]" />
              <Input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={searchPlaceholder}
                className="pl-8"
              />
            </div>
          )}
          {filters}
          {onSearch && (
            <Button type="submit" size="sm">Suchen</Button>
          )}
        </form>
      )}

      <div className="overflow-x-auto">
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
                      <Skeleton className="h-4 w-full" />
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
        <div className="flex items-center justify-between mt-3 pt-3 border-t border-[var(--border-muted)]">
          <span className="text-xs text-[var(--text-muted)]">
            {offset + 1}–{Math.min(offset + pageSize, total)} von {total}
          </span>
          <div className="flex gap-1.5">
            <Button
              variant="ghost"
              size="sm"
              disabled={offset === 0}
              onClick={() => onPageChange(Math.max(0, offset - pageSize))}
            >
              ← Zurück
            </Button>
            <Button
              variant="ghost"
              size="sm"
              disabled={offset + pageSize >= total}
              onClick={() => onPageChange(offset + pageSize)}
            >
              Weiter →
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
