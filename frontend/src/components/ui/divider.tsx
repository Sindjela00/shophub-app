import type { ReactNode } from 'react'

export function Divider({ children }: { children: ReactNode }) {
  return (
    <div className="my-6 flex items-center gap-3 text-xs text-neutral-400 before:h-px before:flex-1 before:bg-neutral-200 after:h-px after:flex-1 after:bg-neutral-200 dark:text-neutral-500 dark:before:bg-neutral-800 dark:after:bg-neutral-800">
      {children}
    </div>
  )
}
