import { motion } from "motion/react";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  Bell,
  Camera,
  Fingerprint,
  Globe,
  Key,
  Mail,
  Phone,
  Save,
  Shield,
  Trash2,
  User,
} from "lucide-react";

import { PREVIEW_BOOTSTRAP, useAuth, usePortalBootstrap } from "../../hooks";
import { requestAccountDelete, updateHomeProfile } from "../../lib/api";
import { getKeycloak, IS_PREVIEW } from "../../lib/keycloak";
import { Button } from "../components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "../components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "../components/ui/dialog";
import { Input } from "../components/ui/input";
import { Label } from "../components/ui/label";
import { Switch } from "../components/ui/switch";

type ProfileFormState = {
  email: string;
  firstName: string;
  lastName: string;
  username: string;
  phone: string;
  preferredLanguage: string;
  preferredTimezone: string;
  emailNotificationsEnabled: boolean;
  licenseRemindersEnabled: boolean;
  supportUpdatesEnabled: boolean;
};

const DEFAULT_FORM: ProfileFormState = {
  email: "",
  firstName: "",
  lastName: "",
  username: "",
  phone: "",
  preferredLanguage: "de",
  preferredTimezone: "Europe/Berlin",
  emailNotificationsEnabled: true,
  licenseRemindersEnabled: true,
  supportUpdatesEnabled: true,
};

