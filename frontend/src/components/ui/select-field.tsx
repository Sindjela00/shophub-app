import type { SelectHTMLAttributes } from 'react'

interface SelectOption {
  value: string
  label: string
}

interface SelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  id: string
  label: string
  options: SelectOption[]
  error?: string
}

export function SelectField({ id, label, options, error, ...selectProps }: SelectFieldProps) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-medium text-neutral-700 dark:text-neutral-300">
        {label}
      </label>
      <select
        id={id}
        aria-invalid={Boolean(error)}
        className="w-full rounded-lg border border-neutral-200 bg-white px-3.5 py-2.5 text-[15px] text-neutral-900 outline-none transition focus:border-purple-500 focus:ring-2 focus:ring-purple-500/30 aria-[invalid=true]:border-red-400 disabled:cursor-not-allowed disabled:opacity-60 dark:border-neutral-700 dark:bg-neutral-950 dark:text-neutral-50 dark:focus:border-purple-400"
        {...selectProps}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      {error && <p className="text-xs text-red-500">{error}</p>}
    </div>
  )
}
