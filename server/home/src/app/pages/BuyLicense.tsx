import { motion } from "motion/react";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import { ArrowRight, Check, Shield, Sparkles, TrendingUp, Zap } from "lucide-react";

import { PREVIEW_BOOTSTRAP, usePortalBootstrap } from "../../hooks";
import { createCheckoutSession } from "../../lib/api";
import { IS_PREVIEW } from "../../lib/keycloak";
import { Badge } from "../components/ui/badge";
import { Button } from "../components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "../components/ui/card";

const PLAN_COPY: Record<string, { description: string; features: string[]; icon: typeof Zap; highlight?: boolean; color: string; bgColor: string }> = {
  standard: {
    description: "Für Privatnutzer, die kontinuierliche Systemgesundheit möchten.",
    features: [
      "Kontinuierliche Systemanalyse",
      "Manuelle Problemlösung mit 1-Klick-Fixes",
      "Speicher- & Update-Überwachung",
      "Sicherheits-Score",
      "Monatsbericht",
      "Zugriff auf Cloud-Wissensdatenbank",
    ],
    icon: Zap,
    color: "text-green-600",
    bgColor: "bg-green-50",
  },
  professional: {
    description: "Für anspruchsvolle Nutzer und kleine Büros.",
    features: [
      "Alles aus Standard plus",
      "Automatische Problemlösung (Auto-Fix)",
      "Frühwarnsystem bei kritischen Ereignissen",
      "Erweiterte Cloud-Intelligenz",
      "Trendanalyse über Monate",
      "Geplanter Wartungsmodus",
      "Support-Paket Export",
    ],
    icon: TrendingUp,
    highlight: true,
    color: "text-blue-600",
    bgColor: "bg-blue-50",
  },
};

