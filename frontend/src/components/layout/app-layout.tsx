import { useState } from 'react'
import { Link, Outlet, useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { FormError } from '@/components/ui/form-error'
import { useAuth } from '@/context/auth-context'
import { getPlatformDashboardLink } from '@/lib/platform-api'
import { API_ORIGIN } from '@/lib/shop-sites-api'
import { getToken } from '@/lib/token-storage'

export function AppLayout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [openingDashboard, setOpeningDashboard] = useState(false)
  const [dashboardError, setDashboardError] = useState<string | null>(null)

  const handleLogout = () => {
    logout()
    navigate('/login')
  }

  const handleOpenPlatformDashboard = async () => {
    // Same synchronous-open-then-navigate pattern used for a shop's own dashboard link — opening
    // the tab before the await keeps browsers from treating it as an unrequested popup.
    const tab = window.open('', '_blank')
    setDashboardError(null)
    setOpeningDashboard(true)
    try {
      const { path } = await getPlatformDashboardLink()
      const token = getToken()
      if (tab) {
        tab.location.href = `${API_ORIGIN}${path}&access_token=${encodeURIComponent(token ?? '')}`
      }
    } catch (err) {
      tab?.close()
      setDashboardError(err instanceof Error ? err.message : 'Unable to open the platform dashboard right now.')
    } finally {
      setOpeningDashboard(false)
    }
  }

  return (
    <div className="flex min-h-svh flex-1 flex-col bg-neutral-50 dark:bg-neutral-950">
      <header className="border-b border-neutral-200 bg-white dark:border-neutral-800 dark:bg-neutral-900">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
          <Link
            to="/"
            className="text-lg font-semibold tracking-tight text-neutral-900 dark:text-neutral-50"
          >
            Shop<span className="text-purple-600 dark:text-purple-400">Hub</span>
          </Link>

          <div className="flex items-center gap-4">
            {user && (
              <span className="hidden text-sm text-neutral-500 sm:inline dark:text-neutral-400">
                {user.email}
              </span>
            )}
            <Button
              type="button"
              variant="secondary"
              fullWidth={false}
              onClick={handleOpenPlatformDashboard}
              disabled={openingDashboard}
            >
              {openingDashboard ? 'Opening…' : 'Platform Dashboard'}
            </Button>
            <Button type="button" variant="secondary" fullWidth={false} onClick={handleLogout}>
              Log out
            </Button>
          </div>
        </div>
        {dashboardError && (
          <div className="mx-auto max-w-5xl px-6 pb-4">
            <FormError message={dashboardError} />
          </div>
        )}
      </header>

      <main className="mx-auto w-full max-w-5xl flex-1 px-6 py-8">
        <Outlet />
      </main>
    </div>
  )
}
