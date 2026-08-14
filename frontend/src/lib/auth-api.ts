import type { AuthResponse, LoginRequest, RegisterRequest } from '@/types/auth'

const API_BASE_URL = import.meta.env.VITE_API_URL ?? '/api'

export class AuthApiError extends Error {
  status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

async function request<TResponse>(path: string, body: unknown): Promise<TResponse> {
  const res = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

  if (!res.ok) {
    const message = await res
      .json()
      .then((data) => data?.error as string | undefined)
      .catch(() => undefined)
    throw new AuthApiError(message ?? `Request failed with status ${res.status}`, res.status)
  }

  return res.json() as Promise<TResponse>
}

export function login(payload: LoginRequest): Promise<AuthResponse> {
  return request<AuthResponse>('/auth/login', payload)
}

export function register(payload: RegisterRequest): Promise<AuthResponse> {
  return request<AuthResponse>('/auth/register', payload)
}
