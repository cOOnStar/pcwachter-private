import { Card, CardContent } from "../components/ui/card";
import { Button } from "../components/ui/button";
import { Badge } from "../components/ui/badge";
import { Input } from "../components/ui/input";
import { Label } from "../components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "../components/ui/dialog";
import {
  Monitor,
  Laptop,
  Server,
  Search,
  Wifi,
  WifiOff,
  Wrench,
  Key,
  Clock,
  Shield,
  AlertCircle,
  CheckCircle,
  AlertTriangle,
  XCircle,
  ArrowDownCircle,
  ScanSearch,
  Settings,
  Pencil,
  Unplug,
  RefreshCw,
  Info,
  History,
  ShieldCheck,
  ShieldAlert,
  ShieldX,
  Calendar,
  Fingerprint,
} from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import { useState } from "react";
import { toast } from "sonner";
import type { Device } from "../../types";
import { useQueryClient } from "@tanstack/react-query";
import { PREVIEW_BOOTSTRAP, usePortalBootstrap } from "../../hooks";
import { assignDeviceLicense, removeDevice, renameDevice } from "../../lib/api";
import { IS_PREVIEW } from "../../lib/keycloak";

export function Devices() {
  const queryClient = useQueryClient();
  const { data } = usePortalBootstrap();
  const portal = data ?? PREVIEW_BOOTSTRAP;
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("Alle");
  const [selectedDevice, setSelectedDevice] = useState<Device | null>(null);
  const [detailTab, setDetailTab] = useState<string>("allgemein");
  const [renameDialogOpen, setRenameDialogOpen] = useState(false);
  const [renameValue, setRenameValue] = useState("");
  const [removeDialogOpen, setRemoveDialogOpen] = useState(false);
  const [assignDialogOpen, setAssignDialogOpen] = useState(false);
  const [selectedLicenseId, setSelectedLicenseId] = useState("");

  const devices = portal.devices.filter((device) => {
    const matchesSearch =
      device.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      device.os.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesStatus =
      statusFilter === "Alle" || device.status === statusFilter;
    return matchesSearch && matchesStatus;
  });

  const stats = {
    total: portal.devices.length,
    online: portal.devices.filter((d) => d.status === "Online").length,
    offline: portal.devices.filter((d) => d.status === "Offline").length,
    maintenance: portal.devices.filter((d) => d.status === "Wartung").length,
  };
  const availableLicenses = portal.licenses.filter((license) => license.status !== "Abgelaufen");

  const getStatusColor = (status: string) => {
    switch (status) {
      case "Online":
        return "bg-green-100 text-green-700 border-green-300";
      case "Offline":
        return "bg-red-100 text-red-700 border-red-300";
      case "Wartung":
        return "bg-yellow-100 text-yellow-700 border-yellow-300";
      default:
        return "bg-gray-100 text-gray-700 border-gray-300";
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case "Online":
        return <Wifi className="w-3.5 h-3.5" />;
      case "Offline":
        return <WifiOff className="w-3.5 h-3.5" />;
      case "Wartung":
        return <Wrench className="w-3.5 h-3.5" />;
      default:
        return null;
    }
  };

  const getDeviceIcon = (type: string) => {
    switch (type) {
      case "Desktop":
        return Monitor;
      case "Laptop":
        return Laptop;
      case "Server":
        return Server;
      default:
        return Monitor;
    }
  };

  const getSecurityIcon = (status: string) => {
    switch (status) {
      case "Geschützt":
        return <ShieldCheck className="w-4 h-4 text-green-600" />;
      case "Warnung":
        return <ShieldAlert className="w-4 h-4 text-yellow-600" />;
      case "Kritisch":
        return <ShieldX className="w-4 h-4 text-red-600" />;
      default:
        return <Shield className="w-4 h-4 text-gray-400" />;
    }
  };

  const getSecurityBadgeColor = (status: string) => {
    switch (status) {
      case "Geschützt":
        return "bg-green-100 text-green-700 border-green-300";
      case "Warnung":
        return "bg-yellow-100 text-yellow-700 border-yellow-300";
      case "Kritisch":
        return "bg-red-100 text-red-700 border-red-300";
      default:
        return "bg-gray-100 text-gray-700 border-gray-300";
    }
  };

  const getLicenseStatusColor = (status: string) => {
    switch (status) {
      case "Aktiv":
        return "bg-green-100 text-green-700 border-green-300";
      case "Testversion":
        return "bg-blue-100 text-blue-700 border-blue-300";
      case "Abgelaufen":
        return "bg-red-100 text-red-700 border-red-300";
      case "Nicht zugewiesen":
        return "bg-gray-100 text-gray-500 border-gray-300";
      default:
        return "bg-gray-100 text-gray-700 border-gray-300";
    }
  };

  const getHistoryIcon = (type: string) => {
    switch (type) {
      case "check-in":
        return <RefreshCw className="w-3.5 h-3.5 text-blue-500" />;
      case "scan":
        return <ScanSearch className="w-3.5 h-3.5 text-purple-500" />;
      case "warning":
        return <AlertTriangle className="w-3.5 h-3.5 text-yellow-500" />;
      case "action":
        return <Settings className="w-3.5 h-3.5 text-green-500" />;
      case "update":
        return <ArrowDownCircle className="w-3.5 h-3.5 text-cyan-500" />;
      default:
        return <Info className="w-3.5 h-3.5 text-gray-400" />;
    }
  };

  const formatDateTime = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString("de-DE", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  const getRelativeTime = (dateStr: string) => {
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
  };

  const handleRename = async () => {
    if (!renameValue.trim() || !selectedDevice) return;
    try {
      if (!IS_PREVIEW) {
        await renameDevice(selectedDevice.id, renameValue.trim());
        await queryClient.invalidateQueries({ queryKey: ["portal-bootstrap"] });
      }
      toast.success("Gerät umbenannt", {
        description: `${selectedDevice.name} wurde in "${renameValue}" umbenannt.`,
      });
      setRenameDialogOpen(false);
      setRenameValue("");
    } catch (error) {
      toast.error("Umbenennen fehlgeschlagen", {
        description: error instanceof Error ? error.message : "Bitte erneut versuchen.",
      });
    }
  };

  const handleRemoveDevice = async () => {
    if (!selectedDevice) return;
    try {
      if (!IS_PREVIEW) {
        await removeDevice(selectedDevice.id);
        await queryClient.invalidateQueries({ queryKey: ["portal-bootstrap"] });
      }
      toast.success("Gerät entkoppelt", {
        description: `${selectedDevice.name} wurde erfolgreich entkoppelt.`,
      });
      setRemoveDialogOpen(false);
      setSelectedDevice(null);
    } catch (error) {
      toast.error("Entkoppeln fehlgeschlagen", {
        description: error instanceof Error ? error.message : "Bitte erneut versuchen.",
      });
    }
  };

  const handleAssignLicense = async () => {
    if (!selectedDevice || !selectedLicenseId) return;
    try {
      const selectedLicense = portal.licenses.find((license) => license.id === selectedLicenseId);
      if (!IS_PREVIEW) {
        await assignDeviceLicense(selectedDevice.id, selectedLicenseId);
        await queryClient.invalidateQueries({ queryKey: ["portal-bootstrap"] });
      }
      toast.success("Lizenz zugewiesen", {
        description: `${selectedDevice.name} wurde mit ${selectedLicense?.name ?? "der Lizenz"} verknüpft.`,
      });
      setSelectedDevice((current) =>
        current
          ? {
              ...current,
              licenseId: selectedLicenseId,
              licenseName: selectedLicense?.name ?? current.licenseName,
              licenseStatus: selectedLicense ? "Aktiv" : current.licenseStatus,
              licenseType: selectedLicense?.type ?? current.licenseType,
              licenseValidUntil: selectedLicense?.validUntil ?? current.licenseValidUntil,
            }
          : current,
      );
      setSelectedLicenseId("");
      setAssignDialogOpen(false);
    } catch (error) {
      toast.error("Zuweisung fehlgeschlagen", {
        description: error instanceof Error ? error.message : "Bitte erneut versuchen.",
      });
    }
  };

  return (
    <div className="p-4 md:p-6 space-y-6 max-w-7xl mx-auto">
      {/* Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl md:text-3xl font-bold text-gray-900">Meine Geräte</h1>
          <p className="text-gray-600 mt-1">
            Übersicht aller registrierten Geräte mit PC-Wächter
          </p>
        </div>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0 }} className="h-full">
          <Card className="border-l-4 border-l-blue-500 h-full">
            <CardContent className="p-4">
              <div className="flex items-center gap-2">
                <div className="p-2 bg-blue-50 rounded-lg">
                  <Monitor className="w-5 h-5 text-blue-600" />
                </div>
                <div>
                  <p className="text-xs text-gray-600">Geräte gesamt</p>
                  <p className="text-xl font-bold text-gray-900">{stats.total}</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </motion.div>

        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }} className="h-full">
          <Card className="border-l-4 border-l-green-500 h-full">
            <CardContent className="p-4">
              <div className="flex items-center gap-2">
                <div className="p-2 bg-green-50 rounded-lg">
                  <Wifi className="w-5 h-5 text-green-600" />
                </div>
                <div>
                  <p className="text-xs text-gray-600">Online</p>
                  <p className="text-xl font-bold text-gray-900">{stats.online}</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </motion.div>

        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.2 }} className="h-full">
          <Card className="border-l-4 border-l-red-500 h-full">
            <CardContent className="p-4">
              <div className="flex items-center gap-2">
                <div className="p-2 bg-red-50 rounded-lg">
                  <WifiOff className="w-5 h-5 text-red-600" />
                </div>
                <div>
                  <p className="text-xs text-gray-600">Offline</p>
                  <p className="text-xl font-bold text-gray-900">{stats.offline}</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </motion.div>

        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.3 }} className="h-full">
          <Card className="border-l-4 border-l-yellow-500 h-full">
            <CardContent className="p-4">
              <div className="flex items-center gap-2">
                <div className="p-2 bg-yellow-50 rounded-lg">
                  <Wrench className="w-5 h-5 text-yellow-600" />
                </div>
                <div>
                  <p className="text-xs text-gray-600">Wartung</p>
                  <p className="text-xl font-bold text-gray-900">{stats.maintenance}</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </motion.div>
      </div>

      {/* Search & Filter */}
      <Card>
        <CardContent className="p-4">
          <div className="flex flex-col sm:flex-row gap-3">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
              <Input
                placeholder="Geräte durchsuchen (Name, OS)..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="pl-10"
              />
            </div>
            <div className="flex gap-2 flex-wrap">
              {["Alle", "Online", "Offline", "Wartung"].map((status) => (
                <Button
                  key={status}
                  size="sm"
                  variant={statusFilter === status ? "default" : "outline"}
                  onClick={() => setStatusFilter(status)}
                  className={
                    statusFilter === status
                      ? "bg-blue-600 hover:bg-blue-700"
                      : ""
                  }
                >
                  {status}
                </Button>
              ))}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Devices List */}
      <div className="space-y-4">
        {devices.length === 0 ? (
          <Card>
            <CardContent className="p-12 text-center">
              <AlertCircle className="w-12 h-12 text-gray-300 mx-auto mb-4" />
              <p className="text-gray-500">
                Keine Geräte gefunden, die Ihren Filterkriterien entsprechen.
              </p>
            </CardContent>
          </Card>
        ) : (
          devices.map((device, index) => {
            const DeviceIcon = getDeviceIcon(device.type);
            return (
              <motion.div
                key={device.id}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: index * 0.05 }}
              >
                <Card className="hover:shadow-lg transition-shadow">
                  <CardContent className="p-5">
                    {/* Top Row: Name, Type, Status, Security */}
                    <div className="flex flex-col lg:flex-row items-start lg:items-center justify-between gap-3 mb-4">
                      <div className="flex items-center gap-3">
                        <div className="p-2.5 bg-blue-50 rounded-xl">
                          <DeviceIcon className="w-6 h-6 text-blue-600" />
                        </div>
                        <div>
                          <div className="flex items-center gap-2">
                            <h3 className="font-semibold text-gray-900">{device.name}</h3>
                          </div>
                          <p className="text-sm text-gray-500">
                            {device.type} &middot; {device.os}
                          </p>
                        </div>
                      </div>
                      <div className="flex items-center gap-2 flex-wrap">
                        {/* Security Status Badge */}
                        <Badge className={getSecurityBadgeColor(device.securityStatus)} variant="outline">
                          <span className="flex items-center gap-1.5">
                            {getSecurityIcon(device.securityStatus)}
                            {device.securityStatus}
                          </span>
                        </Badge>
                        {/* Connection Status */}
                        <Badge className={getStatusColor(device.status)} variant="outline">
                          <span className="flex items-center gap-1.5">
                            {getStatusIcon(device.status)}
                            {device.status}
                          </span>
                        </Badge>
                        {/* License Status */}
                        <Badge className={getLicenseStatusColor(device.licenseStatus)} variant="outline">
                          <span className="flex items-center gap-1.5">
                            <Key className="w-3.5 h-3.5" />
                            {device.licenseStatus}
                          </span>
                        </Badge>
                      </div>
                    </div>

                    {/* Status Message */}
                    <div className={`mb-4 px-3 py-2 rounded-lg text-sm flex items-center gap-2 ${
                      device.securityStatus === "Geschützt"
                        ? "bg-green-50 text-green-700"
                        : device.securityStatus === "Warnung"
                        ? "bg-yellow-50 text-yellow-700"
                        : "bg-red-50 text-red-700"
                    }`}>
                      {device.securityStatus === "Geschützt" ? (
                        <CheckCircle className="w-4 h-4 shrink-0" />
                      ) : device.securityStatus === "Warnung" ? (
                        <AlertTriangle className="w-4 h-4 shrink-0" />
                      ) : (
                        <XCircle className="w-4 h-4 shrink-0" />
                      )}
                      {device.statusMessage}
                    </div>

                    {/* Quick Info Grid */}
                    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3 mb-4">
                      <div className="flex items-start gap-2">
                        <Clock className="w-4 h-4 text-gray-400 mt-0.5 shrink-0" />
                        <div>
                          <p className="text-xs text-gray-500">Letzter Check-in</p>
                          <p className="text-sm text-gray-900">{getRelativeTime(device.lastSeen)}</p>
                        </div>
                      </div>
                      <div className="flex items-start gap-2">
                        <Shield className="w-4 h-4 text-gray-400 mt-0.5 shrink-0" />
                        <div>
                          <p className="text-xs text-gray-500">Client-Version</p>
                          <p className="text-sm text-gray-900">v{device.pcWaechterVersion}</p>
                        </div>
                      </div>
                      <div className="flex items-start gap-2">
                        <ScanSearch className="w-4 h-4 text-gray-400 mt-0.5 shrink-0" />
                        <div>
                          <p className="text-xs text-gray-500">Letzter Scan</p>
                          <p className="text-sm text-gray-900">
                            {device.lastScan ? getRelativeTime(device.lastScan) : "–"}
                          </p>
                        </div>
                      </div>
                      <div className="flex items-start gap-2">
                        <Wrench className="w-4 h-4 text-gray-400 mt-0.5 shrink-0" />
                        <div>
                          <p className="text-xs text-gray-500">Letzte Wartung</p>
                          <p className="text-sm text-gray-900">
                            {device.lastMaintenance ? getRelativeTime(device.lastMaintenance) : "–"}
                          </p>
                        </div>
                      </div>
                      <div className="flex items-start gap-2">
                        <ArrowDownCircle className="w-4 h-4 text-gray-400 mt-0.5 shrink-0" />
                        <div>
                          <p className="text-xs text-gray-500">Update</p>
                          {device.updateAvailable ? (
                            <p className="text-sm text-yellow-700 font-medium">
                              v{device.latestVersion} verfügbar
                            </p>
                          ) : (
                            <p className="text-sm text-green-700">Aktuell</p>
                          )}
                        </div>
                      </div>
                    </div>

                    {/* Actions */}
                    <div className="flex flex-wrap gap-2 pt-3 border-t">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setSelectedDevice(device);
                          setDetailTab("allgemein");
                        }}
                      >
                        <Info className="w-4 h-4 mr-1.5" />
                        Details
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setSelectedDevice(device);
                          setRenameValue(device.name);
                          setRenameDialogOpen(true);
                        }}
                      >
                        <Pencil className="w-4 h-4 mr-1.5" />
                        Umbenennen
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setSelectedDevice(device);
                          setSelectedLicenseId(device.licenseId || "");
                          setAssignDialogOpen(true);
                        }}
                      >
                        <Key className="w-4 h-4 mr-1.5" />
                        Lizenz zuweisen
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        className="text-red-600 hover:text-red-700 hover:bg-red-50"
                        onClick={() => {
                          setSelectedDevice(device);
                          setRemoveDialogOpen(true);
                        }}
                      >
                        <Unplug className="w-4 h-4 mr-1.5" />
                        Entkoppeln
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              </motion.div>
            );
          })
        )}
      </div>

      {/* ====== DETAIL DIALOG ====== */}
      <Dialog open={!!selectedDevice && !renameDialogOpen && !removeDialogOpen} onOpenChange={(open) => { if (!open) setSelectedDevice(null); }}>
        <DialogContent className="sm:max-w-[640px] max-h-[85vh] overflow-y-auto">
          {selectedDevice && (
            <>
              <DialogHeader>
                <DialogTitle className="flex items-center gap-3">
                  {(() => { const DIcon = getDeviceIcon(selectedDevice.type); return <DIcon className="w-5 h-5 text-blue-600" />; })()}
                  {selectedDevice.name}
                </DialogTitle>
                <DialogDescription className="flex items-center gap-2 flex-wrap pt-1">
                  <Badge className={getStatusColor(selectedDevice.status)} variant="outline">
                    <span className="flex items-center gap-1">{getStatusIcon(selectedDevice.status)} {selectedDevice.status}</span>
                  </Badge>
                  <Badge className={getSecurityBadgeColor(selectedDevice.securityStatus)} variant="outline">
                    <span className="flex items-center gap-1">{getSecurityIcon(selectedDevice.securityStatus)} {selectedDevice.securityStatus}</span>
                  </Badge>
                </DialogDescription>
              </DialogHeader>

              {/* Tabs */}
              <div className="flex border-b gap-1 mt-2">
                {[
                  { id: "allgemein", label: "Allgemein", icon: Info },
                  { id: "status", label: "Status", icon: Shield },
                  { id: "lizenz", label: "Lizenz", icon: Key },
                  { id: "verlauf", label: "Verlauf", icon: History },
                ].map((tab) => (
                  <button
                    key={tab.id}
                    onClick={() => setDetailTab(tab.id)}
                    className={`flex items-center gap-1.5 px-3 py-2 text-sm font-medium border-b-2 transition-colors ${
                      detailTab === tab.id
                        ? "border-blue-600 text-blue-600"
                        : "border-transparent text-gray-500 hover:text-gray-700"
                    }`}
                  >
                    <tab.icon className="w-4 h-4" />
                    {tab.label}
                  </button>
                ))}
              </div>

              {/* Tab Content */}
              <div className="mt-4 space-y-4">
                {/* ---- Allgemein ---- */}
                {detailTab === "allgemein" && (
                  <div className="space-y-3">
                    {[
                      { label: "Gerätename", value: selectedDevice.name, icon: Monitor },
                      { label: "Geräte-ID", value: selectedDevice.id, icon: Fingerprint, mono: true },
                      { label: "Gerätetyp", value: selectedDevice.type, icon: getDeviceIcon(selectedDevice.type) },
                      { label: "Betriebssystem", value: selectedDevice.os, icon: Monitor },
                      { label: "PC-Wächter Version", value: `v${selectedDevice.pcWaechterVersion}`, icon: Shield },
                      { label: "Erste Registrierung", value: formatDateTime(selectedDevice.registeredAt), icon: Calendar },
                      { label: "Letzter Kontakt", value: `${formatDateTime(selectedDevice.lastSeen)} (${getRelativeTime(selectedDevice.lastSeen)})`, icon: Clock },
                    ].map((item) => (
                      <div key={item.label} className="flex items-center gap-3 p-3 bg-gray-50 rounded-lg">
                        <item.icon className="w-4 h-4 text-gray-400 shrink-0" />
                        <div className="flex-1 min-w-0">
                          <p className="text-xs text-gray-500">{item.label}</p>
                          <p className={`text-sm text-gray-900 truncate ${item.mono ? "font-mono" : ""}`}>
                            {item.value}
                          </p>
                        </div>
                      </div>
                    ))}
                  </div>
                )}

                {/* ---- Status ---- */}
                {detailTab === "status" && (
                  <div className="space-y-3">
                    <div className={`p-4 rounded-lg flex items-center gap-3 ${
                      selectedDevice.securityStatus === "Geschützt"
                        ? "bg-green-50"
                        : selectedDevice.securityStatus === "Warnung"
                        ? "bg-yellow-50"
                        : "bg-red-50"
                    }`}>
                      {getSecurityIcon(selectedDevice.securityStatus)}
                      <div>
                        <p className="font-semibold text-gray-900">{selectedDevice.statusMessage}</p>
                        <p className="text-sm text-gray-600">Sicherheitsstatus: {selectedDevice.securityStatus}</p>
                      </div>
                    </div>

                    {[
                      {
                        label: "Online-Status",
                        value: selectedDevice.status,
                        sub: selectedDevice.status === "Online"
                          ? `Seit ${getRelativeTime(selectedDevice.lastSeen)} verbunden`
                          : `Zuletzt gesehen: ${getRelativeTime(selectedDevice.lastSeen)}`,
                        color: selectedDevice.status === "Online" ? "text-green-600" : selectedDevice.status === "Offline" ? "text-red-600" : "text-yellow-600",
                      },
                      {
                        label: "Update-Status",
                        value: selectedDevice.updateAvailable
                          ? `Update auf v${selectedDevice.latestVersion} verfügbar`
                          : "Aktuell – keine Updates verfügbar",
                        sub: `Installierte Version: v${selectedDevice.pcWaechterVersion}`,
                        color: selectedDevice.updateAvailable ? "text-yellow-600" : "text-green-600",
                      },
                      {
                        label: "Letzter Scan",
                        value: selectedDevice.lastScan ? formatDateTime(selectedDevice.lastScan) : "Kein Scan durchgeführt",
                        sub: selectedDevice.lastScan ? getRelativeTime(selectedDevice.lastScan) : "",
                        color: "text-gray-900",
                      },
                      {
                        label: "Letzte Wartung",
                        value: selectedDevice.lastMaintenance ? formatDateTime(selectedDevice.lastMaintenance) : "Keine Wartung durchgeführt",
                        sub: selectedDevice.lastMaintenance ? getRelativeTime(selectedDevice.lastMaintenance) : "",
                        color: "text-gray-900",
                      },
                    ].map((item) => (
                      <div key={item.label} className="p-3 bg-gray-50 rounded-lg">
                        <p className="text-xs text-gray-500 mb-1">{item.label}</p>
                        <p className={`text-sm font-medium ${item.color}`}>{item.value}</p>
                        {item.sub && <p className="text-xs text-gray-500 mt-0.5">{item.sub}</p>}
                      </div>
                    ))}
                  </div>
                )}

                {/* ---- Lizenz ---- */}
                {detailTab === "lizenz" && (
                  <div className="space-y-3">
                    {selectedDevice.licenseStatus === "Nicht zugewiesen" ? (
                      <div className="p-4 bg-gray-50 rounded-lg text-center">
                        <Key className="w-8 h-8 text-gray-300 mx-auto mb-2" />
                        <p className="font-medium text-gray-900">Keine Lizenz zugewiesen</p>
                        <p className="text-sm text-gray-500 mt-1">
                          Diesem Gerät ist aktuell keine Lizenz zugewiesen.
                        </p>
                        <Button
                          size="sm"
                          className="mt-3 bg-blue-600 hover:bg-blue-700"
                          onClick={() => {
                            setSelectedLicenseId("");
                            setAssignDialogOpen(true);
                          }}
                        >
                          <Key className="w-4 h-4 mr-1.5" />
                          Lizenz zuweisen
                        </Button>
                      </div>
                    ) : (
                      <>
                        {[
                          { label: "Zugewiesene Lizenz", value: selectedDevice.licenseName },
                          { label: "Lizenzstatus", value: selectedDevice.licenseStatus },
                          { label: "Lizenztyp", value: selectedDevice.licenseType || "–" },
                          { label: "Gültig bis", value: selectedDevice.licenseValidUntil || "–" },
                        ].map((item) => (
                          <div key={item.label} className="p-3 bg-gray-50 rounded-lg">
                            <p className="text-xs text-gray-500">{item.label}</p>
                            <p className="text-sm font-medium text-gray-900">{item.value}</p>
                          </div>
                        ))}
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            setSelectedLicenseId(selectedDevice.licenseId || "");
                            setAssignDialogOpen(true);
                          }}
                        >
                          <RefreshCw className="w-4 h-4 mr-1.5" />
                          Lizenz neu zuweisen
                        </Button>
                      </>
                    )}
                  </div>
                )}

                {/* ---- Verlauf ---- */}
                {detailTab === "verlauf" && (
                  <div className="space-y-0">
                    {selectedDevice.history && selectedDevice.history.length > 0 ? (
                      <div className="relative">
                        {/* Timeline line */}
                        <div className="absolute left-[15px] top-3 bottom-3 w-px bg-gray-200" />
                        {selectedDevice.history.map((entry, i) => (
                          <div key={entry.id} className="relative flex items-start gap-3 py-2.5">
                            <div className="relative z-10 p-1.5 bg-white border border-gray-200 rounded-full">
                              {getHistoryIcon(entry.type)}
                            </div>
                            <div className="flex-1 min-w-0">
                              <p className="text-sm text-gray-900">{entry.message}</p>
                              <p className="text-xs text-gray-500 mt-0.5">
                                {formatDateTime(entry.timestamp)} &middot; {getRelativeTime(entry.timestamp)}
                              </p>
                            </div>
                          </div>
                        ))}
                      </div>
                    ) : (
                      <div className="p-4 text-center text-gray-500">
                        <History className="w-8 h-8 text-gray-300 mx-auto mb-2" />
                        <p>Kein Verlauf vorhanden.</p>
                      </div>
                    )}
                  </div>
                )}
              </div>

              {/* Detail Actions */}
              <DialogFooter className="mt-4 gap-2 sm:gap-0">
                <Button variant="outline" size="sm" onClick={() => { setRenameValue(selectedDevice.name); setRenameDialogOpen(true); }}>
                  <Pencil className="w-4 h-4 mr-1.5" />
                  Umbenennen
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    setSelectedLicenseId(selectedDevice.licenseId || "");
                    setAssignDialogOpen(true);
                  }}
                >
                  <Key className="w-4 h-4 mr-1.5" />
                  Lizenz zuweisen
                </Button>
                <Button variant="outline" size="sm" className="text-red-600 hover:text-red-700 hover:bg-red-50" onClick={() => setRemoveDialogOpen(true)}>
                  <Unplug className="w-4 h-4 mr-1.5" />
                  Gerät entkoppeln
                </Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>

      {/* ====== RENAME DIALOG ====== */}
      <Dialog open={renameDialogOpen} onOpenChange={setRenameDialogOpen}>
        <DialogContent className="sm:max-w-[400px]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Pencil className="w-5 h-5 text-blue-600" />
              Gerät umbenennen
            </DialogTitle>
            <DialogDescription>
              Geben Sie einen neuen Namen für das Gerät ein.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2 py-2">
            <Label htmlFor="deviceName">Gerätename</Label>
            <Input
              id="deviceName"
              value={renameValue}
              onChange={(e) => setRenameValue(e.target.value)}
              placeholder="z.B. Calvin-PC"
            />
          </div>
          <DialogFooter className="gap-2 sm:gap-0">
            <Button variant="outline" onClick={() => setRenameDialogOpen(false)}>
              Abbrechen
            </Button>
            <Button className="bg-blue-600 hover:bg-blue-700" onClick={handleRename} disabled={!renameValue.trim()}>
              Speichern
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ====== REMOVE DIALOG ====== */}
      <Dialog open={removeDialogOpen} onOpenChange={setRemoveDialogOpen}>
        <DialogContent className="sm:max-w-[440px]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-red-600">
              <AlertTriangle className="w-5 h-5" />
              Gerät entkoppeln
            </DialogTitle>
            <DialogDescription className="pt-2">
              <div className="space-y-3">
                <p className="text-sm text-gray-600">
                  Möchten Sie <span className="font-semibold">{selectedDevice?.name}</span> wirklich entkoppeln?
                </p>
                <div className="p-3 bg-yellow-50 border border-yellow-200 rounded-lg text-sm text-yellow-800">
                  <div className="flex items-start gap-2">
                    <AlertTriangle className="w-4 h-4 text-yellow-600 mt-0.5 shrink-0" />
                    <div>
                      Das Gerät wird aus Ihrem Konto entfernt. Die zugewiesene Lizenz wird freigegeben und kann einem anderen Gerät zugewiesen werden.
                    </div>
                  </div>
                </div>
              </div>
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="gap-2 sm:gap-0">
            <Button variant="outline" onClick={() => setRemoveDialogOpen(false)}>
              Abbrechen
            </Button>
            <Button className="bg-red-600 hover:bg-red-700" onClick={handleRemoveDevice}>
              <Unplug className="w-4 h-4 mr-1.5" />
              Gerät entkoppeln
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ====== ASSIGN LICENSE DIALOG ====== */}
      <Dialog
        open={assignDialogOpen}
        onOpenChange={(open) => {
          setAssignDialogOpen(open);
          if (!open) {
            setSelectedLicenseId("");
          }
        }}
      >
        <DialogContent className="sm:max-w-[460px]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Key className="w-5 h-5 text-blue-600" />
              Lizenz zuweisen
            </DialogTitle>
            <DialogDescription>
              Wählen Sie eine aktive Lizenz für {selectedDevice?.name || "dieses Gerät"} aus.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            {availableLicenses.length > 0 ? (
              <>
                <div className="space-y-2">
                  <Label htmlFor="license-select">Verfügbare Lizenzen</Label>
                  <select
                    id="license-select"
                    className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                    value={selectedLicenseId}
                    onChange={(event) => setSelectedLicenseId(event.target.value)}
                  >
                    <option value="">Lizenz auswählen</option>
                    {availableLicenses.map((license) => (
                      <option key={license.id} value={license.id}>
                        {license.name} · {license.devices}/{license.maxDevices} Geräte · gültig bis {license.validUntil}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="rounded-lg bg-gray-50 p-3 text-sm text-gray-600">
                  Nur aktive oder bald ablaufende Lizenzen können zugewiesen werden.
                </div>
              </>
            ) : (
              <div className="rounded-lg border border-yellow-200 bg-yellow-50 p-4 text-sm text-yellow-800">
                Derzeit ist keine aktive Lizenz verfügbar. Kaufen oder verlängern Sie zuerst eine Lizenz.
              </div>
            )}
          </div>
          <DialogFooter className="gap-2 sm:gap-0">
            <Button variant="outline" onClick={() => setAssignDialogOpen(false)}>
              Abbrechen
            </Button>
            <Button
              className="bg-blue-600 hover:bg-blue-700"
              onClick={handleAssignLicense}
              disabled={!selectedLicenseId || availableLicenses.length === 0}
            >
              Lizenz zuweisen
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
