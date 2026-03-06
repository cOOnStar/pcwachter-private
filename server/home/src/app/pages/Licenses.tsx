import { Card, CardContent, CardHeader, CardTitle } from "../components/ui/card";
import { Button } from "../components/ui/button";
import { Badge } from "../components/ui/badge";
import { Input } from "../components/ui/input";
import { 
  Key, 
  Users, 
  AlertCircle, 
  Search, 
  RefreshCw, 
  Download,
  Copy,
  History,
  ShoppingCart,
  CheckCircle,
  Clock
} from "lucide-react";
import { Progress } from "../components/ui/progress";
import { motion } from "motion/react";
import { useState } from "react";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import { PREVIEW_BOOTSTRAP, usePortalBootstrap } from "../../hooks";
import { requestLicenseRenewal } from "../../lib/api";
import { useQueryClient } from "@tanstack/react-query";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "../components/ui/dialog";

export function Licenses() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [searchTerm, setSearchTerm] = useState("");
  const [renewingLicenseId, setRenewingLicenseId] = useState<string | null>(null);
  const { data } = usePortalBootstrap();
  const portal = data ?? PREVIEW_BOOTSTRAP;
  
  const licenses = portal.licenses.filter(license => 
    license.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    license.key.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const stats = {
    activeLicenses: portal.licenses.filter(l => l.status === 'Aktiv').length,
    expiringLicenses: portal.licenses.filter(l => l.status === 'Läuft bald ab').length,
    inactiveLicenses: portal.licenses.filter(l => l.status === 'Abgelaufen').length,
    totalDevices: portal.licenses.reduce((sum, l) => sum + l.devices, 0),
  };

  const copyLicenseKey = (key: string, name: string) => {
    navigator.clipboard.writeText(key);
    toast.success("Lizenzschlüssel kopiert", {
      description: `Der Schlüssel für ${name} wurde kopiert.`,
    });
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Aktiv':
        return 'bg-green-100 text-green-700 border-green-300';
      case 'Läuft bald ab':
        return 'bg-yellow-100 text-yellow-700 border-yellow-300';
      case 'Abgelaufen':
        return 'bg-red-100 text-red-700 border-red-300';
      default:
        return 'bg-gray-100 text-gray-700 border-gray-300';
    }
  };

  const getTypeColor = (type: string) => {
    switch (type) {
      case 'Professional':
        return 'bg-blue-100 text-blue-700 border-blue-300';
      case 'Standard':
        return 'bg-gray-100 text-gray-700 border-gray-300';
      default:
        return 'bg-gray-100 text-gray-700 border-gray-300';
    }
  };

  return (
    <div className="p-4 md:p-6 space-y-6 max-w-7xl mx-auto">
      {/* Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl md:text-3xl font-bold text-gray-900">Lizenzverwaltung</h1>
          <p className="text-gray-600 mt-1">Verwalten Sie Ihre PC-Wächter Lizenzen</p>
        </div>
        <Button 
          onClick={() => navigate('/licenses/buy')}
          className="bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700"
        >
          <ShoppingCart className="w-4 h-4 mr-2" />
          Neue Lizenz kaufen
        </Button>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0 }}
          className="h-full"
        >
          <Card className="border-l-4 border-l-blue-500 h-full">
            <CardContent className="p-4">
              <div className="flex items-center gap-2">
                <div className="p-2 bg-blue-50 rounded-lg">
                  <Key className="w-5 h-5 text-blue-600" />
                </div>
                <div>
                  <p className="text-xs text-gray-600">Aktive Lizenzen</p>
                  <p className="text-xl font-bold text-gray-900">{stats.activeLicenses}</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 }}
          className="h-full"
        >
          <Card className="border-l-4 border-l-yellow-500 h-full">
            <CardContent className="p-4">
              <div className="flex items-center gap-2">
                <div className="p-2 bg-yellow-50 rounded-lg">
                  <Clock className="w-5 h-5 text-yellow-600" />
                </div>
                <div>
                  <p className="text-xs text-gray-600">Läuft bald ab</p>
                  <p className="text-xl font-bold text-gray-900">{stats.expiringLicenses}</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
          className="h-full"
        >
          <Card className="border-l-4 border-l-red-500 h-full">
            <CardContent className="p-4">
              <div className="flex items-center gap-2">
                <div className="p-2 bg-red-50 rounded-lg">
                  <AlertCircle className="w-5 h-5 text-red-600" />
                </div>
                <div>
                  <p className="text-xs text-gray-600">Abgelaufen</p>
                  <p className="text-xl font-bold text-gray-900">{stats.inactiveLicenses}</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.3 }}
          className="h-full"
        >
          <Card className="border-l-4 border-l-green-500 h-full">
            <CardContent className="p-4">
              <div className="flex items-center gap-2">
                <div className="p-2 bg-green-50 rounded-lg">
                  <Users className="w-5 h-5 text-green-600" />
                </div>
                <div>
                  <p className="text-xs text-gray-600">Belegte Plätze</p>
                  <p className="text-xl font-bold text-gray-900">{stats.totalDevices}</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </motion.div>
      </div>

      {/* Search */}
      <Card>
        <CardContent className="p-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
            <Input
              placeholder="Lizenzen durchsuchen..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="pl-10"
            />
          </div>
        </CardContent>
      </Card>

      {/* Licenses List */}
      <div className="space-y-4">
        {licenses.length === 0 ? (
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
          >
            <Card>
              <CardContent className="p-12">
                <div className="flex flex-col items-center justify-center text-center">
                  <div className="w-16 h-16 bg-gray-100 rounded-full flex items-center justify-center mb-4">
                    <Search className="w-8 h-8 text-gray-400" />
                  </div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-2">
                    Keine Lizenzen gefunden
                  </h3>
                  <p className="text-gray-600 max-w-md">
                    Keine Lizenzen für "{searchTerm}" gefunden. Versuchen Sie einen anderen Suchbegriff.
                  </p>
                  <Button
                    variant="outline"
                    className="mt-4"
                    onClick={() => setSearchTerm("")}
                  >
                    Suche zurücksetzen
                  </Button>
                </div>
              </CardContent>
            </Card>
          </motion.div>
        ) : (
        licenses.map((license, index) => (
          <motion.div
            key={license.id}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: index * 0.05 }}
          >
            <Card className="hover:shadow-lg transition-shadow">
              <CardHeader>
                <div className="flex flex-col lg:flex-row items-start lg:items-center justify-between gap-4">
                  <div className="flex-1">
                    <CardTitle className="flex items-center gap-3 text-xl">
                      <Key className="w-6 h-6 text-blue-600" />
                      {license.name}
                    </CardTitle>
                    <p className="text-sm text-gray-600 mt-2 flex items-center gap-2">
                      <span className="font-mono bg-gray-100 px-3 py-1 rounded">
                        {license.key}
                      </span>
                      <button
                        onClick={() => copyLicenseKey(license.key, license.name)}
                        className="p-1 hover:bg-gray-100 rounded"
                      >
                        <Copy className="w-4 h-4 text-gray-500 hover:text-gray-700" />
                      </button>
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge className={getStatusColor(license.status)} variant="outline">
                      {license.status}
                    </Badge>
                    <Badge className={getTypeColor(license.type)} variant="outline">
                      {license.type}
                    </Badge>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {/* Devices */}
                  <div>
                    <div className="flex items-center gap-2 mb-2">
                      <Users className="w-4 h-4 text-gray-500" />
                      <span className="text-sm font-medium text-gray-700">Geräte-Nutzung</span>
                    </div>
                    <p className="text-sm text-gray-600 mb-2">
                      {license.devices} von {license.maxDevices} Geräten genutzt
                    </p>
                    <Progress 
                      value={(license.devices / license.maxDevices) * 100} 
                      className="h-2"
                    />
                  </div>

                  {/* Valid Until */}
                  <div>
                    <div className="flex items-center gap-2 mb-2">
                      <AlertCircle className="w-4 h-4 text-gray-500" />
                      <span className="text-sm font-medium text-gray-700">Gültig bis</span>
                    </div>
                    <p className="text-lg font-semibold text-gray-900">{license.validUntil}</p>
                  </div>
                </div>

                {/* Actions */}
                <div className="flex flex-col sm:flex-row gap-2 pt-4 border-t">
                  <Button size="sm" variant="outline" className="flex-1" onClick={() => navigate("/downloads")}>
                    <Download className="w-4 h-4 mr-2" />
                    Herunterladen
                  </Button>
                  <Dialog>
                    <DialogTrigger asChild>
                      <Button size="sm" variant="outline" className="flex-1">
                        <History className="w-4 h-4 mr-2" />
                        Verlauf anzeigen
                      </Button>
                    </DialogTrigger>
                    <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
                      <DialogHeader>
                        <DialogTitle>Lizenz-Verlauf: {license.name}</DialogTitle>
                        <DialogDescription>
                          Alle Aktivitäten und Änderungen für diese Lizenz
                        </DialogDescription>
                      </DialogHeader>
                      <div className="space-y-4 mt-4">
                        {portal.licenseAuditLog
                          .filter(log => log.licenseId === license.id)
                          .map((log) => (
                            <div key={log.id} className="flex gap-4 pb-4 border-b last:border-0">
                              <div className="flex flex-col items-center">
                                <div className="w-2 h-2 rounded-full bg-blue-500 mt-2"></div>
                                <div className="w-px h-full bg-gray-200 mt-1"></div>
                              </div>
                              <div className="flex-1">
                                <p className="text-sm font-medium text-gray-900">{log.description}</p>
                                <p className="text-xs text-gray-500 mt-1">
                                  {new Date(log.timestamp).toLocaleDateString('de-DE', {
                                    day: '2-digit',
                                    month: '2-digit',
                                    year: 'numeric',
                                    hour: '2-digit',
                                    minute: '2-digit',
                                  })} • {log.user}
                                </p>
                              </div>
                            </div>
                          ))}
                      </div>
                    </DialogContent>
                  </Dialog>
                  <Button
                    size="sm"
                    className="flex-1 bg-blue-600 hover:bg-blue-700"
                    onClick={async () => {
                      try {
                        setRenewingLicenseId(license.id);
                        await requestLicenseRenewal(license.id);
                        await queryClient.invalidateQueries({ queryKey: ["portal-bootstrap"] });
                        toast.success("Verlängerung angefragt", {
                          description: `Für ${license.name} wurde eine Verlängerungsanfrage gespeichert.`,
                        });
                      } catch (error) {
                        toast.error("Anfrage fehlgeschlagen", {
                          description: error instanceof Error ? error.message : "Bitte erneut versuchen.",
                        });
                      } finally {
                        setRenewingLicenseId(null);
                      }
                    }}
                    disabled={renewingLicenseId === license.id}
                  >
                    <RefreshCw className="w-4 h-4 mr-2" />
                    {renewingLicenseId === license.id ? "Wird angefragt..." : "Verlängern"}
                  </Button>
                </div>
              </CardContent>
            </Card>
          </motion.div>
        ))
        )}
      </div>
    </div>
  );
}
