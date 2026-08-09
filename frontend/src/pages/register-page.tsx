import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { AuthLayout } from '@/components/layout/auth-layout'
import { Button } from '@/components/ui/button'
import { FormError } from '@/components/ui/form-error'
import { FormField } from '@/components/ui/form-field'
import { Divider } from '@/components/ui/divider'
import { WalletConnectButton } from '@/components/auth/wallet-connect-button'
import { useAuth } from '@/context/auth-context'
import { AuthApiError } from '@/lib/auth-api'

interface FieldErrors {
  email?: string
  password?: string
  confirmPassword?: string
}

function validate(email: string, password: string, confirmPassword: string): FieldErrors {
  const errors: FieldErrors = {}
  if (!/^\S+@\S+\.\S+$/.test(email)) errors.email = 'Enter a valid email address.'
  if (password.length < 8) errors.password = 'Password must be at least 8 characters.'
  if (confirmPassword !== password) errors.confirmPassword = 'Passwords do not match.'
  return errors
}

export function RegisterPage() {
  const navigate = useNavigate()
  const { register } = useAuth()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [walletAddress, setWalletAddress] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const errors = validate(email, password, confirmPassword)
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) return

    setFormError(null)
    setSubmitting(true)
    try {
      await register({ email, password, confirmPassword })
      navigate('/')
    } catch (err) {
      setFormError(
        err instanceof AuthApiError ? err.message : 'Unable to create your account right now.',
      )
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthLayout
      title="Create your account"
      subtitle="Start deploying your own shops"
      footer={
        <>
          Already have an account?{' '}
          <Link
            to="/login"
            className="font-medium text-purple-600 hover:underline dark:text-purple-400"
          >
            Sign in
          </Link>
        </>
      }
    >
      <form className="flex flex-col gap-5" onSubmit={handleSubmit} noValidate>
        {formError && <FormError message={formError} />}

        <FormField
          id="register-email"
          label="Email"
          type="email"
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          error={fieldErrors.email}
        />

        <FormField
          id="register-password"
          label="Password"
          type="password"
          autoComplete="new-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          error={fieldErrors.password}
        />

        <FormField
          id="register-confirm-password"
          label="Confirm password"
          type="password"
          autoComplete="new-password"
          value={confirmPassword}
          onChange={(e) => setConfirmPassword(e.target.value)}
          error={fieldErrors.confirmPassword}
        />

        <Button type="submit" disabled={submitting}>
          {submitting ? 'Creating account…' : 'Create account'}
        </Button>
      </form>

      <Divider>or</Divider>
      <WalletConnectButton onConnected={setWalletAddress} />
      {walletAddress && (
        <p className="mt-2 text-center text-xs text-neutral-500 dark:text-neutral-400">
          Wallet linked to your new account.
        </p>
      )}
    </AuthLayout>
  )
}
