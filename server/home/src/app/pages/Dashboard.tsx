import { Card, CardContent, CardHeader, CardTitle } from "../components/ui/card";
import { Button } from "../components/ui/button";
import { 
  Activity, 
  Key, 
  AlertTriangle, 
  Download,
  TrendingUp,
  CheckCircle,
  Clock,
  ArrowRight
} from "lucide-react";
import { motion } from "motion/react";
import { useNavigate } from "react-router";
import { PREVIEW_BOOTSTRAP, usePortalBootstrap } from "../../hooks";

export function Dashboard() {
  const navigate = useNavigate();
  const { data } = usePortalBootstrap();
  const portal = data ?? PREVIEW_BOOTSTRAP;
  
  // Statistiken
  const activeLicenses = portal.licenses.filter(l => l.status === 'Aktiv').length;
  const expiringLicenses = portal.licenses.filter(l => l.status === 'Läuft bald ab').length;
  const openTickets = portal.supportTickets.filter(t => t.status === 'Offen' || t.status === 'In Bearbeitung' || t.status === 'Warten auf Antwort').length;
  const totalDevices = portal.devices.length;
  const totalSlots = portal.licenses.reduce((sum, l) => sum + l.maxDevices, 0);
  const usedSlots = portal.licenses.reduce((sum, l) => sum + l.devices, 0);

  // Calculate days until next expiring license
  const getExpiringDays = () => {
    const expiring = portal.licenses.filter(l => l.status === 'Läuft bald ab');
    if (expiring.length === 0) return null;
    const now = new Date('2026-03-06');
    const dates = expiring.map(l => {
      const parts = l.validUntil.split('.');
      return new Date(parseInt(parts[2]), parseInt(parts[1]) - 1, parseInt(parts[0]));
    });
    const nearest = new Date(Math.min(...dates.map(d => d.getTime())));
    return Math.ceil((nearest.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));
  };

  const expiringDays = getExpiringDays();

  // Count recent support responses (messages from support in last 7 days)
  const recentSupportResponses = portal.supportTickets.reduce((count, t) => {
    return count + (t.messages?.filter(m => {
      if (!m.isSupport) return false;
      const msgDate = new Date(m.timestamp);
      const cutoff = new Date('2026-02-27T00:00:00');
      return msgDate >= cutoff;
    }).length || 0);
  }, 0);

  const formatDateTime = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString('de-DE', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const getRelativeTime = (dateStr: string) => {
    const now = new Date();
    const date = new Date(dateStr);
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 1) return 'gerade eben';
    if (diffMins < 60) return `vor ${diffMins} Min.`;
    if (diffHours < 24) return `vor ${diffHours} Std.`;
    return `vor ${diffDays} Tag${diffDays > 1 ? 'en' : ''}`;
  };

  const stats = [
    {
      title: "Aktive Lizenzen",
      value: activeLicenses,
      icon: Key,
      color: "text-blue-600",
      bgColor: "bg-blue-50",
      trend: `${portal.licenses.length} Lizenzen gesamt`,
      trendUp: true,
      borderColor: "border-blue-500",
      iconColor: "text-blue-600",
      href: "/licenses",
    },
    {
      title: "Registrierte Geräte",
      value: totalDevices,
      icon: Activity,
      color: "text-green-600",
      bgColor: "bg-green-50",
      trend: `${usedSlots} von ${totalSlots} Plätzen belegt`,
      trendUp: true,
      borderColor: "border-green-500",
      iconColor: "text-green-600",
      href: "/devices",
    },
    {
      title: "Läuft bald ab",
      value: expiringLicenses,
      icon: Clock,
      color: "text-yellow-600",
      bgColor: "bg-yellow-50",
      trend: expiringDays !== null ? `In ${expiringDays} Tagen` : "–",
      trendUp: false,
      borderColor: "border-yellow-500",
      iconColor: "text-yellow-600",
      href: "/licenses",
    },
    {
      title: "Offene Tickets",
      value: openTickets,
      icon: AlertTriangle,
      color: "text-red-600",
      bgColor: "bg-red-50",
      trend: recentSupportResponses > 0 ? `${recentSupportResponses} neue Antwort${recentSupportResponses > 1 ? 'en' : ''}` : "Keine neuen Antworten",
      trendUp: false,
      borderColor: "border-red-500",
      iconColor: "text-red-600",
      href: "/support",
    },
  ];

  const quickActions = [
    {
      title: "Neue Lizenz kaufen",
      description: "Erweitern Sie Ihre PC-Wächter Installation",
      icon: Key,
      color: "text-purple-600",
      bgColor: "bg-purple-50",
      action: () => navigate('/licenses/buy'),
    },
    {
      title: "Support kontaktieren",
      description: "Erstellen Sie ein neues Support-Ticket",
      icon: AlertTriangle,
      color: "text-blue-600",
      bgColor: "bg-blue-50",
      action: () => navigate('/support'),
    },
    {
      title: "Software herunterladen",
      description: "Laden Sie die neueste Version herunter",
      icon: Download,
      color: "text-green-600",
      bgColor: "bg-green-50",
      action: () => navigate('/downloads'),
    },
  ];

  const recentActivity = (data?.recentActivity ?? [
    {
      type: "license",
      title: "Calvin-PC aktiviert",
      description: "Lizenz Professional – Gerät hinzugefügt",
      timestamp: "2026-03-06T10:00:00",
    },
    {
      type: "ticket",
      title: "Support-Antwort erhalten",
      description: "Ticket #3 – Performance-Probleme nach Update",
      timestamp: "2026-03-02T16:30:00",
    },
    {
      type: "update",
      title: "Neue Version verfügbar",
      description: "PC-Wächter v2.6.0 steht zum Download bereit",
      timestamp: "2026-03-04T10:00:00",
    },
  ]).map((activity) => ({
    ...activity,
    icon:
      activity.type === "license"
        ? CheckCircle
        : activity.type === "warning" || activity.type === "ticket"
        ? AlertTriangle
        : TrendingUp,
    color:
      activity.type === "license"
        ? "text-green-600"
        : activity.type === "warning" || activity.type === "ticket"
        ? "text-blue-600"
        : "text-purple-600",
  }));

  return (
    <div className="p-4 md:p-6 space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl md:text-3xl font-bold text-gray-900">Dashboard</h1>
        <p className="text-gray-600 mt-2">
          Hier ist eine Übersicht über Ihre PC-Wächter Installation
        </p>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {stats.map((stat, index) => (
          <motion.div
            key={stat.title}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: index * 0.1 }}
            className="h-full"
          >
            <Card
              className={`border-l-4 ${stat.borderColor} hover:shadow-lg transition-all cursor-pointer h-full`}
              onClick={() => navigate(stat.href)}
            >
              <CardContent className="p-4">
                <div className="flex items-center gap-3">
                  <div className={`p-2 ${stat.bgColor} rounded-lg`}>
                    <stat.icon className={`w-5 h-5 ${stat.iconColor}`} />
                  </div>
                  <div>
                    <p className="text-xs text-gray-600">{stat.title}</p>
                    <p className="text-xl font-bold text-gray-900">{stat.value}</p>
                    {stat.trend && (
                      <p className="text-xs text-gray-500 mt-0.5">{stat.trend}</p>
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>
          </motion.div>
        ))}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Quick Actions */}
        <motion.div
          initial={{ opacity: 0, x: -20 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ delay: 0.4 }}
          className="lg:col-span-2"
        >
          <Card>
            <CardHeader>
              <CardTitle>Schnellzugriff</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {quickActions.map((action, index) => (
                <motion.div
                  key={action.title}
                  initial={{ opacity: 0, x: -10 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ delay: 0.5 + index * 0.1 }}
                >
                  <button
                    onClick={action.action}
                    className="w-full flex items-center gap-4 p-4 rounded-lg border border-gray-200 hover:border-blue-300 hover:bg-blue-50 transition-all group"
                  >
                    <div className={`p-3 rounded-lg ${action.bgColor} group-hover:scale-110 transition-transform`}>
                      <action.icon className={`w-5 h-5 ${action.color}`} />
                    </div>
                    <div className="flex-1 text-left">
                      <p className="font-semibold text-gray-900">{action.title}</p>
                      <p className="text-sm text-gray-600">{action.description}</p>
                    </div>
                    <ArrowRight className="w-5 h-5 text-gray-400 group-hover:text-blue-600 group-hover:translate-x-1 transition-all" />
                  </button>
                </motion.div>
              ))}
            </CardContent>
          </Card>
        </motion.div>

        {/* Recent Activity */}
        <motion.div
          initial={{ opacity: 0, x: 20 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ delay: 0.4 }}
        >
          <Card>
            <CardHeader>
              <CardTitle>Letzte Aktivitäten</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {recentActivity.map((activity, index) => (
                <motion.div
                  key={activity.title}
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.6 + index * 0.1 }}
                  className="flex gap-3 pb-4 border-b last:border-0 last:pb-0"
                >
                  <div className={`p-2 rounded-lg bg-gray-50 h-fit`}>
                    <activity.icon className={`w-4 h-4 ${activity.color}`} />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-gray-900 truncate">
                      {activity.title}
                    </p>
                    <p className="text-xs text-gray-600 truncate">
                      {activity.description}
                    </p>
                    <p className="text-xs text-gray-500 mt-1">
                      {getRelativeTime(activity.timestamp)}
                    </p>
                  </div>
                </motion.div>
              ))}
            </CardContent>
          </Card>
        </motion.div>
      </div>

      {/* System Status */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.7 }}
      >
        <Card>
          <CardHeader>
            <CardTitle>System-Status</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {portal.systemStatus.map((system, index) => (
                <motion.div
                  key={system.name}
                  initial={{ opacity: 0, scale: 0.9 }}
                  animate={{ opacity: 1, scale: 1 }}
                  transition={{ delay: 0.8 + index * 0.1 }}
                  className="flex items-center gap-3 p-4 rounded-lg bg-green-50 border border-green-200"
                >
                  <div className="w-2 h-2 rounded-full bg-green-500" />
                  <div>
                    <p className="font-medium text-gray-900">{system.name}</p>
                    <p className="text-xs text-gray-600">{system.description}</p>
                  </div>
                </motion.div>
              ))}
            </div>
          </CardContent>
        </Card>
      </motion.div>
    </div>
  );
}
