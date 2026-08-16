import { API_ORIGIN } from '@/lib/shop-sites-api'

// The Shop app has no public route of its own (its operator only creates a ClusterIP
// Service — no Ingress). This proxies through ShopHub's backend instead, same pattern as
// /grafana-proxy, except public: a shop's customers browsing/buying from it have no ShopHub
// account at all, unlike a Grafana dashboard viewer.
export function shopSiteUrl(id: string): string {
  return `${API_ORIGIN}/shop-proxy/${id}/`
}
