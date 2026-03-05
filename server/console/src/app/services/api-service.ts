import { getKeycloakToken } from "../context/auth-context";

const BASE = (import.meta.env.VITE_API_BASE_URL ?? "https://api.xn--pcwchter-2za.de/api/v1").replace(/\/$/, "");

async function authFetch(path: string, options: RequestInit = {}): Promise<Response> {
  const token = await getKeycloakToken();
  return fetch(`${BASE}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
      ...(options.headers ?? {}),
    },
  });
}

async function api<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await authFetch(path, options);
  if (!res.ok) {
    const text = await res.text().catch(() => "");
    const err = Object.assign(new Error(`${res.status} ${text || res.statusText}`), { status: res.status });
    throw err;
  }
  return res.json() as Promise<T>;
}

// ── Types ───────────────────────────────────────────────────────────────────

export interface PagedResponse<T> {
  items: T[];
  total: number;
}

export interface DashboardData {
  kpis: {
    totalDevices: number;
    onlineDevices: number;
    telemetry24h: number;
    totalLicenses: number;
    activeLicenses: number;
  };
  recentActivity: ActivityItem[];
}

export interface ActivityItem {
  id: string;
  type: string;
  user?: string;
  action: string;
  target?: string;
  description?: string;
  timestamp: string;
  category?: string;
  severity?: string;
}

export interface Device {
  id: string;
  hostname: string;
  os: string;
  agent: string;
  lastSeen: string;
  online: boolean;
  ip: string;
  blocked: boolean;
  desktopVersion: string | null;
  updaterVersion: string | null;
  updateChannel: string | null;
}

export interface DeviceDetail extends Device {
  deviceInstallId: string;
  createdAt: string;
  agentVersion: string | null;
  agentChannel: string | null;
  tokens?: { id: string; expiresAt: string | null; revokedAt: string | null; lastUsedAt: string | null }[];
}

export interface License {
  id: string;
  licenseKey?: string;
  tier: string;
  state: string;
  durationDays: number | null;
  issuedAt: string;
  activatedAt: string | null;
  expiresAt: string | null;
  activatedDeviceId: string | null;
  activatedByUserId: string | null;
  notes?: string | null;
}

export interface Plan {
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

export interface PlanStripeStatus {
  plan_id: string;
  stripe_product_id: string | null;
  stripe_price_id: string | null;
  price_version: number;
  amount_cents: number | null;
  currency: string;
  count_active_subs: number;
}

export interface PublishPriceResult {
  plan_id: string;
  old_price_id: string | null;
  new_price_id: string;
  migrated: number;
  failed: number;
  failed_subscription_ids: string[];
  mode: string;
  took_ms: number;
}

export interface FeatureOverride {
  id: string;
  feature_key: string;
  enabled: boolean;
  rollout_percent: number;
  scope: string;
  target_id: string | null;
  version_min: string | null;
  platform: string;
  notes: string | null;
  updated_at: string;
}

export interface Account {
  id: string;
  name: string;
  email: string;
  role: string;
  roleKeys: string[];
  status: string;
  created: string;
  lastLogin: string | null;
}

export interface TelemetryItem {
  id: string;
  category: string;
  device: string;
  receivedAt: string;
  summary: string;
  source: string;
  severity: string;
}

export interface ChartPoint {
  timestamp: string;
  device: string;
  summary: string;
  severity: string;
  id: string;
}

export interface AuditLogItem {
  id: string;
  time: string;
  actor: string;
  action: string;
  target: string;
  ip: string;
  result: string;
}

export interface ContainerInfo {
  name: string;
  status: string;
  image: string;
  cpuPercent: number;
  memoryMb: number;
}

export interface HostInfo {
  cpu_percent: number;
  memory: { total_mb: number; used_mb: number; percent: number };
  disk: { total_gb: number; used_gb: number; percent: number };
  uptime_seconds: number;
}

export interface Notification {
  id: string;
  type: string;
  severity: string;
  title: string;
  message: string;
  timestamp: string;
  read: boolean;
}

// ── Dashboard ───────────────────────────────────────────────────────────────

export async function getDashboard(): Promise<DashboardData> {
  return api("/console/ui/dashboard");
}

// ── Devices ─────────────────────────────────────────────────────────────────

export async function getDevices(params?: {
  search?: string;
  status?: string;
  limit?: number;
  offset?: number;
}): Promise<PagedResponse<Device>> {
  const q = new URLSearchParams();
  if (params?.search) q.set("search", params.search);
  if (params?.status) q.set("status", params.status);
  if (params?.limit) q.set("limit", String(params.limit));
  if (params?.offset) q.set("offset", String(params.offset));
  return api(`/console/ui/devices?${q}`);
}

export async function getDeviceDetail(deviceId: string): Promise<DeviceDetail> {
  return api(`/console/ui/devices/${encodeURIComponent(deviceId)}/detail`);
}

export async function blockDevice(deviceId: string): Promise<{ ok: boolean }> {
  return api(`/console/ui/devices/${encodeURIComponent(deviceId)}/block`, { method: "POST" });
}

export async function unblockDevice(deviceId: string): Promise<{ ok: boolean }> {
  return api(`/console/ui/devices/${encodeURIComponent(deviceId)}/unblock`, { method: "POST" });
}

// ── Licenses ─────────────────────────────────────────────────────────────────

export async function getLicenses(params?: {
  search?: string;
  state?: string;
  tier?: string;
  limit?: number;
  offset?: number;
}): Promise<PagedResponse<License>> {
  const q = new URLSearchParams();
  if (params?.search) q.set("search", params.search);
  if (params?.state) q.set("state", params.state);
  if (params?.tier) q.set("tier", params.tier);
  if (params?.limit) q.set("limit", String(params.limit));
  if (params?.offset) q.set("offset", String(params.offset));
  return api(`/console/ui/licenses?${q}`);
}

export async function generateLicenses(
  tier: string,
  quantity: number,
  durationDays?: number | null,
  notes?: string
): Promise<{ ok: boolean; licenses: License[] }> {
  return api("/console/ui/licenses/generate", {
    method: "POST",
    body: JSON.stringify({ tier, quantity, duration_days: durationDays ?? null, notes: notes ?? null }),
  });
}

export async function revokeLicense(key: string): Promise<{ ok: boolean }> {
  return api(`/console/ui/licenses/${encodeURIComponent(key)}/revoke`, { method: "POST" });
}

export async function blockLicense(key: string): Promise<{ ok: boolean }> {
  return api(`/console/ui/licenses/${encodeURIComponent(key)}/block`, { method: "POST" });
}

export async function unblockLicense(key: string): Promise<{ ok: boolean }> {
  return api(`/console/ui/licenses/${encodeURIComponent(key)}/unblock`, { method: "POST" });
}

export async function patchLicense(
  key: string,
  data: { expires_at?: string | null; notes?: string | null }
): Promise<{ ok: boolean }> {
  return api(`/console/ui/licenses/${encodeURIComponent(key)}`, {
    method: "PATCH",
    body: JSON.stringify(data),
  });
}

// ── Plans ─────────────────────────────────────────────────────────────────────

export async function getPlans(): Promise<PagedResponse<Plan>> {
  return api("/console/ui/plans");
}

export async function getPlan(planId: string): Promise<Plan> {
  const data = await getPlans();
  const plan = data.items.find((p) => p.id === planId);
  if (!plan) throw Object.assign(new Error("Plan nicht gefunden"), { status: 404 });
  return plan;
}

export async function upsertPlan(planId: string, data: Omit<Plan, "id">): Promise<Plan> {
  return api(`/console/ui/plans/${planId}`, {
    method: "PUT",
    body: JSON.stringify(data),
  });
}

export async function getPlanStripeStatus(planId: string): Promise<PlanStripeStatus> {
  return api(`/console/ui/plans/${encodeURIComponent(planId)}/stripe-status`);
}

export async function publishPlanPrice(
  planId: string,
  payload: { mode: "A"; dry_run: boolean }
): Promise<PublishPriceResult> {
  return api(`/console/ui/plans/${encodeURIComponent(planId)}/publish-price`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

// ── Feature Overrides ────────────────────────────────────────────────────────

export async function getFeatureOverrides(): Promise<PagedResponse<FeatureOverride>> {
  return api("/console/ui/features/overrides");
}

export async function upsertFeatureOverride(data: {
  feature_key: string;
  enabled: boolean;
  rollout_percent: number;
  scope?: string;
  target_id?: string | null;
  version_min?: string | null;
  platform?: string;
  notes?: string | null;
}): Promise<FeatureOverride> {
  return api("/console/ui/features/overrides", {
    method: "POST",
    body: JSON.stringify(data),
  });
}

export async function disableFeature(featureKey: string): Promise<{ ok: boolean }> {
  return api(`/console/ui/features/${encodeURIComponent(featureKey)}/disable`, { method: "POST" });
}

// ── Accounts ─────────────────────────────────────────────────────────────────

export async function getAccounts(params?: {
  search?: string;
  role?: string;
  status?: string;
  limit?: number;
  offset?: number;
}): Promise<PagedResponse<Account>> {
  const q = new URLSearchParams();
  if (params?.search) q.set("search", params.search);
  if (params?.role) q.set("role", params.role);
  if (params?.status) q.set("status", params.status);
  if (params?.limit) q.set("limit", String(params.limit));
  if (params?.offset) q.set("offset", String(params.offset));
  return api(`/console/ui/accounts?${q}`);
}

export async function updateAccountRole(accountId: string, role: string): Promise<{ id: string; role: string }> {
  return api(`/console/ui/accounts/${accountId}/role`, {
    method: "PATCH",
    body: JSON.stringify({ role }),
  });
}

// ── Telemetry ─────────────────────────────────────────────────────────────────

export async function getTelemetry(params?: {
  limit?: number;
  offset?: number;
  category?: string;
}): Promise<PagedResponse<TelemetryItem>> {
  const q = new URLSearchParams();
  if (params?.limit) q.set("limit", String(params.limit));
  if (params?.offset) q.set("offset", String(params.offset));
  if (params?.category) q.set("category", params.category);
  return api(`/console/ui/telemetry?${q}`);
}

export async function getTelemetryChart(
  category: string,
  hours = 24
): Promise<{ points: ChartPoint[]; total: number; category: string }> {
  return api(`/console/ui/telemetry/chart?category=${category}&hours=${hours}`);
}

// ── Audit Log ─────────────────────────────────────────────────────────────────

export async function getAuditLog(params?: {
  limit?: number;
  offset?: number;
}): Promise<PagedResponse<AuditLogItem>> {
  const q = new URLSearchParams();
  if (params?.limit) q.set("limit", String(params.limit));
  if (params?.offset) q.set("offset", String(params.offset));
  return api(`/console/ui/audit-log?${q}`);
}

// ── Notifications ─────────────────────────────────────────────────────────────

export async function getNotifications(): Promise<PagedResponse<Notification>> {
  return api("/console/ui/notifications");
}

export async function markNotificationRead(id: string): Promise<{ ok: boolean }> {
  return api(`/console/ui/notifications/${encodeURIComponent(id)}/read`, { method: "POST" });
}

// ── Server ────────────────────────────────────────────────────────────────────

export async function getContainers(): Promise<{ containers: ContainerInfo[] }> {
  return api("/console/ui/server/containers");
}

export async function getHostInfo(): Promise<HostInfo> {
  return api("/console/ui/server/host");
}

// ── Search ────────────────────────────────────────────────────────────────────

export async function search(q: string): Promise<{ items: unknown[]; total: number; query: string }> {
  return api(`/console/ui/search?q=${encodeURIComponent(q)}`);
}

// ── Activity Feed ─────────────────────────────────────────────────────────────

export async function getActivityFeed(params?: {
  limit?: number;
  offset?: number;
}): Promise<PagedResponse<ActivityItem>> {
  const q = new URLSearchParams();
  if (params?.limit) q.set("limit", String(params.limit));
  if (params?.offset) q.set("offset", String(params.offset));
  return api(`/console/ui/activity-feed?${q}`);
}

// ── Knowledge Base ────────────────────────────────────────────────────────────

export interface KbArticle {
  id: string;
  title: string;
  category: string;
  tags: string[];
  updated_at: string;
  summary?: string;
}

export async function getKnowledgeBase(params?: {
  search?: string;
  category?: string;
  limit?: number;
  offset?: number;
}): Promise<PagedResponse<KbArticle>> {
  const q = new URLSearchParams();
  if (params?.search) q.set("search", params.search);
  if (params?.category) q.set("category", params.category);
  if (params?.limit) q.set("limit", String(params.limit));
  if (params?.offset) q.set("offset", String(params.offset));
  return api(`/console/ui/knowledge-base?${q}`);
}

// ── Support ───────────────────────────────────────────────────────────────────

export interface SupportTicket {
  id: string;
  subject: string;
  state: string;
  priority: string;
  created_at: string;
  updated_at: string;
  customer_name?: string;
  customer_email?: string;
}

export interface SupportTicketDetail extends SupportTicket {
  description?: string;
  replies: SupportReply[];
  attachments: SupportAttachment[];
}

export interface SupportReply {
  id: string;
  body: string;
  author: string;
  created_at: string;
  internal: boolean;
}

export interface SupportAttachment {
  id: string;
  filename: string;
  size: number;
  created_at: string;
}

export async function listSupportTickets(params?: {
  all?: boolean;
  page?: number;
  perPage?: number;
}): Promise<PagedResponse<SupportTicket>> {
  const q = new URLSearchParams();
  if (params?.all) q.set("all", "true");
  if (params?.page) q.set("page", String(params.page));
  if (params?.perPage) q.set("per_page", String(params.perPage));
  return api(`/support/tickets?${q}`);
}

export async function getSupportTicket(id: string): Promise<SupportTicketDetail> {
  return api(`/support/tickets/${encodeURIComponent(id)}`);
}

export async function replySupportTicket(id: string, body: string): Promise<{ ok: boolean }> {
  return api(`/support/tickets/${encodeURIComponent(id)}/reply`, {
    method: "POST",
    body: JSON.stringify({ body }),
  });
}

export async function uploadSupportAttachment(file: File): Promise<{ id: string; filename: string }> {
  const token = await getKeycloakToken();
  const formData = new FormData();
  formData.append("file", file);
  const res = await fetch(`${BASE}/support/attachments`, {
    method: "POST",
    headers: { Authorization: `Bearer ${token}` },
    body: formData,
  });
  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw Object.assign(new Error(`${res.status} ${text || res.statusText}`), { status: res.status });
  }
  return res.json();
}

export async function diagZammadRoles(): Promise<{ roles: string[]; user_id?: string }> {
  return api("/support/admin/diag/zammad-roles");
}

export async function diagZammadUser(email: string): Promise<{ zammad_user_id: number | null; email: string }> {
  return api(`/support/admin/diag/zammad-user?email=${encodeURIComponent(email)}`);
}

// ── Device Update Channel ─────────────────────────────────────────────────────

export async function setDeviceUpdateChannel(
  deviceId: string,
  channel: "stable" | "beta" | "internal"
): Promise<{ ok: boolean; update_channel: string }> {
  return api(`/console/ui/devices/${encodeURIComponent(deviceId)}/update-channel`, {
    method: "PATCH",
    body: JSON.stringify({ update_channel: channel }),
  });
}
