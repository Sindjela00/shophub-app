import { createContext, useContext, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import * as authApi from '@/lib/auth-api'
import { clearToken, setToken } from '@/lib/token-storage'
import type { AuthUser } from '@/types/auth'

const USER_KEY = 'shophub.user'

interface AuthContextValue {
  user: AuthUser | null
  isAuthenticated: boolean
  login: (payload: { email: string; password: string }) => Promise<void>
  register: (payload: { email: string; password: string; confirmPassword: string }) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

function readStoredUser(): AuthUser | null {
  const raw = localStorage.getItem(USER_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as AuthUser
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => readStoredUser())

  const persistSession = (token: string, nextUser: AuthUser) => {
    setToken(token)
    localStorage.setItem(USER_KEY, JSON.stringify(nextUser))
    setUser(nextUser)
  }

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      login: async (payload) => {
        const { token, user: loggedInUser } = await authApi.login(payload)
        persistSession(token, loggedInUser)
      },
      register: async (payload) => {
        const { token, user: registeredUser } = await authApi.register(payload)
        persistSession(token, registeredUser)
      },
      logout: () => {
        clearToken()
        localStorage.removeItem(USER_KEY)
        setUser(null)
      },
    }),
    [user],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}
