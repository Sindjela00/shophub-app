import { useEffect, useState } from 'react'
import { Button } from '@/components/ui/button'
import { FormError } from '@/components/ui/form-error'
import { Modal } from '@/components/ui/modal'
import * as shopSitesApi from '@/lib/shop-sites-api'
import type { ShopSite } from '@/types/shop-site'

export function RevealAdminKeyDialog({ site, onClose }: { site: ShopSite; onClose: () => void }) {
  const [apiKey, setApiKey] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    let cancelled = false
    shopSitesApi
      .getAdminKey(site.id)
      .then((result) => {
        if (!cancelled) setApiKey(result.key)
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Unable to reveal the admin key right now.')
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [site.id])

  const handleCopy = async () => {
    if (!apiKey) return
    await navigator.clipboard.writeText(apiKey)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <Modal title="Admin key" onClose={onClose}>
      <div className="flex flex-col gap-4">
        {error && <FormError message={error} />}

        <div className="flex items-start gap-3">
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-purple-100 text-purple-600 dark:bg-purple-500/15 dark:text-purple-400">
            <svg viewBox="0 0 20 20" fill="none" className="h-5 w-5">
              <path
                d="M8 12.5a4 4 0 1 0-3.317-1.768L3 12.4V15h2.6l1.668-1.683A4 4 0 0 0 8 12.5Z"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinejoin="round"
              />
              <circle cx="8" cy="8.5" r="1" fill="currentColor" />
            </svg>
          </span>
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            Paste this into{' '}
            <span className="font-medium text-neutral-900 dark:text-neutral-50">{site.name}</span>
            &apos;s admin login to manage its catalog and view orders.
          </p>
        </div>

        {loading ? (
          <div className="h-16 animate-pulse rounded-lg border border-neutral-200 bg-neutral-100 dark:border-neutral-700 dark:bg-neutral-800" />
        ) : apiKey ? (
          <div className="flex flex-col gap-2">
            <span className="text-xs font-medium tracking-wide text-neutral-500 uppercase dark:text-neutral-400">
              API key
            </span>
            <div className="flex items-center gap-2 rounded-lg border border-neutral-200 bg-neutral-50 py-1 pr-1 pl-3 dark:border-neutral-700 dark:bg-neutral-800">
              <code className="flex-1 py-1.5 font-mono text-xs break-all text-neutral-900 select-all dark:text-neutral-100">
                {apiKey}
              </code>
              <button
                type="button"
                onClick={handleCopy}
                className={`flex shrink-0 items-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs font-medium transition ${
                  copied
                    ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400'
                    : 'bg-white text-neutral-700 hover:bg-purple-50 hover:text-purple-700 dark:bg-neutral-900 dark:text-neutral-200 dark:hover:bg-neutral-800 dark:hover:text-purple-300'
                }`}
              >
                {copied ? (
                  <>
                    <svg viewBox="0 0 20 20" fill="none" className="h-3.5 w-3.5">
                      <path
                        d="M4 10.5l3.5 3.5L16 5.5"
                        stroke="currentColor"
                        strokeWidth="2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      />
                    </svg>
                    Copied
                  </>
                ) : (
                  <>
                    <svg viewBox="0 0 20 20" fill="none" className="h-3.5 w-3.5">
                      <rect x="7" y="7" width="9" height="9" rx="1.5" stroke="currentColor" strokeWidth="1.5" />
                      <path
                        d="M4.5 12.5V5A1.5 1.5 0 0 1 6 3.5h7.5"
                        stroke="currentColor"
                        strokeWidth="1.5"
                        strokeLinecap="round"
                      />
                    </svg>
                    Copy
                  </>
                )}
              </button>
            </div>
            <p className="text-xs text-neutral-500 dark:text-neutral-500">
              Keep this key secret — anyone with it can manage {site.name}&apos;s catalog and orders.
            </p>
          </div>
        ) : null}

        <div className="flex justify-end pt-1">
          <Button type="button" variant="secondary" fullWidth={false} onClick={onClose}>
            Close
          </Button>
        </div>
      </div>
    </Modal>
  )
}
