import { useEffect, useState } from 'react'
import { Button } from '@/components/ui/button'
import { FormError } from '@/components/ui/form-error'
import { DeleteShopSiteDialog } from '@/components/shop-sites/delete-shop-site-dialog'
import { ShopSiteFormModal } from '@/components/shop-sites/shop-site-form-modal'
import type { ShopSiteFormValues } from '@/components/shop-sites/shop-site-form-modal'
import * as shopSitesApi from '@/lib/shop-sites-api'
import { shopSiteUrl } from '@/lib/shop-site-url'
import type { ShopSite } from '@/types/shop-site'

const availabilityLabel: Record<ShopSite['availability'], string> = {
  standard: 'Standard · 2 replicas',
  high: 'High · 3 replicas',
}

const databaseLabel: Record<ShopSite['databaseKind'], string> = {
  standard: 'PostgreSQL',
  light: 'Redis',
}

function shortenAddress(address: string): string {
  return address.length > 12 ? `${address.slice(0, 6)}…${address.slice(-4)}` : address
}

const rowActionClass =
  'rounded-lg border border-neutral-200 px-3 py-1.5 text-xs font-medium text-neutral-700 transition hover:border-purple-300 hover:bg-purple-50 dark:border-neutral-700 dark:text-neutral-300 dark:hover:border-purple-500/50 dark:hover:bg-neutral-800/70'

export function ShopSitesPage() {
  const [sites, setSites] = useState<ShopSite[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [editingSite, setEditingSite] = useState<ShopSite | 'new' | null>(null)
  const [deletingSite, setDeletingSite] = useState<ShopSite | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setLoadError(null)
    shopSitesApi
      .listShopSites()
      .then((result) => {
        if (!cancelled) setSites(result)
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setLoadError(err instanceof Error ? err.message : 'Unable to load your shop sites right now.')
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [])

  const handleFormSubmit = async (values: ShopSiteFormValues) => {
    if (editingSite && editingSite !== 'new') {
      const updated = await shopSitesApi.updateShopSite(editingSite.id, {
        availability: values.availability,
        walletAddress: values.walletAddress,
      })
      setSites((prev) => prev.map((s) => (s.id === updated.id ? updated : s)))
    } else {
      const created = await shopSitesApi.createShopSite(values)
      setSites((prev) => [created, ...prev])
    }
    setEditingSite(null)
  }

  const handleDelete = async () => {
    if (!deletingSite) return
    await shopSitesApi.deleteShopSite(deletingSite.id)
    setSites((prev) => prev.filter((s) => s.id !== deletingSite.id))
    setDeletingSite(null)
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-neutral-900 dark:text-neutral-50">
            Your shop sites
          </h1>
          <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">
            Create and manage your Shop deployments.
          </p>
        </div>
        <Button type="button" fullWidth={false} onClick={() => setEditingSite('new')}>
          New site
        </Button>
      </div>

      {loadError && <FormError message={loadError} />}

      {loading ? (
        <p className="text-sm text-neutral-500 dark:text-neutral-400">Loading…</p>
      ) : sites.length === 0 && !loadError ? (
        <div className="rounded-xl border border-dashed border-neutral-300 p-12 text-center dark:border-neutral-700">
          <p className="text-sm text-neutral-500 dark:text-neutral-400">
            You haven&apos;t created any shop sites yet.
          </p>
          <div className="mt-4 flex justify-center">
            <Button type="button" fullWidth={false} onClick={() => setEditingSite('new')}>
              Create your first site
            </Button>
          </div>
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-neutral-200 dark:border-neutral-800">
          <table className="w-full min-w-[720px] text-left text-sm">
            <thead className="bg-neutral-50 text-xs tracking-wide text-neutral-500 uppercase dark:bg-neutral-900 dark:text-neutral-400">
              <tr>
                <th className="px-4 py-3 font-medium">Name</th>
                <th className="px-4 py-3 font-medium">Availability</th>
                <th className="px-4 py-3 font-medium">Database</th>
                <th className="px-4 py-3 font-medium">Wallet</th>
                <th className="px-4 py-3 font-medium">
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-neutral-200 dark:divide-neutral-800">
              {sites.map((site) => (
                <tr key={site.id}>
                  <td className="px-4 py-3 font-medium text-neutral-900 dark:text-neutral-50">
                    {site.name}
                  </td>
                  <td className="px-4 py-3 text-neutral-600 dark:text-neutral-400">
                    {availabilityLabel[site.availability]}
                  </td>
                  <td className="px-4 py-3 text-neutral-600 dark:text-neutral-400">
                    {databaseLabel[site.databaseKind]}
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-neutral-500 dark:text-neutral-400">
                    {shortenAddress(site.walletAddress)}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end gap-2">
                      <a
                        href={shopSiteUrl(site.id)}
                        target="_blank"
                        rel="noreferrer"
                        className={rowActionClass}
                      >
                        Open site
                      </a>
                      <button
                        type="button"
                        onClick={() => setEditingSite(site)}
                        className={rowActionClass}
                      >
                        Edit
                      </button>
                      <button
                        type="button"
                        onClick={() => setDeletingSite(site)}
                        className="rounded-lg border border-red-200 px-3 py-1.5 text-xs font-medium text-red-600 transition hover:bg-red-50 dark:border-red-500/30 dark:text-red-400 dark:hover:bg-red-500/10"
                      >
                        Delete
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {editingSite && (
        <ShopSiteFormModal
          site={editingSite === 'new' ? undefined : editingSite}
          onClose={() => setEditingSite(null)}
          onSubmit={handleFormSubmit}
        />
      )}

      {deletingSite && (
        <DeleteShopSiteDialog
          site={deletingSite}
          onClose={() => setDeletingSite(null)}
          onConfirm={handleDelete}
        />
      )}
    </div>
  )
}
