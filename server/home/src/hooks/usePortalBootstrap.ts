import { useQuery } from '@tanstack/react-query';

import { mockData } from '../app/data/mock-data';
import { fetchPortalBootstrap } from '../lib/api';
import { IS_PREVIEW } from '../lib/keycloak';
import type { PortalBootstrap } from '../types';

const PREVIEW_PORTAL_BOOTSTRAP: PortalBootstrap = {
  ...mockData,
  recentActivity: [
    {
      type: 'license',
      title: 'Calvin-PC aktiviert',
      description: 'Lizenz Professional – Gerät hinzugefügt',
      timestamp: '2026-03-06T10:00:00',
    },
    {
      type: 'success',
      title: 'Support-Antwort erhalten',
      description: 'Ticket #3 – Performance-Probleme nach Update',
      timestamp: '2026-03-02T16:30:00',
    },
    {
      type: 'info',
      title: 'Neue Version verfügbar',
      description: 'PC-Wächter v2.6.0 steht zum Download bereit',
      timestamp: '2026-03-04T10:00:00',
    },
  ],
  profileSettings: {
    phone: null,
    preferredLanguage: 'de',
    preferredTimezone: 'Europe/Berlin',
    emailNotificationsEnabled: true,
    licenseRemindersEnabled: true,
    supportUpdatesEnabled: true,
    deletionRequestedAt: null,
    deletionScheduledFor: null,
  },
  supportConfig: {
    allow_customer_group_selection: false,
    customer_visible_group_ids: [],
    default_group_id: null,
    default_priority_id: null,
    uploads_enabled: true,
    uploads_max_bytes: 5242880,
    maintenance_mode: false,
    maintenance_message: '',
    groups: [],
    support_available: true,
    zammad_reachable: true,
  },
  plans: [
    {
      id: 'standard',
      label: 'Standard',
      price_eur: 4.99,
      duration_days: 30,
      max_devices: 1,
      is_active: true,
      sort_order: 1,
      grace_period_days: 7,
      amount_cents: 499,
      currency: 'eur',
    },
    {
      id: 'professional',
      label: 'Professional',
      price_eur: 49.99,
      duration_days: 365,
      max_devices: 3,
      is_active: true,
      sort_order: 2,
      grace_period_days: 7,
      amount_cents: 4999,
      currency: 'eur',
    },
  ],
};

const EMPTY_PORTAL_BOOTSTRAP: PortalBootstrap = {
  licenses: [],
  devices: [],
  supportTickets: [],
  notifications: [],
  stats: {
    totalLicenses: 0,
    activeLicenses: 0,
    expiringLicenses: 0,
    openTickets: 0,
  },
  systemStatus: [],
  user: {
    id: '',
    email: '',
  },
  documentation: [],
  documentationCategories: [],
  popularArticles: [],
  licenseAuditLog: [],
  ticketTemplates: [],
  recentActivity: [],
  profileSettings: {
    phone: null,
    preferredLanguage: 'de',
    preferredTimezone: 'Europe/Berlin',
    emailNotificationsEnabled: true,
    licenseRemindersEnabled: true,
    supportUpdatesEnabled: true,
    deletionRequestedAt: null,
    deletionScheduledFor: null,
  },
  supportConfig: {
    allow_customer_group_selection: false,
    customer_visible_group_ids: [],
    default_group_id: null,
    default_priority_id: null,
    uploads_enabled: false,
    uploads_max_bytes: 0,
    maintenance_mode: false,
    maintenance_message: '',
    groups: [],
    support_available: false,
    zammad_reachable: true,
  },
  plans: [],
};

export const PREVIEW_BOOTSTRAP = IS_PREVIEW ? PREVIEW_PORTAL_BOOTSTRAP : EMPTY_PORTAL_BOOTSTRAP;

export function usePortalBootstrap() {
  return useQuery<PortalBootstrap>({
    queryKey: ['portal-bootstrap'],
    queryFn: () => (IS_PREVIEW ? Promise.resolve(PREVIEW_PORTAL_BOOTSTRAP) : fetchPortalBootstrap()),
    placeholderData: IS_PREVIEW ? PREVIEW_PORTAL_BOOTSTRAP : undefined,
  });
}
