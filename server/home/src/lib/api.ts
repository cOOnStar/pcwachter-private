import { IS_PREVIEW, createKeycloakInstance, getKeycloak } from './keycloak';
import type { GitHubRelease, PortalBootstrap } from '../types';

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:18080/api/v1').replace(/\/+$/, '');

type JsonBody = Record<string, unknown> | unknown[] | null;

async function getAccessToken(): Promise<string> {
  if (IS_PREVIEW) {
    return '';
  }

  const existing = getKeycloak();
  const keycloak = existing || (await createKeycloakInstance());
  if (!keycloak || !keycloak.authenticated || !keycloak.token) {
    throw new Error('not_authenticated');
  }

  try {
    await keycloak.updateToken(30);
  } catch {
    throw new Error('token_refresh_failed');
  }

  if (!keycloak.token) {
    throw new Error('missing_access_token');
  }
  return keycloak.token as string;
}

async function apiRequest<T>(
  path: string,
  init: RequestInit = {},
  options: { auth?: boolean } = {},
): Promise<T> {
  const headers = new Headers(init.headers);
  const wantsJson = init.body && !(init.body instanceof FormData);
  if (wantsJson && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  if (options.auth !== false) {
    const token = await getAccessToken();
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers,
  });

  if (!response.ok) {
    let detail = `request_failed_${response.status}`;
    try {
      const payload = await response.json();
      if (typeof payload?.detail === 'string') {
        detail = payload.detail;
      }
    } catch {
      // ignore JSON parse failures for error responses
    }
    throw new Error(detail);
  }

  if (response.status === 204) {
    return null as T;
  }
  return response.json() as Promise<T>;
}

export function fetchPortalBootstrap(): Promise<PortalBootstrap> {
  return apiRequest<PortalBootstrap>('/home/bootstrap');
}

export function fetchLatestRelease(): Promise<GitHubRelease | null> {
  return apiRequest<GitHubRelease | null>('/home/downloads/latest-release');
}

export function updateHomeProfile(payload: JsonBody): Promise<PortalBootstrap> {
  return apiRequest<PortalBootstrap>('/home/profile', {
    method: 'PATCH',
    body: JSON.stringify(payload),
  });
}

export function requestAccountDelete(confirmation: string): Promise<{ ok: boolean }> {
  return apiRequest<{ ok: boolean }>('/home/account/delete-request', {
    method: 'POST',
    body: JSON.stringify({ confirmation }),
  });
}

export function renameDevice(deviceId: string, name: string): Promise<{ ok: boolean }> {
  return apiRequest<{ ok: boolean }>(`/home/devices/${encodeURIComponent(deviceId)}/name`, {
    method: 'PATCH',
    body: JSON.stringify({ name }),
  });
}

export function removeDevice(deviceId: string): Promise<{ ok: boolean }> {
  return apiRequest<{ ok: boolean }>(`/home/devices/${encodeURIComponent(deviceId)}`, {
    method: 'DELETE',
  });
}

export function assignDeviceLicense(deviceId: string, licenseId: string): Promise<{ ok: boolean }> {
  return apiRequest<{ ok: boolean }>(`/home/devices/${encodeURIComponent(deviceId)}/license`, {
    method: 'PUT',
    body: JSON.stringify({ license_id: licenseId }),
  });
}

export function markNotificationRead(notificationId: string): Promise<{ ok: boolean }> {
  return apiRequest<{ ok: boolean }>(`/notifications/${notificationId}/read`, {
    method: 'PUT',
  });
}

export function markAllNotificationsRead(): Promise<{ ok: boolean }> {
  return apiRequest<{ ok: boolean }>('/notifications/read-all', {
    method: 'PUT',
  });
}

export function deleteNotification(notificationId: string): Promise<{ ok: boolean }> {
  return apiRequest<{ ok: boolean }>(`/notifications/${notificationId}`, {
    method: 'DELETE',
  });
}

export function clearNotifications(): Promise<{ ok: boolean }> {
  return apiRequest<{ ok: boolean }>('/notifications', {
    method: 'DELETE',
  });
}

export function uploadSupportAttachment(file: File): Promise<{ id: string; filename?: string; size?: number; mime_type?: string }> {
  const formData = new FormData();
  formData.append('file', file);
  return apiRequest<{ id: string; filename?: string; size?: number; mime_type?: string }>('/support/attachments', {
    method: 'POST',
    body: formData,
  });
}

export function createSupportTicket(payload: JsonBody): Promise<{ id: number }> {
  return apiRequest<{ id: number }>('/support/tickets', {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

export function replySupportTicket(ticketId: number, payload: JsonBody): Promise<{ id: number }> {
  return apiRequest<{ id: number }>(`/support/tickets/${ticketId}/reply`, {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

export function closeSupportTicket(ticketId: number): Promise<{ ok: boolean }> {
  return apiRequest<{ ok: boolean }>(`/support/tickets/${ticketId}/close`, {
    method: 'PUT',
  });
}

export function rateSupportTicket(ticketId: number, rating: number, comment?: string): Promise<{ ok: boolean }> {
  return apiRequest<{ ok: boolean }>(`/support/tickets/${ticketId}/rating`, {
    method: 'POST',
    body: JSON.stringify({ rating, comment }),
  });
}

export function createCheckoutSession(planId: string, successUrl: string, cancelUrl: string): Promise<{ checkout_url: string }> {
  return apiRequest<{ checkout_url: string }>('/payments/create-checkout', {
    method: 'POST',
    body: JSON.stringify({
      plan_id: planId,
      success_url: successUrl,
      cancel_url: cancelUrl,
    }),
  });
}

export function requestLicenseRenewal(licenseId: string): Promise<{ ok: boolean }> {
  return apiRequest<{ ok: boolean }>(`/home/licenses/${encodeURIComponent(licenseId)}/renew-request`, {
    method: 'POST',
  });
}
