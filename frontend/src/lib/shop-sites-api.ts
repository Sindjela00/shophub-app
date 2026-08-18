import { getToken } from '@/lib/token-storage'
import type { CreateShopSiteRequest, DiscordStatus, ShopSite, UpdateShopSiteRequest } from '@/types/shop-site'

const API_BASE_URL = import.meta.env.VITE_API_URL ?? '/api'

// The /grafana-proxy route lives on the API server's origin but outside the /api prefix.
export const API_ORIGIN = API_BASE_URL.replace(/\/api\/?$/, '')

export class ShopSiteApiError extends Error {
  status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

// Exported for platform-api.ts — the platform dashboard link isn't shop-site-scoped, but the
// request/auth/error-handling plumbing is identical.
export async function request<TResponse>(method: string, path: string, body?: unknown): Promise<TResponse> {
  const token = getToken()
  const res = await fetch(`${API_BASE_URL}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  if (!res.ok) {
    const message = await res
      .json()
      .then((data) => data?.error as string | undefined)
      .catch(() => undefined)
    throw new ShopSiteApiError(message ?? `Request failed with status ${res.status}`, res.status)
  }

  if (res.status === 204) {
    return undefined as TResponse
  }

  return res.json() as Promise<TResponse>
}

export function listShopSites(): Promise<ShopSite[]> {
  return request<ShopSite[]>('GET', '/shop-sites')
}

export function createShopSite(payload: CreateShopSiteRequest): Promise<ShopSite> {
  return request<ShopSite>('POST', '/shop-sites', payload)
}

export function updateShopSite(id: string, payload: UpdateShopSiteRequest): Promise<ShopSite> {
  return request<ShopSite>('PUT', `/shop-sites/${id}`, payload)
}

export function deleteShopSite(id: string): Promise<void> {
  return request<void>('DELETE', `/shop-sites/${id}`)
}

export function getDashboardLink(id: string): Promise<{ path: string }> {
  return request<{ path: string }>('GET', `/shop-sites/${id}/dashboard-link`)
}

export function getSiteUrl(id: string): Promise<{ url: string }> {
  return request<{ url: string }>('GET', `/shop-sites/${id}/site-url`)
}

export function getDiscordInviteUrl(id: string): Promise<{ inviteUrl: string }> {
  return request<{ inviteUrl: string }>('GET', `/shop-sites/${id}/discord/invite-url`)
}

export function getDiscordStatus(id: string): Promise<DiscordStatus> {
  return request<DiscordStatus>('GET', `/shop-sites/${id}/discord/status`)
}

export function attachDiscord(id: string, guildId: string): Promise<DiscordStatus> {
  return request<DiscordStatus>('POST', `/shop-sites/${id}/discord/attach`, { guildId })
}
