// User Types
export interface User {
  id: string;
  username?: string;
  email: string;
  firstName?: string;
  lastName?: string;
  phone?: string;
  emailVerified?: boolean;
}

// License Types
export interface License {
  id: string;
  name: string;
  key: string;
  type: 'Standard' | 'Professional' | 'Enterprise';
  status: 'Aktiv' | 'Abgelaufen' | 'Läuft bald ab';
  validUntil: string;
  devices: number;
  maxDevices: number;
}

// Device Types
export interface Device {
  id: string;
  name: string;
  type: 'Desktop' | 'Laptop' | 'Server';
  os: string;
  lastSeen: string;
  status: 'Online' | 'Offline' | 'Wartung';
  ipAddress: string;
  licenseId: string;
  licenseName: string;
  licenseStatus: 'Aktiv' | 'Testversion' | 'Abgelaufen' | 'Nicht zugewiesen';
  licenseValidUntil?: string;
  licenseType?: string;
  pcWaechterVersion: string;
  registeredAt: string;
  securityStatus: 'Geschützt' | 'Warnung' | 'Kritisch';
  statusMessage: string;
  lastScan?: string;
  lastMaintenance?: string;
  updateAvailable: boolean;
  latestVersion?: string;
  history?: DeviceHistoryEntry[];
}

export interface DeviceHistoryEntry {
  id: string;
  type: 'check-in' | 'scan' | 'warning' | 'action' | 'update';
  message: string;
  timestamp: string;
}

// Support Types
export type TicketStatus = 'Offen' | 'In Bearbeitung' | 'Warten auf Antwort' | 'Geschlossen';

export interface TicketMessage {
  id: number;
  sender: string;
  message: string;
  timestamp: string;
  isSupport: boolean;
}

export interface TicketAttachment {
  id: string;
  name: string;
  size: number;
  type: string;
  uploadedAt: string;
}

export interface TicketRating {
  rating: number;
  comment?: string;
  ratedAt: string;
}

export interface SupportTicket {
  id: number;
  title: string;
  description: string;
  status: TicketStatus;
  category: string;
  createdAt: string;
  lastUpdate: string;
  messages: TicketMessage[];
  attachments?: TicketAttachment[];
  rating?: TicketRating;
}

export interface TicketTemplate {
  id: string;
  name: string;
  category: string;
  description: string;
  fields: {
    label: string;
    type: 'text' | 'textarea' | 'select';
    placeholder?: string;
    options?: string[];
    required?: boolean;
  }[];
}

// Notification Types
export interface Notification {
  id: string;
  title: string;
  message: string;
  type: 'info' | 'warning' | 'success' | 'error';
  timestamp: string;
  read: boolean;
  meta?: Record<string, unknown>;
}

// License Audit Log Types
export interface LicenseAuditEntry {
  id: string;
  licenseId: string;
  action: 'created' | 'updated' | 'deleted' | 'device_added' | 'device_removed' | 'renewed';
  description: string;
  user: string;
  timestamp: string;
  details?: {
    oldValue?: any;
    newValue?: any;
    deviceInfo?: string;
  };
}

// Dashboard Types
export interface DashboardStats {
  totalLicenses: number;
  activeLicenses: number;
  expiringLicenses: number;
  openTickets: number;
}

export interface SystemStatus {
  name: string;
  status: 'operational' | 'degraded' | 'outage';
  description: string;
}

// GitHub Release Types
export interface GitHubAsset {
  id?: number;
  name: string;
  size: number;
  download_count?: number;
  browser_download_url: string;
}

export interface GitHubRelease {
  tag_name: string;
  name: string;
  body: string;
  published_at: string;
  html_url?: string;
  assets: GitHubAsset[];
}

// Documentation Types
export interface DocumentationFile {
  id: number | string;
  name: string;
  version: string;
  size: string;
  format: string;
  language: string;
  type: 'manual' | 'guide' | 'technical';
}

export interface DocumentationArticle {
  title: string;
  views: string;
}

export interface DocumentationCategory {
  title: string;
  icon: string;
  color: string;
  bgColor: string;
  articles: DocumentationArticle[];
}

export interface PopularArticle {
  title: string;
  category: string;
  views: string;
  rating: number;
}

// Search Types
export interface SearchResult {
  id: string;
  title: string;
  description: string;
  type: 'license' | 'ticket' | 'documentation';
  url: string;
}

export interface RecentActivity {
  type: string;
  title: string;
  description: string;
  timestamp: string;
}

export interface ProfileSettings {
  phone?: string | null;
  preferredLanguage: string;
  preferredTimezone: string;
  emailNotificationsEnabled: boolean;
  licenseRemindersEnabled: boolean;
  supportUpdatesEnabled: boolean;
  deletionRequestedAt?: string | null;
  deletionScheduledFor?: string | null;
}

export interface SupportGroupOption {
  id: number;
  name: string;
}

export interface SupportConfig {
  allow_customer_group_selection: boolean;
  customer_visible_group_ids: number[];
  default_group_id?: number | null;
  default_priority_id?: number | null;
  uploads_enabled: boolean;
  uploads_max_bytes: number;
  maintenance_mode: boolean;
  maintenance_message: string;
  groups: SupportGroupOption[];
  support_available: boolean;
  zammad_reachable: boolean;
}

export interface PlanSummary {
  id: string;
  label: string;
  price_eur?: number | null;
  duration_days?: number | null;
  max_devices?: number | null;
  is_active: boolean;
  sort_order: number;
  feature_flags?: Record<string, boolean> | null;
  grace_period_days?: number;
  stripe_price_id?: string | null;
  amount_cents?: number | null;
  currency?: string;
}

export interface PortalBootstrap extends MockData {
  recentActivity: RecentActivity[];
  profileSettings: ProfileSettings;
  supportConfig: SupportConfig;
  plans: PlanSummary[];
}

// Mock Data Type
export interface MockData {
  licenses: License[];
  devices: Device[];
  supportTickets: SupportTicket[];
  notifications: Notification[];
  stats: DashboardStats;
  systemStatus: SystemStatus[];
  user: User;
  documentation: DocumentationFile[];
  documentationCategories: DocumentationCategory[];
  popularArticles: PopularArticle[];
  licenseAuditLog: LicenseAuditEntry[];
  ticketTemplates: TicketTemplate[];
}
