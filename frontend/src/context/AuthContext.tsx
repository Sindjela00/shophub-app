import { useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import * as authApi from '../services/authApi'
import type { AuthUser } from '../types/auth'
import { AuthContext } from './auth-context'
import type { AuthContextValue } from './auth-context'

const TOKEN_KEY = 'shophub.token'
const USER_KEY = 'shophub.user'

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
    localStorage.setItem(TOKEN_KEY, token)
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
        localStorage.removeItem(TOKEN_KEY)
        localStorage.removeItem(USER_KEY)
        setUser(null)
      },
    }),
    [user],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
