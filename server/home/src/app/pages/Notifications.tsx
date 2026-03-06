import { motion } from "motion/react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { AlertCircle, Bell, Check, CheckCircle, Info, Trash2, X } from "lucide-react";

import { PREVIEW_BOOTSTRAP, usePortalBootstrap } from "../../hooks";
import {
  clearNotifications,
  deleteNotification as deleteNotificationRequest,
  markAllNotificationsRead,
  markNotificationRead,
} from "../../lib/api";
import { IS_PREVIEW } from "../../lib/keycloak";
import { Button } from "../components/ui/button";
import { Badge } from "../components/ui/badge";
import { Card, CardContent } from "../components/ui/card";

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

export function Notifications() {
  const queryClient = useQueryClient();
  const { data } = usePortalBootstrap();
  const portal = data ?? PREVIEW_BOOTSTRAP;
  const notifications = portal.notifications;
  const unreadCount = notifications.filter((notification) => !notification.read).length;

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["portal-bootstrap"] });

  const handleMarkAsRead = async (id: string) => {
    try {
      if (!IS_PREVIEW) {
        await markNotificationRead(id);
        await refresh();
      }
      toast.success("Als gelesen markiert");
    } catch (error) {
      toast.error("Aktion fehlgeschlagen", { description: error instanceof Error ? error.message : "Bitte erneut versuchen." });
    }
  };

  const handleMarkAllAsRead = async () => {
    try {
      if (!IS_PREVIEW) {
        await markAllNotificationsRead();
        await refresh();
      }
      toast.success("Alle als gelesen markiert");
    } catch (error) {
      toast.error("Aktion fehlgeschlagen", { description: error instanceof Error ? error.message : "Bitte erneut versuchen." });
    }
  };

  const handleDeleteNotification = async (id: string) => {
    try {
      if (!IS_PREVIEW) {
        await deleteNotificationRequest(id);
        await refresh();
      }
      toast.success("Benachrichtigung gelöscht");
    } catch (error) {
      toast.error("Löschen fehlgeschlagen", { description: error instanceof Error ? error.message : "Bitte erneut versuchen." });
    }
  };

  const handleClearAll = async () => {
    try {
      if (!IS_PREVIEW) {
        await clearNotifications();
        await refresh();
      }
      toast.success("Alle Benachrichtigungen gelöscht");
    } catch (error) {
      toast.error("Löschen fehlgeschlagen", { description: error instanceof Error ? error.message : "Bitte erneut versuchen." });
    }
  };

  const getNotificationIcon = (type: string) => {
    if (type === "warning") return AlertCircle;
    if (type === "success") return CheckCircle;
    if (type === "info") return Info;
    return Bell;
  };

  const getNotificationColor = (type: string) => {
    if (type === "warning") return { bg: "bg-yellow-50", text: "text-yellow-600" };
    if (type === "success") return { bg: "bg-green-50", text: "text-green-600" };
    if (type === "info") return { bg: "bg-blue-50", text: "text-blue-600" };
    return { bg: "bg-gray-50", text: "text-gray-600" };
  };

  return (
    <div className="p-4 md:p-6 space-y-6 max-w-4xl mx-auto">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-2xl md:text-3xl font-bold text-gray-900 flex items-center gap-3 flex-wrap">
              <Bell className="w-7 h-7 md:w-8 md:h-8 text-blue-600" />
              Benachrichtigungen
              {unreadCount > 0 ? <Badge className="bg-red-500 text-white">{unreadCount} neu</Badge> : null}
            </h1>
            <p className="text-gray-600 mt-1">Bleiben Sie auf dem Laufenden</p>
          </div>
          {notifications.length > 0 ? (
            <div className="flex gap-2">
              {unreadCount > 0 ? (
                <Button variant="outline" onClick={() => void handleMarkAllAsRead()}>
                  <Check className="w-4 h-4 mr-2" />
                  Alle als gelesen
                </Button>
              ) : null}
              <Button variant="outline" onClick={() => void handleClearAll()} className="text-red-600 hover:text-red-700">
                <Trash2 className="w-4 h-4 mr-2" />
                Alle löschen
              </Button>
            </div>
          ) : null}
        </div>
      </motion.div>

      {notifications.length > 0 ? (
        <div className="space-y-3">
          {notifications.map((notification, index) => {
            const NotificationIcon = getNotificationIcon(notification.type);
            const colors = getNotificationColor(notification.type);
            return (
              <motion.div key={notification.id} initial={{ opacity: 0, x: -20 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: index * 0.05 }}>
                <Card className={`${!notification.read ? "border-l-4 border-l-blue-500 shadow-md" : ""} hover:shadow-lg transition-shadow`}>
                  <CardContent className="p-4">
                    <div className="flex gap-4">
                      <div className={`p-3 rounded-lg ${colors.bg} flex-shrink-0`}>
                        <NotificationIcon className={`w-6 h-6 ${colors.text}`} />
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-start justify-between gap-4 mb-2">
                          <div>
                            <h3 className={`font-semibold ${!notification.read ? "text-gray-900" : "text-gray-700"}`}>{notification.title}</h3>
                            <p className="text-sm text-gray-600 mt-1">{notification.message}</p>
                          </div>
                          {!notification.read ? <div className="w-2 h-2 bg-blue-600 rounded-full flex-shrink-0 mt-2" /> : null}
                        </div>
                        <div className="flex items-center justify-between gap-3">
                          <span className="text-xs text-gray-500">{formatRelativeTime(notification.timestamp)}</span>
                          <div className="flex gap-2">
                            {!notification.read ? (
                              <Button size="sm" variant="ghost" onClick={() => void handleMarkAsRead(notification.id)} className="h-8">
                                <Check className="w-4 h-4 mr-1" />
                                Als gelesen
                              </Button>
                            ) : null}
                            <Button size="sm" variant="ghost" onClick={() => void handleDeleteNotification(notification.id)} className="h-8 text-red-600 hover:text-red-700 hover:bg-red-50">
                              <X className="w-4 h-4" />
                            </Button>
                          </div>
                        </div>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              </motion.div>
            );
          })}
        </div>
      ) : (
        <motion.div initial={{ opacity: 0, scale: 0.9 }} animate={{ opacity: 1, scale: 1 }}>
          <Card>
            <CardContent className="p-12">
              <div className="flex flex-col items-center justify-center text-center">
                <div className="w-20 h-20 bg-gray-100 rounded-full flex items-center justify-center mb-4">
                  <Bell className="w-10 h-10 text-gray-400" />
                </div>
                <h3 className="text-xl font-semibold text-gray-900 mb-2">Keine Benachrichtigungen</h3>
                <p className="text-gray-600 max-w-md">Neue System-, Lizenz- und Support-Hinweise werden hier angezeigt.</p>
              </div>
            </CardContent>
          </Card>
        </motion.div>
      )}
    </div>
  );
}
