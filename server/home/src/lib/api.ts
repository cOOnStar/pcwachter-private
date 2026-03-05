const API_URL =
  process.env.API_INTERNAL_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "https://api.xn--pcwchter-2za.de";

export interface PlanItem {
  id: string;
  label: string;
  price_eur: number | null;
  duration_days: number | null;
  max_devices: number | null;
  is_active: boolean;
  sort_order: number;
  feature_flags: Record<string, boolean> | null;
  grace_period_days: number;
  stripe_price_id: string | null;
}

export async function getPlans(): Promise<PlanItem[]> {
  try {
    const res = await fetch(`${API_URL}/console/public/plans`, {
      next: { revalidate: 300 },
    });
    if (!res.ok) return [];
    const data = await res.json();
    return (data.items ?? []) as PlanItem[];
  } catch {
    return [];
  }
}

export async function getLicenseStatus(
  accessToken: string
): Promise<LicenseStatus | null> {
  try {
    const res = await fetch(`${API_URL}/license/status`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      cache: "no-store",
    });
    if (!res.ok) return null;
    return (await res.json()) as LicenseStatus;
  } catch {
    return null;
  }
}

export interface LicenseStatus {
  ok: boolean;
  plan: string;
  plan_label: string;
  state: string;
  expires_at: string | null;
  grace_period_until: string | null;
  days_remaining: number | null;
  max_devices: number | null;
  features: Record<string, boolean>;
}