export function BuyLicense() {
  const { data } = usePortalBootstrap();
  const portal = data ?? PREVIEW_BOOTSTRAP;
  const [pendingPlanId, setPendingPlanId] = useState<string | null>(null);

  const plans = useMemo(
    () =>
      portal.plans
        .filter((plan) => plan.is_active && plan.id !== "trial")
        .sort((left, right) => left.sort_order - right.sort_order)
        .map((plan) => {
          const key = plan.id.toLowerCase();
          const copy = PLAN_COPY[key] ?? {
            description: `${plan.label} für den produktiven Einsatz von PC-Wächter.`,
            features: [
              `Bis zu ${plan.max_devices ?? "mehrere"} Geräte`,
              plan.duration_days ? `${plan.duration_days} Tage Laufzeit` : "Unbegrenzte Laufzeit",
              "Portal- und Support-Zugriff",
            ],
            icon: Zap,
            color: "text-gray-700",
            bgColor: "bg-gray-50",
          };
          return { ...plan, ...copy };
        }),
    [portal.plans],
  );

  const handlePurchaseClick = async (planId: string, planLabel: string) => {
    try {
      if (IS_PREVIEW) {
        toast.info("Vorschaumodus", { description: `${planLabel} würde jetzt zur Kasse weitergeleitet.` });
        return;
      }
      setPendingPlanId(planId);
      const successUrl = `${window.location.origin}/licenses?checkout=success`;
      const cancelUrl = `${window.location.origin}/licenses/buy?checkout=cancel`;
      const session = await createCheckoutSession(planId, successUrl, cancelUrl);
      window.location.assign(session.checkout_url);
    } catch (error) {
      toast.error("Checkout konnte nicht gestartet werden", { description: error instanceof Error ? error.message : "Bitte erneut versuchen." });
    } finally {
      setPendingPlanId(null);
    }
  };

  return (
    <div className="p-4 md:p-6 space-y-8 max-w-7xl mx-auto">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="text-center space-y-4">
        <div className="inline-flex items-center gap-2 bg-gradient-to-r from-blue-100 to-purple-100 text-blue-700 px-4 py-2 rounded-full text-sm font-medium">
          <Sparkles className="w-4 h-4" />
          Lizenz kaufen
        </div>
        <h1 className="text-3xl md:text-4xl font-bold text-gray-900">Wählen Sie die passende Edition</h1>
        <p className="text-lg text-gray-600 max-w-2xl mx-auto">Schützen Sie Ihre IT-Infrastruktur mit PC-Wächter. Wählen Sie das passende Paket für Ihre Anforderungen.</p>
      </motion.div>

      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }} className="flex flex-wrap justify-center gap-6 py-4">
        <div className="flex items-center gap-2 text-sm text-gray-600"><Shield className="w-5 h-5 text-green-600" /><span>100% Datenschutz-konform</span></div>
        <div className="flex items-center gap-2 text-sm text-gray-600"><Check className="w-5 h-5 text-green-600" /><span>30 Tage Geld-zurück-Garantie</span></div>
        <div className="flex items-center gap-2 text-sm text-gray-600"><Check className="w-5 h-5 text-green-600" /><span>Sofortige Aktivierung</span></div>
      </motion.div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-8 max-w-5xl mx-auto">
        {plans.length > 0 ? (
          plans.map((plan, index) => (
            <motion.div key={plan.id} initial={{ opacity: 0, y: 30 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.2 + index * 0.1 }}>
              <Card className={`relative h-full hover:shadow-xl transition-all ${plan.highlight ? "border-blue-500 border-2 shadow-lg" : ""}`}>
                {plan.highlight ? (
                  <div className="absolute -top-4 left-1/2 -translate-x-1/2">
                    <Badge className="bg-gradient-to-r from-blue-600 to-purple-600 text-white px-4 py-1">Beliebteste Wahl</Badge>
                  </div>
                ) : null}
                <CardHeader>
                  <div className="flex items-center gap-3 mb-4">
                    <div className={`p-3 rounded-lg ${plan.bgColor}`}>
                      <plan.icon className={`w-6 h-6 ${plan.color}`} />
                    </div>
                    <div><CardTitle className="text-2xl">{plan.label}</CardTitle></div>
                  </div>
                  <p className="text-gray-600">{plan.description}</p>
                </CardHeader>
                <CardContent className="space-y-6">
                  <div>
                    <div className="flex items-baseline gap-2">
                      <span className="text-4xl font-bold text-gray-900">
                        {plan.amount_cents ? `${(plan.amount_cents / 100).toFixed(2).replace(".", ",")} €` : "Preis auf Anfrage"}
                      </span>
                    </div>
                    <p className="text-sm text-gray-600 mt-1">
                      {plan.duration_days ? `${plan.duration_days} Tage Laufzeit` : "Unbegrenzte Laufzeit"} · bis zu {plan.max_devices ?? "mehrere"} Geräte
                    </p>
                  </div>
                  <div className="space-y-3">
                    {plan.features.map((feature) => (
                      <div key={`${plan.id}-${feature}`} className="flex items-start gap-3">
                        <Check className="w-5 h-5 text-green-600 flex-shrink-0 mt-0.5" />
                        <span className="text-sm text-gray-700">{feature}</span>
                      </div>
                    ))}
                  </div>
                  <Button
                    className={`w-full ${plan.highlight ? "bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700" : "bg-gray-900 hover:bg-gray-800"}`}
                    size="lg"
                    onClick={() => void handlePurchaseClick(plan.id, plan.label)}
                    disabled={pendingPlanId === plan.id}
                  >
                    {pendingPlanId === plan.id ? "Weiterleitung..." : "Jetzt kaufen"}
                    <ArrowRight className="w-4 h-4 ml-2" />
                  </Button>
                </CardContent>
              </Card>
            </motion.div>
          ))
        ) : (
          <Card className="md:col-span-2">
            <CardContent className="p-12 text-center">
              <p className="text-lg font-semibold text-gray-900">Keine buchbaren Pläne verfügbar</p>
              <p className="text-sm text-gray-600 mt-2">Sobald im Backend aktive Pläne hinterlegt sind, erscheinen sie hier automatisch.</p>
            </CardContent>
          </Card>
        )}
      </div>

      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.5 }} className="max-w-4xl mx-auto">
        <Card>
          <CardHeader><CardTitle className="text-center">Häufig gestellte Fragen</CardTitle></CardHeader>
          <CardContent className="space-y-6">
            <div><h3 className="font-semibold text-gray-900 mb-2">Kann ich jederzeit upgraden?</h3><p className="text-gray-600 text-sm">Ja. Planwechsel und Verlängerungen lassen sich jederzeit über das Kundenkonto anstoßen.</p></div>
            <div><h3 className="font-semibold text-gray-900 mb-2">Wie erfolgt die Aktivierung?</h3><p className="text-gray-600 text-sm">Nach erfolgreichem Checkout wird die Lizenz automatisch Ihrem Konto zugeordnet und kann direkt Geräten zugewiesen werden.</p></div>
            <div><h3 className="font-semibold text-gray-900 mb-2">Wie funktioniert die Verlängerung?</h3><p className="text-gray-600 text-sm">Laufende Pläne können vor Ablauf erneut gekauft oder über das Abrechnungsportal verwaltet werden.</p></div>
          </CardContent>
        </Card>
      </motion.div>
    </div>
  );
}
