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
  amount_cents: number | null;
  currency: string;
  price_version: number;
  stripe_product_id: string | null;
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

export interface AccountProfile {
  sub: string;
  email: string | null;
  first_name: string | null;
  last_name: string | null;
  name: string | null;
  email_verified: boolean | null;
  warnings?: string[];
}

export async function getAccountProfile(
  accessToken: string
): Promise<AccountProfile | null> {
  try {
    const res = await fetch(`${API_URL}/api/v1/me/profile`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      cache: "no-store",
    });
    if (!res.ok) return null;
    return (await res.json()) as AccountProfile;
  } catch {
    return null;
  }
}

export interface SupportTicketSummary {
  id: string;
  number: string | null;
  title: string | null;
  state: string | null;
  created_at: string | null;
  updated_at: string | null;
  last_contact_agent_at: string | null;
  last_contact_customer_at: string | null;
  article_count: number | null;
}

function asRecord(value: unknown): Record<string, unknown> | null {
  if (!value || typeof value !== "object" || Array.isArray(value)) return null;
  return value as Record<string, unknown>;
}

function asString(value: unknown): string | null {
  if (typeof value !== "string") return null;
  const trimmed = value.trim();
  return trimmed || null;
}

function asNumber(value: unknown): number | null {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string") {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
}

function normalizeSupportTicket(value: unknown): SupportTicketSummary | null {
  const record = asRecord(value);
  const id = record ? asString(record.id) ?? String(record.id ?? "") : "";
  if (!record || !id) return null;

  return {
    id,
    number: asString(record.number),
    title: asString(record.title),
    state: asString(record.state),
    created_at: asString(record.created_at),
    updated_at: asString(record.updated_at),
    last_contact_agent_at: asString(record.last_contact_agent_at),
    last_contact_customer_at: asString(record.last_contact_customer_at),
    article_count: asNumber(record.article_count),
  };
}

function normalizeSupportTickets(payload: unknown): SupportTicketSummary[] {
  if (Array.isArray(payload)) {
    return payload
      .map(normalizeSupportTicket)
      .filter((ticket): ticket is SupportTicketSummary => ticket !== null);
  }

  const record = asRecord(payload);
  if (!record) return [];

  if (Array.isArray(record.tickets) && record.tickets.every((ticket) => asRecord(ticket))) {
    return record.tickets
      .map(normalizeSupportTicket)
      .filter((ticket): ticket is SupportTicketSummary => ticket !== null);
  }

  const assets = asRecord(record.assets);
  const assetTickets = assets ? asRecord(assets.Ticket) : null;
  if (!assetTickets) return [];

  const recordIds = Array.isArray(record.record_ids)
    ? record.record_ids
    : Array.isArray(record.tickets)
      ? record.tickets
      : null;

  if (recordIds) {
    return recordIds
      .map((id) => {
        const key = typeof id === "string" ? id : String(id);
        return normalizeSupportTicket(assetTickets[key]);
      })
      .filter((ticket): ticket is SupportTicketSummary => ticket !== null);
  }

  return Object.values(assetTickets)
    .map(normalizeSupportTicket)
    .filter((ticket): ticket is SupportTicketSummary => ticket !== null);
}

export async function getSupportTickets(
  accessToken: string
): Promise<SupportTicketSummary[]> {
  try {
    const res = await fetch(`${API_URL}/api/v1/support/tickets?per_page=50`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      cache: "no-store",
    });
    if (!res.ok) return [];
    const data = await res.json();
    return normalizeSupportTickets(data);
  } catch {
    return [];
  }
}
