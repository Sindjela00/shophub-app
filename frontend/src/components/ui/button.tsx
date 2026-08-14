import type { ButtonHTMLAttributes } from 'react'

type ButtonVariant = 'primary' | 'secondary' | 'danger'

const variantClass: Record<ButtonVariant, string> = {
  primary:
    'font-semibold bg-purple-600 text-white shadow-sm shadow-purple-600/30 hover:bg-purple-500 dark:bg-purple-500 dark:hover:bg-purple-400',
  secondary:
    'font-medium border border-neutral-200 bg-neutral-50 text-neutral-900 hover:border-purple-300 hover:bg-purple-50 dark:border-neutral-700 dark:bg-neutral-800 dark:text-neutral-100 dark:hover:border-purple-500/50 dark:hover:bg-neutral-800/70',
  danger:
    'font-medium border border-red-200 bg-white text-red-600 hover:bg-red-50 dark:border-red-500/30 dark:bg-neutral-900 dark:text-red-400 dark:hover:bg-red-500/10',
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  fullWidth?: boolean
}

export function Button({ variant = 'primary', fullWidth = true, className = '', ...props }: ButtonProps) {
  return (
    <button
      className={`flex items-center justify-center gap-2 rounded-lg px-4 py-2.5 text-sm transition disabled:cursor-not-allowed disabled:opacity-60 ${fullWidth ? 'w-full' : ''} ${variantClass[variant]} ${className}`}
      {...props}
    />
  )
}
