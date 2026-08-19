import { request } from '@/lib/shop-sites-api'

export function getPlatformDashboardLink(): Promise<{ path: string }> {
  return request<{ path: string }>('GET', '/platform/dashboard-link')
}
