import type { InputHTMLAttributes } from 'react'

interface FormFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  id: string
  label: string
  error?: string
}

export function FormField({ id, label, error, ...inputProps }: FormFieldProps) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-medium text-neutral-700 dark:text-neutral-300">
        {label}
      </label>
      <input
        id={id}
        aria-invalid={Boolean(error)}
        className="w-full rounded-lg border border-neutral-200 bg-white px-3.5 py-2.5 text-[15px] text-neutral-900 outline-none transition placeholder:text-neutral-400 focus:border-purple-500 focus:ring-2 focus:ring-purple-500/30 aria-[invalid=true]:border-red-400 dark:border-neutral-700 dark:bg-neutral-950 dark:text-neutral-50 dark:focus:border-purple-400"
        {...inputProps}
      />
      {error && <p className="text-xs text-red-500">{error}</p>}
    </div>
  )
}
