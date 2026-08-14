const SHOP_BASE_DOMAIN = import.meta.env.VITE_SHOP_BASE_DOMAIN ?? 'shops.localhost'

export function shopSiteUrl(id: string): string {
  return `https://shop-${id.replace(/-/g, '')}.${SHOP_BASE_DOMAIN}`
}
