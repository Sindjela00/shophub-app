import { useState } from 'react'
import { Button } from '@/components/ui/button'

function shortenAddress(address: string): string {
  return `${address.slice(0, 6)}...${address.slice(-4)}`
}

function WalletIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" className="h-4 w-4 shrink-0">
      <path
        d="M3 6.5A1.5 1.5 0 0 1 4.5 5h11A1.5 1.5 0 0 1 17 6.5v7a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 3 13.5v-7Z"
        stroke="currentColor"
        strokeWidth="1.4"
      />
      <path d="M3 8h14" stroke="currentColor" strokeWidth="1.4" />
      <circle cx="13.25" cy="11" r="1" fill="currentColor" />
    </svg>
  )
}

export function WalletConnectButton({ onConnected }: { onConnected?: (address: string) => void }) {
  const [address, setAddress] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [connecting, setConnecting] = useState(false)

  const hasWallet = typeof window !== 'undefined' && Boolean(window.ethereum)

  const handleConnect = async () => {
    if (!window.ethereum) {
      setError('No Web3 wallet detected. Install MetaMask to continue.')
      return
    }

    setConnecting(true)
    setError(null)
    try {
      const accounts = (await window.ethereum.request({
        method: 'eth_requestAccounts',
      })) as string[]
      const connected = accounts[0]
      setAddress(connected)
      onConnected?.(connected)
    } catch {
      setError('Wallet connection was rejected.')
    } finally {
      setConnecting(false)
    }
  }

  return (
    <div className="flex flex-col items-center gap-2">
      <Button
        type="button"
        variant="secondary"
        onClick={handleConnect}
        disabled={connecting || address !== null}
      >
        <WalletIcon />
        {address ? shortenAddress(address) : connecting ? 'Connecting…' : 'Connect wallet'}
      </Button>
      {!hasWallet && !address && (
        <p className="text-center text-xs text-neutral-500 dark:text-neutral-400">
          No injected wallet found - install MetaMask.
        </p>
      )}
      {error && <p className="text-center text-xs text-red-500">{error}</p>}
    </div>
  )
}
