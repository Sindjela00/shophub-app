import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { FormError } from '@/components/ui/form-error'
import { Modal } from '@/components/ui/modal'
import type { ShopSite } from '@/types/shop-site'

export function DeleteShopSiteDialog({
  site,
  onClose,
  onConfirm,
}: {
  site: ShopSite
  onClose: () => void
  onConfirm: () => Promise<void>
}) {
  const [error, setError] = useState<string | null>(null)
  const [deleting, setDeleting] = useState(false)

  const handleConfirm = async () => {
    setError(null)
    setDeleting(true)
    try {
      await onConfirm()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to delete the shop site right now.')
      setDeleting(false)
    }
  }

  return (
    <Modal title="Delete shop site" onClose={onClose}>
      <div className="flex flex-col gap-4">
        {error && <FormError message={error} />}
        <p className="text-sm text-neutral-600 dark:text-neutral-400">
          Are you sure you want to delete{' '}
          <span className="font-medium text-neutral-900 dark:text-neutral-50">{site.name}</span>?
          This tears down its deployment and cannot be undone.
        </p>
        <div className="flex justify-end gap-3">
          <Button type="button" variant="secondary" fullWidth={false} onClick={onClose} disabled={deleting}>
            Cancel
          </Button>
          <Button type="button" variant="danger" fullWidth={false} onClick={handleConfirm} disabled={deleting}>
            {deleting ? 'Deleting…' : 'Delete'}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
