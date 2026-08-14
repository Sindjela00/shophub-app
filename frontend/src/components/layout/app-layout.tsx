import { Link, Outlet, useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/context/auth-context'

export function AppLayout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  const handleLogout = () => {
    logout()
    navigate('/login')
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
            <Button type="button" variant="secondary" fullWidth={false} onClick={handleLogout}>
              Log out
            </Button>
          </div>
        </div>
      </header>

      <main className="mx-auto w-full max-w-5xl flex-1 px-6 py-8">
        <Outlet />
      </main>
    </div>
  )
}
