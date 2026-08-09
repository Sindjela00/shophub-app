import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

const features = [
  'One-click shop deployment on Kubernetes',
  'Accept crypto payments out of the box',
  'Built-in observability & alerting per shop',
]

function CheckIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" className="h-5 w-5 shrink-0 text-purple-400">
      <circle cx="10" cy="10" r="10" fill="currentColor" fillOpacity="0.15" />
      <path
        d="M6 10.5l2.5 2.5 5.5-6"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}

export function AuthLayout({
  title,
  subtitle,
  children,
  footer,
}: {
  title: string
  subtitle: string
  children: ReactNode
  footer: ReactNode
}) {
  return (
    <div className="grid flex-1 bg-white dark:bg-neutral-950 md:grid-cols-2">
      <div className="relative hidden flex-col justify-between overflow-hidden bg-neutral-950 p-12 md:flex">
        <div
          className="absolute inset-0 opacity-[0.15]"
          style={{
            backgroundImage: 'radial-gradient(circle, rgba(255,255,255,0.6) 1px, transparent 1px)',
            backgroundSize: '28px 28px',
          }}
        />
        <div className="pointer-events-none absolute -top-24 -left-24 h-96 w-96 rounded-full bg-purple-600/30 blur-3xl" />
        <div className="pointer-events-none absolute -right-24 -bottom-32 h-96 w-96 rounded-full bg-fuchsia-500/20 blur-3xl" />

        <Link to="/login" className="relative z-10 text-lg font-semibold tracking-tight text-white">
          Shop<span className="text-purple-400">Hub</span>
        </Link>

        <div className="relative z-10 max-w-sm">
          <h2 className="text-3xl leading-tight font-semibold text-white">
            Launch your storefront in minutes, not weeks.
          </h2>
          <p className="mt-4 text-neutral-400">
            Spin up a fully managed shop, pick your database and replica count, and start selling -
            we run the rest on Kubernetes.
          </p>

          <ul className="mt-8 space-y-3.5">
            {features.map((feature) => (
              <li key={feature} className="flex items-center gap-3 text-sm text-neutral-300">
                <CheckIcon />
                {feature}
              </li>
            ))}
          </ul>
        </div>

        <p className="relative z-10 text-xs text-neutral-500">© 2026 ShopHub</p>
      </div>

      <div className="flex flex-col items-center justify-center px-6 py-12 sm:px-12">
        <div className="w-full max-w-sm">
          <Link
            to="/login"
            className="mb-8 block text-center text-lg font-semibold tracking-tight text-neutral-900 md:hidden dark:text-neutral-50"
          >
            Shop
            <span className="text-purple-600 dark:text-purple-400">Hub</span>
          </Link>

          <h1 className="text-center text-2xl font-semibold text-neutral-900 dark:text-neutral-50">
            {title}
          </h1>
          <p className="mt-2 mb-8 text-center text-sm text-neutral-500 dark:text-neutral-400">
            {subtitle}
          </p>

          {children}

          <p className="mt-8 text-center text-sm text-neutral-500 dark:text-neutral-400">
            {footer}
          </p>
        </div>
      </div>
    </div>
  )
}
