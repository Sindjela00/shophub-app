import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { FormError } from '@/components/ui/form-error'
import { FormField } from '@/components/ui/form-field'
import { Modal } from '@/components/ui/modal'
import { SelectField } from '@/components/ui/select-field'
import type { Availability, DatabaseKind, ShopSite } from '@/types/shop-site'

export interface ShopSiteFormValues {
  name: string
  availability: Availability
  walletAddress: string
  databaseKind: DatabaseKind
}

interface FieldErrors {
  name?: string
  walletAddress?: string
}

function validate(values: ShopSiteFormValues, mode: 'create' | 'edit'): FieldErrors {
  const errors: FieldErrors = {}
  if (mode === 'create' && values.name.trim().length === 0) {
    errors.name = 'Name is required.'
  }
  if (values.walletAddress.trim().length === 0) {
    errors.walletAddress = 'Wallet address is required.'
  }
  return errors
}

export function ShopSiteFormModal({
  site,
  onClose,
  onSubmit,
}: {
  site?: ShopSite
  onClose: () => void
  onSubmit: (values: ShopSiteFormValues) => Promise<void>
}) {
  const mode = site ? 'edit' : 'create'

  const [values, setValues] = useState<ShopSiteFormValues>({
    name: site?.name ?? '',
    availability: site?.availability ?? 'standard',
    walletAddress: site?.walletAddress ?? '',
    databaseKind: site?.databaseKind ?? 'standard',
  })
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const errors = validate(values, mode)
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) return

    setFormError(null)
    setSubmitting(true)
    try {
      await onSubmit(values)
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Unable to save the shop site right now.')
      setSubmitting(false)
    }
  }

  return (
    <Modal title={mode === 'create' ? 'New shop site' : `Edit ${site!.name}`} onClose={onClose}>
      <form className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
        {formError && <FormError message={formError} />}

        <FormField
          id="shop-site-name"
          label="Name"
          value={values.name}
          disabled={mode === 'edit'}
          onChange={(e) => setValues((v) => ({ ...v, name: e.target.value }))}
          error={fieldErrors.name}
        />

        <SelectField
          id="shop-site-availability"
          label="Availability"
          value={values.availability}
          onChange={(e) =>
            setValues((v) => ({ ...v, availability: e.target.value as Availability }))
          }
          options={[
            { value: 'standard', label: 'Standard (2 replicas)' },
            { value: 'high', label: 'High (3 replicas)' },
          ]}
        />

        <FormField
          id="shop-site-wallet-address"
          label="Wallet address"
          value={values.walletAddress}
          onChange={(e) => setValues((v) => ({ ...v, walletAddress: e.target.value }))}
          error={fieldErrors.walletAddress}
        />

        <SelectField
          id="shop-site-database-kind"
          label="Database"
          value={values.databaseKind}
          disabled={mode === 'edit'}
          onChange={(e) =>
            setValues((v) => ({ ...v, databaseKind: e.target.value as DatabaseKind }))
          }
          options={[
            { value: 'standard', label: 'PostgreSQL (standard)' },
            { value: 'light', label: 'Redis (light)' },
          ]}
        />

        <div className="mt-2 flex justify-end gap-3">
          <Button type="button" variant="secondary" fullWidth={false} onClick={onClose} disabled={submitting}>
            Cancel
          </Button>
          <Button type="submit" fullWidth={false} disabled={submitting}>
            {submitting ? 'Saving…' : mode === 'create' ? 'Create site' : 'Save changes'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
