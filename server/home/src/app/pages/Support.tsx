import { motion } from "motion/react";
import { useEffect, useMemo, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  AlertCircle,
  CheckCircle,
  Clock,
  Filter,
  MessageSquare,
  Paperclip,
  Plus,
  Search,
  Send,
  Star,
  Tag,
  Upload,
  X,
  XCircle,
} from "lucide-react";

import { PREVIEW_BOOTSTRAP, usePortalBootstrap } from "../../hooks";
import {
  closeSupportTicket,
  createSupportTicket,
  rateSupportTicket,
  replySupportTicket,
  uploadSupportAttachment,
} from "../../lib/api";
import { IS_PREVIEW } from "../../lib/keycloak";
import type { SupportTicket } from "../../types";
import { Button } from "../components/ui/button";
import { Badge } from "../components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "../components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "../components/ui/dialog";
import { Input } from "../components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "../components/ui/select";
import { Textarea } from "../components/ui/textarea";

type DraftAttachment = { id: string; name: string; size: number };
type CategoryOption = { value: string; label: string; groupId?: number };

function formatDateTime(dateStr: string) {
  return new Date(dateStr).toLocaleDateString("de-DE", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function formatRelativeTime(dateStr: string) {
  const now = new Date();
  const date = new Date(dateStr);
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);
  const diffDays = Math.floor(diffHours / 24);
  if (diffMins < 1) return "gerade eben";
  if (diffMins < 60) return `vor ${diffMins} Min.`;
  if (diffHours < 24) return `vor ${diffHours} Std.`;
  return `vor ${diffDays} Tag${diffDays > 1 ? "en" : ""}`;
}

function formatFileSize(bytes: number) {
  return bytes >= 1024 * 1024
    ? `${(bytes / (1024 * 1024)).toFixed(1)} MB`
    : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

export function Support() {
  const queryClient = useQueryClient();
  const { data } = usePortalBootstrap();
  const portal = data ?? PREVIEW_BOOTSTRAP;
  const createFileInputRef = useRef<HTMLInputElement | null>(null);
  const replyFileInputRef = useRef<HTMLInputElement | null>(null);
  const [searchTerm, setSearchTerm] = useState("");
  const [filterStatus, setFilterStatus] = useState("all");
  const [newTicketOpen, setNewTicketOpen] = useState(false);
  const [selectedTicketId, setSelectedTicketId] = useState<number | null>(null);
  const [closeConfirmTicketId, setCloseConfirmTicketId] = useState<number | null>(null);
  const [createTitle, setCreateTitle] = useState("");
  const [createCategory, setCreateCategory] = useState("");
  const [createBody, setCreateBody] = useState("");
  const [createFiles, setCreateFiles] = useState<File[]>([]);
  const [creatingTicket, setCreatingTicket] = useState(false);
  const [replyBody, setReplyBody] = useState("");
  const [replyFiles, setReplyFiles] = useState<File[]>([]);
  const [sendingReply, setSendingReply] = useState(false);
  const [closingTicket, setClosingTicket] = useState(false);
  const [ratingValue, setRatingValue] = useState(5);
  const [ratingComment, setRatingComment] = useState("");
  const [savingRating, setSavingRating] = useState(false);

  const selectedTicket = portal.supportTickets.find((ticket) => ticket.id === selectedTicketId) ?? null;
  const categoryOptions = useMemo<CategoryOption[]>(() => {
    if (portal.supportConfig.allow_customer_group_selection && portal.supportConfig.groups.length > 0) {
      return portal.supportConfig.groups.map((group) => ({ value: group.name, label: group.name, groupId: group.id }));
    }
    const values = new Set<string>();
    portal.ticketTemplates.forEach((template) => values.add(template.category));
    portal.supportTickets.forEach((ticket) => values.add(ticket.category));
    if (values.size === 0) ["Installation", "Lizenzierung", "Technischer Support", "Konfiguration", "Feedback"].forEach((value) => values.add(value));
    return Array.from(values).map((value) => ({ value, label: value }));
  }, [portal.supportConfig.allow_customer_group_selection, portal.supportConfig.groups, portal.supportTickets, portal.ticketTemplates]);

  useEffect(() => {
    if (!createCategory && categoryOptions.length > 0) setCreateCategory(categoryOptions[0].value);
  }, [categoryOptions, createCategory]);

  useEffect(() => {
    setRatingValue(selectedTicket?.rating?.rating ?? 5);
    setRatingComment(selectedTicket?.rating?.comment ?? "");
    if (!selectedTicket) {
      setReplyBody("");
      setReplyFiles([]);
    }
  }, [selectedTicket]);

  const selectedTemplate = portal.ticketTemplates.find((template) => template.category === createCategory) ?? null;
  const filteredTickets = portal.supportTickets.filter((ticket) => {
    const matchesSearch = ticket.title.toLowerCase().includes(searchTerm.toLowerCase()) || ticket.id.toString().includes(searchTerm.toLowerCase());
    const matchesFilter = filterStatus === "all" || ticket.status === filterStatus;
    return matchesSearch && matchesFilter;
  });
  const responseTimes = portal.supportTickets
    .map((ticket) => {
      const firstSupportMessage = ticket.messages.find((message) => message.isSupport);
      if (!firstSupportMessage) return null;
      const diffHours = (new Date(firstSupportMessage.timestamp).getTime() - new Date(ticket.createdAt).getTime()) / 3600000;
      return diffHours >= 0 ? diffHours : null;
    })
    .filter((value): value is number => value !== null);
  const averageResponseTime = responseTimes.length > 0 ? responseTimes.reduce((sum, value) => sum + value, 0) / responseTimes.length : null;
  const stats = {
    open: portal.supportTickets.filter((ticket) => ticket.status === "Offen").length,
    inProgress: portal.supportTickets.filter((ticket) => ticket.status === "In Bearbeitung" || ticket.status === "Warten auf Antwort").length,
    closed: portal.supportTickets.filter((ticket) => ticket.status === "Geschlossen").length,
    avgResponseTime: averageResponseTime === null ? "–" : averageResponseTime < 1 ? "< 1 Std." : `${averageResponseTime.toFixed(1).replace(".", ",")} Std.`,
  };

  const getStatusColor = (status: string) => {
    if (status === "Offen") return "bg-yellow-100 text-yellow-700 border-yellow-300";
    if (status === "In Bearbeitung") return "bg-blue-100 text-blue-700 border-blue-300";
    if (status === "Warten auf Antwort") return "bg-orange-100 text-orange-700 border-orange-300";
    if (status === "Geschlossen") return "bg-green-100 text-green-700 border-green-300";
    return "bg-gray-100 text-gray-700 border-gray-300";
  };
  const getStatusIcon = (status: string) => (status === "Geschlossen" ? CheckCircle : status === "Offen" ? AlertCircle : Clock);
  const getCategoryColor = (category: string) => {
    if (category === "Installation") return "bg-purple-100 text-purple-700 border-purple-300";
    if (category === "Lizenzierung") return "bg-indigo-100 text-indigo-700 border-indigo-300";
    if (category === "Technischer Support") return "bg-cyan-100 text-cyan-700 border-cyan-300";
    if (category === "Konfiguration") return "bg-orange-100 text-orange-700 border-orange-300";
    if (category === "Feedback") return "bg-pink-100 text-pink-700 border-pink-300";
    return "bg-gray-100 text-gray-700 border-gray-300";
  };

  const uploadDraftFiles = async (files: File[]): Promise<DraftAttachment[]> => {
    if (files.length === 0) return [];
    if (IS_PREVIEW) return files.map((file, index) => ({ id: `preview-${file.name}-${index}`, name: file.name, size: file.size }));
    const attachments: DraftAttachment[] = [];
    for (const file of files) {
      const uploaded = await uploadSupportAttachment(file);
      attachments.push({ id: uploaded.id, name: uploaded.filename ?? file.name, size: uploaded.size ?? file.size });
    }
    return attachments;
  };

  const handleCreateTicket = async () => {
    if (!createTitle.trim() || !createBody.trim()) {
      toast.error("Ticket unvollständig", { description: "Bitte füllen Sie Betreff und Beschreibung aus." });
      return;
    }
    setCreatingTicket(true);
    try {
      const attachments = await uploadDraftFiles(createFiles);
      const selectedCategoryOption = categoryOptions.find((option) => option.value === createCategory);
      if (!IS_PREVIEW) {
        await createSupportTicket({
          title: createTitle.trim(),
          body: createBody.trim(),
          category: createCategory || null,
          group_id: portal.supportConfig.allow_customer_group_selection && selectedCategoryOption?.groupId ? selectedCategoryOption.groupId : undefined,
          attachment_ids: attachments.map((attachment) => attachment.id),
        });
        await queryClient.invalidateQueries({ queryKey: ["portal-bootstrap"] });
      }
      toast.success("Ticket erfolgreich erstellt", { description: "Ihr Anliegen wurde an den Support übermittelt." });
      setCreateTitle("");
      setCreateBody("");
      setCreateFiles([]);
      setNewTicketOpen(false);
    } catch (error) {
      toast.error("Ticket konnte nicht erstellt werden", { description: error instanceof Error ? error.message : "Bitte erneut versuchen." });
    } finally {
      setCreatingTicket(false);
    }
  };

  const handleSendMessage = async () => {
    if (!selectedTicket || !replyBody.trim()) {
      toast.error("Nachricht leer", { description: "Bitte geben Sie eine Antwort ein." });
      return;
    }
    setSendingReply(true);
    try {
      const attachments = await uploadDraftFiles(replyFiles);
      if (!IS_PREVIEW) {
        await replySupportTicket(selectedTicket.id, {
          body: replyBody.trim(),
          attachment_ids: attachments.map((attachment) => attachment.id),
        });
        await queryClient.invalidateQueries({ queryKey: ["portal-bootstrap"] });
      }
      toast.success("Nachricht gesendet", { description: "Ihre Antwort wurde an das Support-Team gesendet." });
      setReplyBody("");
      setReplyFiles([]);
    } catch (error) {
      toast.error("Antwort konnte nicht gesendet werden", { description: error instanceof Error ? error.message : "Bitte erneut versuchen." });
    } finally {
      setSendingReply(false);
    }
  };

  const handleCloseTicket = async (ticketId: number) => {
    setClosingTicket(true);
    try {
      if (!IS_PREVIEW) {
        await closeSupportTicket(ticketId);
        await queryClient.invalidateQueries({ queryKey: ["portal-bootstrap"] });
      }
      toast.success("Ticket geschlossen", { description: `Ticket #${ticketId} wurde erfolgreich geschlossen.` });
      setCloseConfirmTicketId(null);
    } catch (error) {
      toast.error("Ticket konnte nicht geschlossen werden", { description: error instanceof Error ? error.message : "Bitte erneut versuchen." });
    } finally {
      setClosingTicket(false);
    }
  };

  const handleSaveRating = async () => {
    if (!selectedTicket) return;
    setSavingRating(true);
    try {
      if (!IS_PREVIEW) {
        await rateSupportTicket(selectedTicket.id, ratingValue, ratingComment.trim() || undefined);
        await queryClient.invalidateQueries({ queryKey: ["portal-bootstrap"] });
      }
      toast.success("Bewertung gespeichert", { description: "Vielen Dank für Ihr Feedback." });
    } catch (error) {
      toast.error("Bewertung fehlgeschlagen", { description: error instanceof Error ? error.message : "Bitte erneut versuchen." });
    } finally {
      setSavingRating(false);
    }
  };

  const isTicketCloseable = (ticket: SupportTicket) => ticket.status !== "Geschlossen";

  return (
    <div className="p-4 md:p-6 space-y-6 max-w-7xl mx-auto">
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl md:text-3xl font-bold text-gray-900">Support</h1>
          <p className="text-gray-600 mt-1">Verwalten Sie Ihre Support-Anfragen</p>
        </div>
        <Dialog open={newTicketOpen} onOpenChange={setNewTicketOpen}>
          <DialogTrigger asChild>
            <Button className="bg-blue-600 hover:bg-blue-700" disabled={!portal.supportConfig.support_available || portal.supportConfig.maintenance_mode}>
              <Plus className="w-4 h-4 mr-2" />
              Neues Ticket erstellen
            </Button>
          </DialogTrigger>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>Neues Support-Ticket erstellen</DialogTitle>
              <DialogDescription>Beschreiben Sie Ihr Anliegen so detailliert wie möglich.</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <div>
                <label className="text-sm font-medium mb-2 block">Betreff</label>
                <Input value={createTitle} onChange={(event) => setCreateTitle(event.target.value)} placeholder="z.B. Problem mit Lizenzaktivierung" />
              </div>
              <div>
                <label className="text-sm font-medium mb-2 block">Kategorie</label>
                <Select value={createCategory} onValueChange={setCreateCategory}>
                  <SelectTrigger>
                    <SelectValue placeholder="Kategorie wählen" />
                  </SelectTrigger>
                  <SelectContent>
                    {categoryOptions.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {selectedTemplate ? <p className="text-xs text-gray-500 mt-2">{selectedTemplate.description}</p> : null}
              </div>
              <div>
                <label className="text-sm font-medium mb-2 block">Beschreibung</label>
                <Textarea value={createBody} onChange={(event) => setCreateBody(event.target.value)} placeholder="Beschreiben Sie Ihr Problem im Detail..." rows={6} />
              </div>
              <div className="space-y-3">
                <input ref={createFileInputRef} type="file" multiple className="hidden" onChange={(event) => setCreateFiles(Array.from(event.target.files ?? []))} />
                <Button variant="outline" className="w-full" disabled={!portal.supportConfig.uploads_enabled} onClick={() => createFileInputRef.current?.click()}>
                  <Paperclip className="w-4 h-4 mr-2" />
                  Dateien anhängen
                </Button>
                {portal.supportConfig.uploads_enabled ? <p className="text-xs text-gray-500">Max. {formatFileSize(portal.supportConfig.uploads_max_bytes)} pro Datei.</p> : <p className="text-xs text-red-600">Datei-Uploads sind aktuell deaktiviert.</p>}
                {createFiles.length > 0 ? (
                  <div className="space-y-2">
                    {createFiles.map((file) => (
                      <div key={`${file.name}-${file.size}`} className="flex items-center justify-between rounded-lg bg-gray-50 px-3 py-2 text-sm">
                        <span className="truncate">{file.name}</span>
                        <div className="flex items-center gap-3">
                          <span className="text-gray-500">{formatFileSize(file.size)}</span>
                          <button type="button" onClick={() => setCreateFiles((current) => current.filter((currentFile) => currentFile !== file))} className="text-gray-400 hover:text-red-600">
                            <X className="w-4 h-4" />
                          </button>
                        </div>
                      </div>
                    ))}
                  </div>
                ) : null}
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setNewTicketOpen(false)}>Abbrechen</Button>
              <Button onClick={() => void handleCreateTicket()} className="bg-blue-600 hover:bg-blue-700" disabled={creatingTicket}>
                {creatingTicket ? "Erstellt..." : "Ticket erstellen"}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
      {portal.supportConfig.maintenance_mode ? (
        <Card className="border-yellow-200 bg-yellow-50">
          <CardContent className="p-4 flex items-start gap-3">
            <AlertCircle className="w-5 h-5 text-yellow-700 mt-0.5 shrink-0" />
            <div>
              <p className="font-semibold text-yellow-900">Support derzeit eingeschränkt</p>
              <p className="text-sm text-yellow-800">{portal.supportConfig.maintenance_message || "Der Supportbereich ist aktuell in Wartung."}</p>
            </div>
          </CardContent>
        </Card>
      ) : null}
      {!portal.supportConfig.zammad_reachable ? (
        <Card className="border-red-200 bg-red-50">
          <CardContent className="p-4 flex items-start gap-3">
            <XCircle className="w-5 h-5 text-red-700 mt-0.5 shrink-0" />
            <div>
              <p className="font-semibold text-red-900">Support-System momentan nicht erreichbar</p>
              <p className="text-sm text-red-800">Bestehende Tickets bleiben sichtbar, neue Aktionen können aber fehlschlagen.</p>
            </div>
          </CardContent>
        </Card>
      ) : null}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0 }} className="h-full">
          <Card className="border-l-4 border-l-yellow-500 h-full"><CardContent className="p-4"><div className="flex items-center gap-2"><div className="p-2 bg-yellow-50 rounded-lg"><AlertCircle className="w-5 h-5 text-yellow-600" /></div><div><p className="text-xs text-gray-600">Offen</p><p className="text-xl font-bold text-gray-900">{stats.open}</p></div></div></CardContent></Card>
        </motion.div>
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }} className="h-full">
          <Card className="border-l-4 border-l-blue-500 h-full"><CardContent className="p-4"><div className="flex items-center gap-2"><div className="p-2 bg-blue-50 rounded-lg"><Clock className="w-5 h-5 text-blue-600" /></div><div><p className="text-xs text-gray-600">In Bearbeitung</p><p className="text-xl font-bold text-gray-900">{stats.inProgress}</p></div></div></CardContent></Card>
        </motion.div>
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.2 }} className="h-full">
          <Card className="border-l-4 border-l-green-500 h-full"><CardContent className="p-4"><div className="flex items-center gap-2"><div className="p-2 bg-green-50 rounded-lg"><CheckCircle className="w-5 h-5 text-green-600" /></div><div><p className="text-xs text-gray-600">Geschlossen</p><p className="text-xl font-bold text-gray-900">{stats.closed}</p></div></div></CardContent></Card>
        </motion.div>
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.3 }} className="h-full">
          <Card className="border-l-4 border-l-purple-500 h-full"><CardContent className="p-4"><div className="flex items-center gap-2"><div className="p-2 bg-purple-50 rounded-lg"><Clock className="w-5 h-5 text-purple-600" /></div><div><p className="text-xs text-gray-600">Ø Antwortzeit</p><p className="text-xl font-bold text-gray-900">{stats.avgResponseTime}</p></div></div></CardContent></Card>
        </motion.div>
      </div>
      <Card>
        <CardContent className="p-4">
          <div className="flex flex-col sm:flex-row gap-4">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
              <Input placeholder="Tickets durchsuchen..." value={searchTerm} onChange={(event) => setSearchTerm(event.target.value)} className="pl-10" />
            </div>
            <Select value={filterStatus} onValueChange={setFilterStatus}>
              <SelectTrigger className="w-full sm:w-48"><Filter className="w-4 h-4 mr-2" /><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Alle Status</SelectItem>
                <SelectItem value="Offen">Offen</SelectItem>
                <SelectItem value="In Bearbeitung">In Bearbeitung</SelectItem>
                <SelectItem value="Warten auf Antwort">Warten auf Antwort</SelectItem>
                <SelectItem value="Geschlossen">Geschlossen</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>
      <div className="space-y-4">
        {filteredTickets.length === 0 ? (
          <motion.div initial={{ opacity: 0, scale: 0.95 }} animate={{ opacity: 1, scale: 1 }}>
            <Card>
              <CardContent className="p-12">
                <div className="flex flex-col items-center justify-center text-center">
                  <div className="w-16 h-16 bg-gray-100 rounded-full flex items-center justify-center mb-4"><Search className="w-8 h-8 text-gray-400" /></div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-2">Keine Tickets gefunden</h3>
                  <p className="text-gray-600 max-w-md">{searchTerm ? `Keine Tickets für "${searchTerm}" gefunden. Versuchen Sie einen anderen Suchbegriff.` : "Es gibt keine Tickets mit dem ausgewählten Status."}</p>
                  {(searchTerm || filterStatus !== "all") ? <Button variant="outline" className="mt-4" onClick={() => { setSearchTerm(""); setFilterStatus("all"); }}>Filter zurücksetzen</Button> : null}
                </div>
              </CardContent>
            </Card>
          </motion.div>
        ) : (
          filteredTickets.map((ticket, index) => {
            const StatusIcon = getStatusIcon(ticket.status);
            return (
              <motion.div key={ticket.id} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: index * 0.05 }}>
                <Card className="hover:shadow-lg transition-shadow cursor-pointer" onClick={() => setSelectedTicketId(ticket.id)}>
                  <CardHeader>
                    <div className="flex flex-col lg:flex-row items-start lg:items-center justify-between gap-4">
                      <div className="flex-1">
                        <CardTitle className="flex items-center gap-3 text-xl"><MessageSquare className="w-5 h-5 text-blue-600" />{ticket.title}</CardTitle>
                        <p className="text-sm text-gray-600 mt-2">Ticket #{ticket.id} • Erstellt am {formatDateTime(ticket.createdAt)}</p>
                      </div>
                      <div className="flex flex-wrap items-center gap-2">
                        <Badge className={getCategoryColor(ticket.category)} variant="outline"><Tag className="w-3 h-3 mr-1" />{ticket.category}</Badge>
                        <Badge className={getStatusColor(ticket.status)} variant="outline"><StatusIcon className="w-3 h-3 mr-1" />{ticket.status}</Badge>
                        {isTicketCloseable(ticket) ? <Button variant="outline" size="sm" className="text-red-600 border-red-200 hover:bg-red-50 hover:text-red-700" onClick={(event) => { event.stopPropagation(); setCloseConfirmTicketId(ticket.id); }}><XCircle className="w-3.5 h-3.5 mr-1" />Schließen</Button> : null}
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent>
                    <p className="text-gray-700 mb-4">{ticket.description}</p>
                    <div className="flex flex-wrap items-center justify-between gap-3 pt-4 border-t">
                      <div className="text-sm text-gray-600">Zuletzt aktualisiert: {formatDateTime(ticket.lastUpdate)}</div>
                      <div className="flex items-center gap-3 text-sm">
                        <span className="text-blue-600 flex items-center gap-1"><MessageSquare className="w-4 h-4" />{ticket.messages.length} Nachrichten</span>
                        {ticket.attachments?.length ? <span className="text-gray-600 flex items-center gap-1"><Paperclip className="w-4 h-4" />{ticket.attachments.length} Anhang{ticket.attachments.length > 1 ? "e" : ""}</span> : null}
                      </div>
                    </div>
                  </CardContent>
                </Card>
              </motion.div>
            );
          })
        )}
      </div>
      <Dialog open={!!selectedTicket} onOpenChange={(open) => setSelectedTicketId(open ? selectedTicketId : null)}>
        <DialogContent className="max-w-3xl max-h-[80vh] overflow-y-auto">
          {selectedTicket ? (
            <>
              <DialogHeader>
                <div className="flex items-start justify-between gap-4">
                  <div className="flex-1">
                    <DialogTitle className="text-2xl">{selectedTicket.title}</DialogTitle>
                    <DialogDescription className="mt-2 flex items-center gap-2 flex-wrap">
                      <span>Ticket #{selectedTicket.id} • Erstellt am {formatDateTime(selectedTicket.createdAt)}</span>
                    </DialogDescription>
                  </div>
                  <div className="flex flex-col items-end gap-2">
                    <Badge className={getStatusColor(selectedTicket.status)} variant="outline">{selectedTicket.status}</Badge>
                    <Badge className={getCategoryColor(selectedTicket.category)} variant="outline"><Tag className="w-3 h-3 mr-1" />{selectedTicket.category}</Badge>
                  </div>
                </div>
              </DialogHeader>
              <div className="space-y-4 py-4">
                {selectedTicket.messages.map((message) => (
                  <div key={message.id} className={`p-4 rounded-lg ${message.isSupport ? "bg-blue-50" : "bg-gray-50"}`}>
                    <div className="flex items-center gap-2 mb-2">
                      <div className={`w-8 h-8 rounded-full flex items-center justify-center text-white text-sm font-semibold ${message.isSupport ? "bg-green-600" : "bg-blue-600"}`}>{message.isSupport ? "S" : message.sender.charAt(0)}</div>
                      <div>
                        <p className="font-medium text-gray-900">{message.isSupport ? "Support Team" : message.sender}</p>
                        <p className="text-xs text-gray-500">{formatDateTime(message.timestamp)} • {formatRelativeTime(message.timestamp)}</p>
                      </div>
                    </div>
                    <p className="text-gray-700 whitespace-pre-wrap">{message.message}</p>
                  </div>
                ))}
                {selectedTicket.attachments?.length ? (
                  <div className="rounded-lg border border-gray-200 p-4">
                    <p className="text-sm font-medium text-gray-900 mb-3">Anhänge</p>
                    <div className="space-y-2">
                      {selectedTicket.attachments.map((attachment) => (
                        <div key={attachment.id} className="flex items-center justify-between rounded-lg bg-gray-50 px-3 py-2 text-sm">
                          <div className="min-w-0">
                            <p className="font-medium text-gray-900 truncate">{attachment.name}</p>
                            <p className="text-xs text-gray-500">{formatFileSize(attachment.size)}</p>
                          </div>
                          <Upload className="w-4 h-4 text-gray-400" />
                        </div>
                      ))}
                    </div>
                  </div>
                ) : null}
                {selectedTicket.status !== "Geschlossen" ? (
                  <div className="pt-4 border-t">
                    <label className="text-sm font-medium mb-2 block">Antwort verfassen</label>
                    <Textarea value={replyBody} onChange={(event) => setReplyBody(event.target.value)} placeholder="Ihre Nachricht..." rows={4} className="mb-3" />
                    <input ref={replyFileInputRef} type="file" multiple className="hidden" onChange={(event) => setReplyFiles(Array.from(event.target.files ?? []))} />
                    {replyFiles.length > 0 ? (
                      <div className="mb-3 space-y-2">
                        {replyFiles.map((file) => (
                          <div key={`${file.name}-${file.size}`} className="flex items-center justify-between rounded-lg bg-gray-50 px-3 py-2 text-sm">
                            <span className="truncate">{file.name}</span>
                            <div className="flex items-center gap-3">
                              <span className="text-gray-500">{formatFileSize(file.size)}</span>
                              <button type="button" onClick={() => setReplyFiles((current) => current.filter((currentFile) => currentFile !== file))} className="text-gray-400 hover:text-red-600">
                                <X className="w-4 h-4" />
                              </button>
                            </div>
                          </div>
                        ))}
                      </div>
                    ) : null}
                    <div className="flex gap-2">
                      <Button variant="outline" className="flex-1" disabled={!portal.supportConfig.uploads_enabled} onClick={() => replyFileInputRef.current?.click()}><Paperclip className="w-4 h-4 mr-2" />Anhang</Button>
                      <Button onClick={() => void handleSendMessage()} className="flex-1 bg-blue-600 hover:bg-blue-700" disabled={sendingReply}><Send className="w-4 h-4 mr-2" />{sendingReply ? "Sendet..." : "Senden"}</Button>
                    </div>
                  </div>
                ) : (
                  <div className="pt-4 border-t space-y-4">
                    <div className="flex items-center gap-2 p-4 bg-gray-50 rounded-lg text-gray-500"><CheckCircle className="w-5 h-5 text-green-500 shrink-0" /><p className="text-sm">Dieses Ticket ist geschlossen. Es können keine weiteren Nachrichten hinzugefügt werden.</p></div>
                    <div className="rounded-lg border border-gray-200 p-4 space-y-3">
                      <div className="flex items-center justify-between gap-3">
                        <div><p className="font-semibold text-gray-900">Support bewerten</p><p className="text-sm text-gray-600">Wie hilfreich war die Bearbeitung dieses Tickets?</p></div>
                        {selectedTicket.rating ? <Badge variant="outline" className="bg-green-50 text-green-700 border-green-300">{selectedTicket.rating.rating}/5 bewertet</Badge> : null}
                      </div>
                      <div className="flex flex-wrap gap-2">
                        {[1, 2, 3, 4, 5].map((value) => (
                          <Button key={value} type="button" variant={ratingValue === value ? "default" : "outline"} className={ratingValue === value ? "bg-blue-600 hover:bg-blue-700" : ""} onClick={() => setRatingValue(value)}>
                            <Star className="w-4 h-4 mr-2" />{value}
                          </Button>
                        ))}
                      </div>
                      <Textarea value={ratingComment} onChange={(event) => setRatingComment(event.target.value)} placeholder="Optionales Feedback" rows={3} />
                      <div className="flex justify-end">
                        <Button onClick={() => void handleSaveRating()} className="bg-blue-600 hover:bg-blue-700" disabled={savingRating}>{savingRating ? "Speichert..." : selectedTicket.rating ? "Bewertung aktualisieren" : "Bewerten"}</Button>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            </>
          ) : null}
        </DialogContent>
      </Dialog>
      <Dialog open={!!closeConfirmTicketId} onOpenChange={(open) => setCloseConfirmTicketId(open ? closeConfirmTicketId : null)}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Ticket schließen</DialogTitle>
            <DialogDescription>Sind Sie sicher, dass Sie dieses Ticket schließen möchten?</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloseConfirmTicketId(null)}>Abbrechen</Button>
            <Button onClick={() => (closeConfirmTicketId ? void handleCloseTicket(closeConfirmTicketId) : undefined)} className="bg-red-600 hover:bg-red-700" disabled={closingTicket}>
              <XCircle className="w-4 h-4 mr-2" />{closingTicket ? "Schließt..." : "Schließen"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
