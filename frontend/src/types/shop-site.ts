export type Availability = 'standard' | 'high'
export type DatabaseKind = 'standard' | 'light'

export interface ShopSite {
  id: string
  name: string
  availability: Availability
  walletAddress: string
  databaseKind: DatabaseKind
  createdAt: string
}

export interface CreateShopSiteRequest {
  name: string
  availability: Availability
  walletAddress: string
  databaseKind: DatabaseKind
}

export interface UpdateShopSiteRequest {
  availability: Availability
  walletAddress: string
}

export interface DiscordStatus {
  attached: boolean
  guildId: string | null
  ready: boolean
  message: string | null
}
