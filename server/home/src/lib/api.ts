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
  price_version?: number;
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

export interface HomeDevice {
  device_install_id: string;
  host_name: string | null;
  os_name: string | null;
  os_version: string | null;
  last_seen_at: string | null;
  online: boolean;
  primary_ip: string | null;
}

export async function getDevices(accessToken: string): Promise<HomeDevice[]> {
  try {
    const res = await fetch(`${API_URL}/console/home/devices`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      cache: "no-store",
    });
    if (!res.ok) return [];
    const data = await res.json();
    return (data.items ?? []) as HomeDevice[];
  } catch {
    return [];
  }
}

export async function renameDevice(
  accessToken: string,
  deviceInstallId: string,
  name: string
): Promise<{ ok: boolean }> {
  const res = await fetch(
    `${API_URL}/console/home/devices/${encodeURIComponent(deviceInstallId)}/name`,
    {
      method: "PATCH",
      headers: {
        Authorization: `Bearer ${accessToken}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ name }),
    }
  );
  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`${res.status} ${text || res.statusText}`);
  }
  return res.json();
}

export async function revokeDevice(
  accessToken: string,
  deviceInstallId: string
): Promise<{ ok: boolean }> {
  const res = await fetch(
    `${API_URL}/console/home/devices/${encodeURIComponent(deviceInstallId)}`,
    {
      method: "DELETE",
      headers: { Authorization: `Bearer ${accessToken}` },
    }
  );
  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`${res.status} ${text || res.statusText}`);
  }
  return res.json();
}

export interface SupportTicketIn {
  title: string;
  body: string;
}

export async function createSupportTicket(
  accessToken: string,
  payload: SupportTicketIn
): Promise<{ id?: number | string }> {
  const res = await fetch(`${API_URL}/support/tickets`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });
  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`${res.status} ${text || res.statusText}`);
  }
  return res.json();
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
