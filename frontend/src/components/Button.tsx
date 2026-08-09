import type { ButtonHTMLAttributes } from 'react'

type ButtonVariant = 'primary' | 'secondary'

const variantClass: Record<ButtonVariant, string> = {
  primary:
    'font-semibold bg-purple-600 text-white shadow-sm shadow-purple-600/30 hover:bg-purple-500 dark:bg-purple-500 dark:hover:bg-purple-400',
  secondary:
    'font-medium border border-neutral-200 bg-neutral-50 text-neutral-900 hover:border-purple-300 hover:bg-purple-50 dark:border-neutral-700 dark:bg-neutral-800 dark:text-neutral-100 dark:hover:border-purple-500/50 dark:hover:bg-neutral-800/70',
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
}

export function Button({ variant = 'primary', className = '', ...props }: ButtonProps) {
  return (
    <button
      className={`flex w-full items-center justify-center gap-2 rounded-lg px-4 py-2.5 text-sm transition disabled:cursor-not-allowed disabled:opacity-60 ${variantClass[variant]} ${className}`}
      {...props}
    />
  )
}
