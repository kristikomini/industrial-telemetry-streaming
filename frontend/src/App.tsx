import { useEffect, useState, useCallback } from 'react'
import { AlertFeed } from './components/AlertFeed'
import { MachineHealthChart, type TopFailingMachine } from './components/MachineHealthChart'
import { ChartSkeleton } from './components/ChartSkeleton'
import { StatBar } from './components/StatBar'
import { useAlertHub, type ConnectionStatus } from './hooks/useAlertHub'

export interface CriticalAlert {
  id: number
  machineId: string
  temperature: number
  consecutiveCount: number
  message: string
  timestamp: string
}

const API_BASE = '/api'

async function fetchTopFailing(limit = 5): Promise<TopFailingMachine[]> {
  const res = await fetch(`${API_BASE}/alerts/top-failing?limit=${limit}`)
  if (!res.ok) throw new Error('Failed to fetch')
  return res.json()
}

const STATUS_DISPLAY: Record<ConnectionStatus, { label: string; color: string }> = {
  connecting: { label: 'Connecting…', color: 'var(--muted)' },
  connected: { label: 'Live', color: 'var(--accent)' },
  reconnecting: { label: 'Reconnecting…', color: 'var(--warning)' },
  disconnected: { label: 'Offline', color: 'var(--alert)' },
}

function App() {
  const [topFailing, setTopFailing] = useState<TopFailingMachine[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)
  const { alerts, status } = useAlertHub()
  const conn = STATUS_DISPLAY[status]

  const load = useCallback(async () => {
    try {
      const data = await fetchTopFailing()
      setTopFailing(data)
      setLastUpdated(new Date())
      setError(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Unknown error')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    load()
    const interval = setInterval(load, 10000)
    return () => clearInterval(interval)
  }, [load])

  return (
    <div style={{ padding: '1.5rem', maxWidth: 1400, margin: '0 auto' }}>
      <header style={{ marginBottom: '2rem' }}>
        <h1
          style={{
            fontFamily: 'JetBrains Mono',
            fontSize: 'clamp(1.25rem, 4vw, 1.75rem)',
            margin: 0,
          }}
        >
          Motor Valley Monitor
        </h1>
        <p style={{ color: 'var(--muted)', margin: '0.25rem 0 0', display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          Factory floor alerts — real-time via SignalR
          <span
            role="status"
            aria-live="polite"
            aria-label={`Realtime connection: ${conn.label}`}
            style={{ color: conn.color, fontWeight: 600 }}
          >
            <span aria-hidden="true">● </span>{conn.label}
          </span>
          {lastUpdated && !loading && (
            <span style={{ fontSize: '0.875rem' }}>
              · Last updated {lastUpdated.toLocaleTimeString()}
            </span>
          )}
        </p>
      </header>

      {error && (
        <div
          style={{
            background: 'var(--alert)',
            color: '#fff',
            padding: '0.75rem 1rem',
            borderRadius: 8,
            marginBottom: '1rem',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: 8,
          }}
        >
          <span>{error} — is the API running?</span>
          <button
            onClick={() => {
              setLoading(true)
              load()
            }}
            style={{
              background: 'rgba(255,255,255,0.2)',
              border: 'none',
              padding: '0.5rem 1rem',
              borderRadius: 6,
              color: '#fff',
              cursor: 'pointer',
              fontWeight: 600,
            }}
          >
            Retry
          </button>
        </div>
      )}

      <StatBar alerts={alerts} />

      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
          gap: '2rem',
          alignItems: 'start',
        }}
      >
        <div>
          <h2 style={{ fontSize: '1.1rem', marginBottom: '1rem' }}>
            Top 5 Failing Machines
          </h2>
          {loading ? <ChartSkeleton /> : <MachineHealthChart data={topFailing} />}
        </div>
        <div>
          <h2 style={{ fontSize: '1.1rem', marginBottom: '1rem' }}>
            Live Alert Feed
          </h2>
          <AlertFeed alerts={alerts} />
        </div>
      </div>
    </div>
  )
}

export default App
