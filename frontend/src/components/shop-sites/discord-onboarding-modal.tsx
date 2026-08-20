import { useEffect, useState } from 'react'
import { Button } from '@/components/ui/button'
import { FormError } from '@/components/ui/form-error'
import { FormField } from '@/components/ui/form-field'
import { Modal } from '@/components/ui/modal'
import * as shopSitesApi from '@/lib/shop-sites-api'
import type { DiscordStatus, ShopSite } from '@/types/shop-site'

const inviteLinkClass =
  'flex items-center justify-center gap-2 rounded-lg border border-neutral-200 bg-neutral-50 px-4 py-2.5 text-sm font-medium text-neutral-900 transition hover:border-purple-300 hover:bg-purple-50 dark:border-neutral-700 dark:bg-neutral-800 dark:text-neutral-100 dark:hover:border-purple-500/50 dark:hover:bg-neutral-800/70'

// Mirrors ShopDiscordService.AttachAsync's channelName (`${site.Name}-alerts`) and the
// operator's discord.SanitizeChannelName (shophub-shop-operator/internal/discord/client.go),
// which is what actually turns that raw name into the channel Discord ends up with: lowercase,
// spaces/invalid characters collapsed to hyphens, capped at Discord's 100-character limit.
// Kept in sync with both so this preview matches the real channel name, not just a guess.
const INVALID_CHANNEL_CHARS = /[^a-z0-9_-]+/g

function sanitizeChannelName(name: string): string {
  let s = name.trim().toLowerCase().replaceAll(' ', '-')
  s = s.replace(INVALID_CHANNEL_CHARS, '-')
  s = s.replace(/^-+|-+$/g, '')
  if (s.length === 0) s = 'shop'
  return s.slice(0, 100)
}

function statusLabel(status: DiscordStatus | null): string {
  if (!status) return ''
  if (!status.attached) return 'Not attached yet.'
  if (status.ready) return 'Attached — alerts are flowing to your channel.'
  return status.message ?? 'Attached, waiting for the bot to finish setting up the channel…'
}

export function DiscordOnboardingModal({
  site,
  onClose,
}: {
  site: ShopSite
  onClose: () => void
}) {
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [inviteUrl, setInviteUrl] = useState<string | null>(null)
  const [status, setStatus] = useState<DiscordStatus | null>(null)
  const [guildId, setGuildId] = useState('')
  const [attaching, setAttaching] = useState(false)
  const [attachError, setAttachError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setLoadError(null)
    Promise.all([shopSitesApi.getDiscordInviteUrl(site.id), shopSitesApi.getDiscordStatus(site.id)])
      .then(([invite, currentStatus]) => {
        if (cancelled) return
        setInviteUrl(invite.inviteUrl)
        setStatus(currentStatus)
        setGuildId(currentStatus.guildId ?? '')
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setLoadError(err instanceof Error ? err.message : 'Unable to load Discord onboarding right now.')
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [site.id])

  const handleAttach = async () => {
    setAttachError(null)
    setAttaching(true)
    try {
      const updated = await shopSitesApi.attachDiscord(site.id, guildId.trim())
      setStatus(updated)
    } catch (err) {
      setAttachError(err instanceof Error ? err.message : 'Unable to attach Discord right now.')
    } finally {
      setAttaching(false)
    }
  }

  return (
    <Modal title={`Discord alerts — ${site.name}`} onClose={onClose}>
      <div className="flex flex-col gap-4">
        {loadError && <FormError message={loadError} />}
        {attachError && <FormError message={attachError} />}

        {loading ? (
          <p className="text-sm text-neutral-500 dark:text-neutral-400">Loading…</p>
        ) : (
          <>
            <p className="text-sm text-neutral-600 dark:text-neutral-400">
              Invite the bot to your own Discord server, then paste its Server ID below and verify.
              Once attached, it creates a{' '}
              <span className="font-mono">#{sanitizeChannelName(`${site.name}-alerts`)}</span> channel
              there and posts a welcome message.
            </p>

            {inviteUrl && (
              <a href={inviteUrl} target="_blank" rel="noreferrer" className={inviteLinkClass}>
                Invite bot to your server
              </a>
            )}

            <FormField
              id="discord-guild-id"
              label="Server (Guild) ID"
              placeholder="e.g. 123456789012345678"
              value={guildId}
              onChange={(e) => setGuildId(e.target.value)}
              disabled={attaching}
            />

            <p className="text-sm text-neutral-600 dark:text-neutral-400">{statusLabel(status)}</p>

            <div className="flex justify-end gap-3">
              <Button type="button" variant="secondary" fullWidth={false} onClick={onClose} disabled={attaching}>
                Close
              </Button>
              <Button
                type="button"
                fullWidth={false}
                onClick={handleAttach}
                disabled={attaching || guildId.trim().length === 0}
              >
                {attaching ? 'Verifying…' : 'Verify & Attach'}
              </Button>
            </div>
          </>
        )}
      </div>
    </Modal>
  )
}
