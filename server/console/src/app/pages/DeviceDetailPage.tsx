import { useState } from "react";
import { useParams, Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, ShieldOff, ShieldCheck } from "lucide-react";
import { getDeviceDetail, blockDevice, unblockDevice } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import { useAuth } from "../context/auth-context";
import { formatRelative, formatDateTime } from "@/lib/utils";
import { toast } from "@/components/ui/use-toast";

export default function DeviceDetailPage() {
  const { deviceId } = useParams<{ deviceId: string }>();
  const { isAdmin } = useAuth();
  const queryClient = useQueryClient();
  const [actionLoading, setActionLoading] = useState(false);

  const { data, isLoading, error } = useQuery({
    queryKey: ["device-detail", deviceId],
    queryFn: () => getDeviceDetail(deviceId!),
    enabled: !!deviceId,
  });

  const err = error as (Error & { status?: number }) | null;

  async function handleBlock() {
    if (!data || !confirm(`Gerät ${data.hostname} sperren?`)) return;
    setActionLoading(true);
    try {
      await blockDevice(deviceId!);
      toast({ title: "Gerät gesperrt", variant: "warning" });
      queryClient.invalidateQueries({ queryKey: ["device-detail", deviceId] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    } finally {
      setActionLoading(false);
    }
  }

  async function handleUnblock() {
    if (!data || !confirm(`Gerät ${data.hostname} entsperren?`)) return;
    setActionLoading(true);
    try {
      await unblockDevice(deviceId!);
      toast({ title: "Gerät entsperrt", variant: "success" });
      queryClient.invalidateQueries({ queryKey: ["device-detail", deviceId] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    } finally {
      setActionLoading(false);
    }
  }

  return (
    <div>
      <div className="mb-4">
        <Link to="/devices" className="inline-flex items-center gap-1.5 text-sm text-[var(--text-muted)] hover:text-[var(--text-primary)] transition-colors">
          <ArrowLeft className="w-3.5 h-3.5" />
          Zurück zu Geräten
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
            title={data.hostname || data.deviceInstallId}
            subtitle={data.os || "Unbekanntes OS"}
            action={
              isAdmin() ? (
                data.blocked ? (
                  <Button variant="ghost" size="sm" onClick={handleUnblock} disabled={actionLoading}>
                    <ShieldCheck className="w-4 h-4" />
                    Entsperren
                  </Button>
                ) : (
                  <Button variant="danger" size="sm" onClick={handleBlock} disabled={actionLoading}>
                    <ShieldOff className="w-4 h-4" />
                    Sperren
                  </Button>
                )
              ) : undefined
            }
          />

          {data.blocked && (
            <div className="mb-4">
              <Badge variant="danger">Gerät gesperrt</Badge>
            </div>
          )}

          <Tabs defaultValue="info">
            <TabsList>
              <TabsTrigger value="info">Info</TabsTrigger>
              <TabsTrigger value="tokens">Tokens</TabsTrigger>
            </TabsList>

            <TabsContent value="info">
              <Card>
                <CardHeader><CardTitle>Geräteinformationen</CardTitle></CardHeader>
                <CardContent>
                  <div className="grid grid-cols-2 gap-4 lg:grid-cols-3">
                    {[
                      ["Install-ID", data.deviceInstallId],
                      ["Hostname", data.hostname],
                      ["Betriebssystem", data.os],
                      ["Agent-Version", data.agentVersion ?? data.agent],
                      ["Agent-Channel", data.agentChannel],
                      ["Desktop-Version", data.desktopVersion],
                      ["Updater-Version", data.updaterVersion],
                      ["Update-Channel", data.updateChannel],
                      ["IP-Adresse", data.ip],
                      ["Online", data.online ? "Ja" : "Nein"],
                      ["Zuletzt gesehen", formatRelative(data.lastSeen)],
                      ["Registriert am", formatDateTime(data.createdAt)],
                    ].map(([label, value]) => (
                      <div key={label}>
                        <p className="text-[0.7rem] text-[var(--text-muted)] uppercase tracking-wider mb-1">{label}</p>
                        <p className="text-sm text-[var(--text-primary)] font-mono">{value || "–"}</p>
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>
            </TabsContent>

            <TabsContent value="tokens">
              <Card>
                <CardHeader><CardTitle>Device Tokens</CardTitle></CardHeader>
                <CardContent>
                  {!data.tokens?.length ? (
                    <p className="text-sm text-[var(--text-muted)] py-8 text-center">Keine Tokens</p>
                  ) : (
                    <div className="flex flex-col divide-y divide-[var(--border-muted)]">
                      {data.tokens.map((t) => (
                        <div key={t.id} className="flex items-center justify-between py-3 gap-4">
                          <span className="font-mono text-xs text-[var(--text-secondary)] truncate">{t.id}</span>
                          <div className="flex items-center gap-2 shrink-0">
                            {t.revokedAt ? (
                              <Badge variant="danger">Widerrufen</Badge>
                            ) : t.expiresAt ? (
                              <Badge variant="neutral">Läuft ab {formatDateTime(t.expiresAt)}</Badge>
                            ) : (
                              <Badge variant="success">Aktiv</Badge>
                            )}
                            <span className="text-xs text-[var(--text-muted)]">
                              {t.lastUsedAt ? `Zuletzt: ${formatRelative(t.lastUsedAt)}` : "Nie genutzt"}
                            </span>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </CardContent>
              </Card>
            </TabsContent>
          </Tabs>
        </>
      ) : null}
    </div>
  );
}