export function Profile() {
  const queryClient = useQueryClient();
  const { user: authUser, updateProfile } = useAuth();
  const { data } = usePortalBootstrap();
  const portal = data ?? PREVIEW_BOOTSTRAP;

  const [isEditing, setIsEditing] = useState(false);
  const [isSavingProfile, setIsSavingProfile] = useState(false);
  const [isSavingPreferences, setIsSavingPreferences] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [confirmText, setConfirmText] = useState("");
  const [emailError, setEmailError] = useState("");
  const [form, setForm] = useState<ProfileFormState>(DEFAULT_FORM);

  useEffect(() => {
    setForm({
      email: portal.user.email || authUser?.email || "",
      firstName: portal.user.firstName || authUser?.firstName || "",
      lastName: portal.user.lastName || authUser?.lastName || "",
      username: portal.user.username || authUser?.username || "",
      phone: portal.profileSettings.phone || "",
      preferredLanguage: portal.profileSettings.preferredLanguage || "de",
      preferredTimezone: portal.profileSettings.preferredTimezone || "Europe/Berlin",
      emailNotificationsEnabled: portal.profileSettings.emailNotificationsEnabled,
      licenseRemindersEnabled: portal.profileSettings.licenseRemindersEnabled,
      supportUpdatesEnabled: portal.profileSettings.supportUpdatesEnabled,
    });
  }, [
    authUser?.email,
    authUser?.firstName,
    authUser?.lastName,
    authUser?.username,
    portal.profileSettings.emailNotificationsEnabled,
    portal.profileSettings.licenseRemindersEnabled,
    portal.profileSettings.phone,
    portal.profileSettings.preferredLanguage,
    portal.profileSettings.preferredTimezone,
    portal.profileSettings.supportUpdatesEnabled,
    portal.user.email,
    portal.user.firstName,
    portal.user.lastName,
    portal.user.username,
  ]);

  const activeLicenses = portal.licenses.filter(
    (license) => license.status === "Aktiv" || license.status === "Läuft bald ab",
  );
  const hasActiveLicenses = activeLicenses.length > 0;
  const displayName = [form.firstName, form.lastName].filter(Boolean).join(" ");
  const initials = (form.firstName.charAt(0) || form.username.charAt(0) || form.email.charAt(0) || "?").toUpperCase();

  const getLatestExpiryDate = () => {
    if (activeLicenses.length === 0) {
      return "";
    }
    const dates = activeLicenses.map((license) => {
      const [day, month, year] = license.validUntil.split(".");
      return new Date(Number(year), Number(month) - 1, Number(day));
    });
    const latest = new Date(Math.max(...dates.map((date) => date.getTime())));
    return latest.toLocaleDateString("de-DE", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
    });
  };

  const setField = <K extends keyof ProfileFormState>(key: K, value: ProfileFormState[K]) => {
    setForm((current) => ({ ...current, [key]: value }));
  };

  const validateEmail = () => {
    if (!form.email.trim()) {
      setEmailError("E-Mail-Adresse ist ein Pflichtfeld.");
      return false;
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) {
      setEmailError("Bitte geben Sie eine gültige E-Mail-Adresse ein.");
      return false;
    }
    setEmailError("");
    return true;
  };

  const persistProfile = async (mode: "profile" | "preferences") => {
    if (!validateEmail()) {
      return false;
    }

    if (mode === "profile") {
      setIsSavingProfile(true);
    } else {
      setIsSavingPreferences(true);
    }

    try {
      if (!IS_PREVIEW) {
        await updateHomeProfile({
          email: form.email.trim(),
          username: form.username.trim() || null,
          first_name: form.firstName.trim() || null,
          last_name: form.lastName.trim() || null,
          phone: form.phone.trim() || null,
          preferred_language: form.preferredLanguage,
          preferred_timezone: form.preferredTimezone,
          email_notifications_enabled: form.emailNotificationsEnabled,
          license_reminders_enabled: form.licenseRemindersEnabled,
          support_updates_enabled: form.supportUpdatesEnabled,
        });
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: ["portal-bootstrap"] }),
          updateProfile(),
        ]);
      }

      toast.success(mode === "profile" ? "Profil gespeichert" : "Einstellungen gespeichert", {
        description:
          mode === "profile"
            ? "Ihre persönlichen Daten wurden erfolgreich aktualisiert."
            : "Ihre Portal-Einstellungen wurden übernommen.",
      });
      setIsEditing(false);
      return true;
    } catch (error) {
      toast.error("Speichern fehlgeschlagen", {
        description: error instanceof Error ? error.message : "Bitte erneut versuchen.",
      });
      return false;
    } finally {
      if (mode === "profile") {
        setIsSavingProfile(false);
      } else {
        setIsSavingPreferences(false);
      }
    }
  };

  const handleDeleteAccount = async () => {
    try {
      if (!IS_PREVIEW) {
        await requestAccountDelete(confirmText);
        await queryClient.invalidateQueries({ queryKey: ["portal-bootstrap"] });
      }
      toast.success("Account-Löschung beantragt", {
        description: "Ihr Account ist zur Löschung vorgemerkt und wird nach Ablauf der Frist entfernt.",
      });
      setDeleteDialogOpen(false);
      setConfirmText("");
    } catch (error) {
      toast.error("Löschanfrage fehlgeschlagen", {
        description: error instanceof Error ? error.message : "Bitte erneut versuchen.",
      });
    }
  };

  const handleChangePassword = () => {
    if (IS_PREVIEW) {
      toast.info("Passwort-Änderung", {
        description: "Im Vorschaumodus nicht verfügbar.",
      });
      return;
    }

    const keycloak = getKeycloak();
    if (typeof keycloak?.accountManagement === "function") {
      keycloak.accountManagement();
      return;
    }

    const keycloakUrl = import.meta.env.VITE_KEYCLOAK_URL || "https://login.xn--pcwchter-2za.de";
    const realm = import.meta.env.VITE_KEYCLOAK_REALM || "pcwaechter-prod";
    window.location.assign(`${keycloakUrl.replace(/\/+$/, "")}/realms/${realm}/account`);
  };

  return (
    <div className="p-4 md:p-6 space-y-6 max-w-5xl mx-auto">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
        <h1 className="text-2xl md:text-3xl font-bold text-gray-900">Benutzerprofil</h1>
        <p className="text-gray-600 mt-1">Verwalten Sie Ihre persönlichen Daten und Einstellungen</p>
      </motion.div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <motion.div
          initial={{ opacity: 0, x: -20 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ delay: 0.1 }}
          className="lg:col-span-1"
        >
          <Card>
            <CardContent className="p-6">
              <div className="flex flex-col items-center text-center space-y-4">
                <div className="relative">
                  <div className="w-32 h-32 bg-gradient-to-br from-blue-500 to-purple-600 rounded-full flex items-center justify-center text-white text-4xl font-bold">
                    {initials}
                  </div>
                  <button
                    type="button"
                    className="absolute bottom-0 right-0 p-2 bg-blue-600 text-white rounded-full hover:bg-blue-700 transition-colors shadow-lg"
                    onClick={() =>
                      toast.info("Avatar-Upload", {
                        description: "Profilbilder werden in diesem Portal noch nicht verwaltet.",
                      })
                    }
                  >
                    <Camera className="w-4 h-4" />
                  </button>
                </div>

                <div className="space-y-1">
                  <h2 className="text-xl font-bold text-gray-900">{displayName || form.username || form.email || "–"}</h2>
                  <p className="text-sm text-gray-600">{form.email || "–"}</p>
                </div>

                <div className="w-full pt-4 border-t">
                  <div className="grid grid-cols-2 gap-4 text-center">
                    <div>
                      <p className="text-2xl font-bold text-gray-900">{activeLicenses.length}</p>
                      <p className="text-xs text-gray-600">Aktive Lizenzen</p>
                    </div>
                    <div>
                      <p className="text-2xl font-bold text-gray-900">{portal.devices.length}</p>
                      <p className="text-xs text-gray-600">Geräte</p>
                    </div>
                  </div>
                </div>

                <div className="w-full pt-4 border-t">
                  <p className="text-xs text-gray-600">Account-Status</p>
                  <p className="text-sm font-semibold text-gray-900">
                    {portal.profileSettings.deletionScheduledFor ? "Löschung vorgemerkt" : "Aktiv"}
                  </p>
                </div>

                {portal.profileSettings.deletionScheduledFor ? (
                  <div className="w-full rounded-lg border border-yellow-200 bg-yellow-50 p-3 text-left text-sm text-yellow-800">
                    Geplante Löschung am{" "}
                    {new Date(portal.profileSettings.deletionScheduledFor).toLocaleDateString("de-DE", {
                      day: "2-digit",
                      month: "2-digit",
                      year: "numeric",
                    })}
                    .
                  </div>
                ) : null}
              </div>
            </CardContent>
          </Card>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, x: 20 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ delay: 0.1 }}
          className="lg:col-span-2 space-y-6"
        >
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <CardTitle className="flex items-center gap-2">
                  <User className="w-5 h-5 text-blue-600" />
                  Persönliche Informationen
                </CardTitle>
                {!isEditing ? (
                  <Button variant="outline" size="sm" onClick={() => setIsEditing(true)}>
                    Bearbeiten
                  </Button>
                ) : (
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" onClick={() => setIsEditing(false)}>
                      Abbrechen
                    </Button>
                    <Button
                      size="sm"
                      onClick={() => void persistProfile("profile")}
                      className="bg-blue-600 hover:bg-blue-700"
                      disabled={isSavingProfile}
                    >
                      <Save className="w-4 h-4 mr-2" />
                      {isSavingProfile ? "Speichert..." : "Speichern"}
                    </Button>
                  </div>
                )}
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="email">
                  E-Mail-Adresse <span className="text-red-500">*</span>
                </Label>
                <div className="relative">
                  <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                  <Input
                    id="email"
                    type="email"
                    value={form.email}
                    onChange={(event) => {
                      setField("email", event.target.value);
                      if (emailError) {
                        setEmailError("");
                      }
                    }}
                    disabled={!isEditing}
                    className={`pl-10 ${emailError ? "border-red-500 focus-visible:ring-red-500" : ""}`}
                  />
                </div>
                {emailError ? <p className="text-sm text-red-500">{emailError}</p> : null}
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="firstName">Vorname</Label>
                  <div className="relative">
                    <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <Input
                      id="firstName"
                      value={form.firstName}
                      onChange={(event) => setField("firstName", event.target.value)}
                      disabled={!isEditing}
                      className="pl-10"
                    />
                  </div>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="lastName">Nachname</Label>
                  <div className="relative">
                    <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <Input
                      id="lastName"
                      value={form.lastName}
                      onChange={(event) => setField("lastName", event.target.value)}
                      disabled={!isEditing}
                      className="pl-10"
                    />
                  </div>
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="username">Benutzername</Label>
                <div className="relative">
                  <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                  <Input
                    id="username"
                    value={form.username}
                    onChange={(event) => setField("username", event.target.value)}
                    disabled={!isEditing}
                    className="pl-10"
                    placeholder="Optional"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="phone">Telefonnummer</Label>
                <div className="relative">
                  <Phone className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                  <Input
                    id="phone"
                    type="tel"
                    value={form.phone}
                    onChange={(event) => setField("phone", event.target.value)}
                    disabled={!isEditing}
                    className="pl-10"
                    placeholder="Optional"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="userId">Benutzer-ID</Label>
                <div className="relative">
                  <Fingerprint className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                  <Input
                    id="userId"
                    value={portal.user.id || authUser?.id || "–"}
                    disabled
                    className="pl-10 font-mono text-gray-500 bg-gray-50"
                  />
                </div>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Shield className="w-5 h-5 text-green-600" />
                Sicherheit
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between p-4 bg-gray-50 rounded-lg gap-3">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-white rounded-lg">
                    <Key className="w-5 h-5 text-blue-600" />
                  </div>
                  <div>
                    <p className="font-semibold text-gray-900">Passwort ändern</p>
                    <p className="text-sm text-gray-600">Wird über Keycloak verwaltet</p>
                  </div>
                </div>
                <Button variant="outline" onClick={handleChangePassword}>
                  Ändern
                </Button>
              </div>

              <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between p-4 bg-gray-50 rounded-lg gap-3">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-white rounded-lg">
                    <Shield className="w-5 h-5 text-green-600" />
                  </div>
                  <div>
                    <p className="font-semibold text-gray-900">Zwei-Faktor-Authentifizierung</p>
                    <p className="text-sm text-gray-600">Aktivierung direkt in Keycloak</p>
                  </div>
                </div>
                <Switch
                  checked={false}
                  disabled
                  onCheckedChange={() => undefined}
                />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <div className="flex items-center justify-between gap-4">
                <CardTitle className="flex items-center gap-2">
                  <Bell className="w-5 h-5 text-yellow-600" />
                  Benachrichtigungen
                </CardTitle>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => void persistProfile("preferences")}
                  disabled={isSavingPreferences}
                >
                  <Save className="w-4 h-4 mr-2" />
                  {isSavingPreferences ? "Speichert..." : "Einstellungen speichern"}
                </Button>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex items-center justify-between py-3 border-b last:border-0">
                <div>
                  <p className="font-medium text-gray-900">E-Mail-Benachrichtigungen</p>
                  <p className="text-sm text-gray-600">Updates und Neuigkeiten per E-Mail erhalten</p>
                </div>
                <Switch
                  checked={form.emailNotificationsEnabled}
                  onCheckedChange={(checked) => setField("emailNotificationsEnabled", checked)}
                />
              </div>

              <div className="flex items-center justify-between py-3 border-b last:border-0">
                <div>
                  <p className="font-medium text-gray-900">Lizenz-Erinnerungen</p>
                  <p className="text-sm text-gray-600">Bei ablaufenden Lizenzen benachrichtigen</p>
                </div>
                <Switch
                  checked={form.licenseRemindersEnabled}
                  onCheckedChange={(checked) => setField("licenseRemindersEnabled", checked)}
                />
              </div>

              <div className="flex items-center justify-between py-3 border-b last:border-0">
                <div>
                  <p className="font-medium text-gray-900">Support-Updates</p>
                  <p className="text-sm text-gray-600">Über Antworten auf Ihre Tickets informieren</p>
                </div>
                <Switch
                  checked={form.supportUpdatesEnabled}
                  onCheckedChange={(checked) => setField("supportUpdatesEnabled", checked)}
                />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <div className="flex items-center justify-between gap-4">
                <CardTitle className="flex items-center gap-2">
                  <Globe className="w-5 h-5 text-purple-600" />
                  Sprache & Region
                </CardTitle>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => void persistProfile("preferences")}
                  disabled={isSavingPreferences}
                >
                  <Save className="w-4 h-4 mr-2" />
                  {isSavingPreferences ? "Speichert..." : "Speichern"}
                </Button>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="language">Sprache</Label>
                <select
                  id="language"
                  value={form.preferredLanguage}
                  onChange={(event) => setField("preferredLanguage", event.target.value)}
                  className="w-full px-4 py-2 border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                >
                  <option value="de">Deutsch</option>
                  <option value="en">English</option>
                  <option value="fr">Français</option>
                </select>
              </div>

              <div className="space-y-2">
                <Label htmlFor="timezone">Zeitzone</Label>
                <select
                  id="timezone"
                  value={form.preferredTimezone}
                  onChange={(event) => setField("preferredTimezone", event.target.value)}
                  className="w-full px-4 py-2 border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                >
                  <option value="Europe/Berlin">Europe/Berlin (MEZ)</option>
                  <option value="Europe/London">Europe/London (GMT)</option>
                  <option value="America/New_York">America/New York (EST)</option>
                </select>
              </div>
            </CardContent>
          </Card>

          <Card className="border-red-200">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-red-600">
                <Trash2 className="w-5 h-5" />
                Gefahrenzone
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between p-4 bg-red-50 rounded-lg border border-red-200 gap-3">
                <div>
                  <p className="font-semibold text-gray-900">Account löschen</p>
                  <p className="text-sm text-gray-600">Alle Ihre Daten werden dauerhaft gelöscht</p>
                </div>
                <Button
                  variant="outline"
                  className="border-red-300 text-red-600 hover:bg-red-50 w-full sm:w-auto"
                  onClick={() => setDeleteDialogOpen(true)}
                >
                  Account löschen
                </Button>
              </div>
            </CardContent>
          </Card>
        </motion.div>
      </div>

      <Dialog
        open={deleteDialogOpen}
        onOpenChange={(open) => {
          setDeleteDialogOpen(open);
          if (!open) {
            setConfirmText("");
          }
        }}
      >
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-red-600">
              <AlertTriangle className="w-5 h-5" />
              Account löschen
            </DialogTitle>
            <DialogDescription className="pt-2">
              {hasActiveLicenses ? (
                <div className="space-y-3">
                  <div className="p-3 bg-yellow-50 border border-yellow-200 rounded-lg">
                    <div className="flex items-start gap-2">
                      <AlertTriangle className="w-5 h-5 text-yellow-600 mt-0.5 shrink-0" />
                      <div>
                        <p className="font-semibold text-yellow-800">Achtung: Aktive Lizenzen vorhanden!</p>
                        <p className="text-sm text-yellow-700 mt-1">
                          Sie haben noch {activeLicenses.length} aktive Lizenz
                          {activeLicenses.length > 1 ? "en" : ""}, die bis zum{" "}
                          <span className="font-semibold">{getLatestExpiryDate()}</span> gültig
                          {activeLicenses.length > 1 ? " sind" : " ist"}.
                        </p>
                      </div>
                    </div>
                  </div>
                  <p className="text-sm text-gray-600">
                    Wenn Sie Ihren Account löschen, gehen{" "}
                    <span className="font-semibold text-red-600">alle Lizenzen unwiderruflich verloren</span>.
                  </p>
                </div>
              ) : (
                <p className="text-sm text-gray-600">
                  Sind Sie sicher, dass Sie Ihren Account löschen möchten? Alle Ihre Daten werden dauerhaft gelöscht.
                </p>
              )}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-2 pt-2">
            <Label htmlFor="confirmDelete" className="text-sm text-gray-700">
              Geben Sie <span className="font-mono font-semibold text-red-600">LÖSCHEN</span> ein, um zu bestätigen:
            </Label>
            <Input
              id="confirmDelete"
              value={confirmText}
              onChange={(event) => setConfirmText(event.target.value)}
              placeholder="LÖSCHEN"
              className="font-mono"
            />
          </div>

          <DialogFooter className="gap-2 sm:gap-0">
            <Button
              type="button"
              variant="outline"
              onClick={() => {
                setDeleteDialogOpen(false);
                setConfirmText("");
              }}
            >
              Abbrechen
            </Button>
            <Button
              type="button"
              className="bg-red-600 hover:bg-red-700"
              onClick={() => void handleDeleteAccount()}
              disabled={confirmText !== "LÖSCHEN"}
            >
              <Trash2 className="w-4 h-4 mr-2" />
              Account endgültig löschen
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
