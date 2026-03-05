import { useRef, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Paperclip, Send } from "lucide-react";
import {
  getSupportTicket,
  replySupportTicket,
  uploadSupportAttachment,
} from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import { formatDateTime } from "@/lib/utils";
import { toast } from "@/components/ui/use-toast";

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

export default function SupportTicketDetailPage() {
  const { ticketId } = useParams<{ ticketId: string }>();
  const queryClient = useQueryClient();
  const [replyBody, setReplyBody] = useState("");
  const [sending, setSending] = useState(false);
  const [uploading, setUploading] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: ["support-ticket", ticketId],
    queryFn: () => getSupportTicket(ticketId!),
    enabled: !!ticketId,
  });

  const err = error as (Error & { status?: number }) | null;

  async function handleReply() {
    if (!replyBody.trim() || !ticketId) return;
    setSending(true);
    try {
      await replySupportTicket(ticketId, replyBody.trim());
      setReplyBody("");
      toast({ title: "Antwort gesendet", variant: "success" });
      queryClient.invalidateQueries({ queryKey: ["support-ticket", ticketId] });
    } catch (e: unknown) {
      toast({ title: "Fehler beim Senden", description: String(e), variant: "destructive" });
    } finally {
      setSending(false);
    }
  }

  async function handleFileUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploading(true);
    try {
      await uploadSupportAttachment(file);
      toast({ title: "Datei hochgeladen", variant: "success" });
      queryClient.invalidateQueries({ queryKey: ["support-ticket", ticketId] });
    } catch (e: unknown) {
      toast({ title: "Upload fehlgeschlagen", description: String(e), variant: "destructive" });
    } finally {
      setUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  }

  return (
    <div>
      <div className="mb-4">
        <Link
          to="/support"
          className="inline-flex items-center gap-1.5 text-sm text-[var(--text-muted)] hover:text-[var(--text-primary)] transition-colors"
        >
          <ArrowLeft className="w-3.5 h-3.5" />
          Zurück zu Support
        </Link>
      </div>

      {isLoading ? (
        <div>
          <Skeleton className="h-8 w-64 mb-2" />
          <Skeleton className="h-4 w-48 mb-6" />
          <Skeleton className="h-48 w-full" />
        </div>
      ) : err ? (
        <ErrorBanner message={err.message} status={err.status} />
      ) : data ? (
        <>
          <PageHeader
            title={data.subject}
            subtitle={`Ticket #${data.id}`}
            action={<Badge variant={stateVariant(data.state)}>{data.state}</Badge>}
          />

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 mb-6">
            <Card className="lg:col-span-2">
              <CardHeader>
                <CardTitle>Beschreibung</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-[var(--text-secondary)] whitespace-pre-wrap">
                  {data.description || "Keine Beschreibung"}
                </p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Details</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex flex-col gap-3">
                  {[
                    ["Priorität", data.priority],
                    ["Erstellt", formatDateTime(data.created_at)],
                    ["Aktualisiert", formatDateTime(data.updated_at)],
                    ["Kunde", data.customer_name || data.customer_email || "–"],
                  ].map(([label, value]) => (
                    <div key={label}>
                      <p className="text-[0.7rem] text-[var(--text-muted)] uppercase tracking-wider mb-0.5">
                        {label}
                      </p>
                      <p className="text-sm text-[var(--text-primary)]">{value}</p>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          </div>

          <Card className="mb-4">
            <CardHeader>
              <CardTitle>Verlauf ({data.replies?.length ?? 0})</CardTitle>
            </CardHeader>
            <CardContent>
              {(data.replies ?? []).length === 0 ? (
                <p className="text-sm text-[var(--text-muted)] py-6 text-center">Noch keine Antworten</p>
              ) : (
                <div className="flex flex-col divide-y divide-[var(--border-muted)]">
                  {data.replies.map((r) => (
                    <div key={r.id} className="py-4">
                      <div className="flex items-center gap-2 mb-1.5">
                        <span className="text-sm font-medium text-[var(--text-primary)]">{r.author}</span>
                        {r.internal && <Badge variant="neutral">intern</Badge>}
                        <span className="text-xs text-[var(--text-muted)] ml-auto">
                          {formatDateTime(r.created_at)}
                        </span>
                      </div>
                      <p className="text-sm text-[var(--text-secondary)] whitespace-pre-wrap">{r.body}</p>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          {(data.attachments ?? []).length > 0 && (
            <Card className="mb-4">
              <CardHeader>
                <CardTitle>Anhänge</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex flex-col gap-2">
                  {data.attachments.map((a) => (
                    <div key={a.id} className="flex items-center gap-2 text-sm">
                      <Paperclip className="w-3.5 h-3.5 text-[var(--text-muted)]" />
                      <span className="text-[var(--text-primary)]">{a.filename}</span>
                      <span className="text-xs text-[var(--text-muted)]">
                        ({(a.size / 1024).toFixed(1)} KB)
                      </span>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          )}

          <Card>
            <CardHeader>
              <CardTitle>Antworten</CardTitle>
            </CardHeader>
            <CardContent>
              <textarea
                className="w-full min-h-[100px] rounded-lg border border-[var(--border)] bg-[var(--bg-surface)] text-sm text-[var(--text-primary)] p-3 placeholder:text-[var(--text-muted)] focus:outline-none focus:ring-2 focus:ring-[var(--accent)] resize-y"
                placeholder="Antwort eingeben…"
                value={replyBody}
                onChange={(e) => setReplyBody(e.target.value)}
              />
              <div className="flex items-center gap-3 mt-3">
                <Button
                  size="sm"
                  onClick={handleReply}
                  disabled={sending || !replyBody.trim()}
                >
                  <Send className="w-3.5 h-3.5" />
                  {sending ? "Sendet…" : "Antworten"}
                </Button>

                <input
                  type="file"
                  ref={fileInputRef}
                  className="hidden"
                  onChange={handleFileUpload}
                />
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={uploading}
                >
                  <Paperclip className="w-3.5 h-3.5" />
                  {uploading ? "Lädt hoch…" : "Anhang"}
                </Button>
              </div>
            </CardContent>
          </Card>
        </>
      ) : null}
    </div>
  );
}
