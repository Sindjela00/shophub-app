const SHOP_BASE_DOMAIN = import.meta.env.VITE_SHOP_BASE_DOMAIN ?? 'shops.localhost'

// Mirrors ShopSite.K8sName on the backend (`shop-{Id:N}`) - each site's ingress host is
// expected to be this subdomain once the shop-operator provisions it.
export function shopSiteUrl(id: string): string {
  return `https://shop-${id.replace(/-/g, '')}.${SHOP_BASE_DOMAIN}`
}
