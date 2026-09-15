import type { CriticalAlert } from '../App'

interface AlertFeedProps {
  alerts: CriticalAlert[]
}

export function AlertFeed({ alerts }: AlertFeedProps) {
  if (alerts.length === 0) {
    return (
      <div
        style={{
          color: 'var(--muted)',
          padding: '2rem',
          textAlign: 'center',
          background: 'var(--surface)',
          border: '1px solid var(--border)',
          borderRadius: 12,
        }}
      >
        No alerts yet. Waiting for critical events...
      </div>
    )
  }

  return (
    <div
      role="log"
      aria-live="polite"
      aria-relevant="additions"
      aria-label="Live critical alert feed"
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
        maxHeight: 400,
        overflowY: 'auto',
      }}
    >
      {alerts.map((a, i) => (
        <div
          key={`${a.machineId}-${a.timestamp}-${i}`}
          style={{
            background: 'var(--surface)',
            border: '1px solid var(--border)',
            borderRadius: 8,
            padding: '0.75rem 1rem',
            flexShrink: 0,
          }}
        >
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
            }}
          >
            <span style={{ fontFamily: 'JetBrains Mono', fontWeight: 600 }}>
              {a.machineId}
            </span>
            <span style={{ color: 'var(--alert)', fontSize: '0.75rem', fontWeight: 600 }}>
              CRITICAL
            </span>
          </div>
          <div style={{ color: 'var(--muted)', fontSize: '0.875rem', marginTop: 4 }}>
            T={a.temperature.toFixed(1)}°C · {a.consecutiveCount} consecutive
          </div>
          <div style={{ color: 'var(--muted)', fontSize: '0.75rem', marginTop: 2 }}>
            {new Date(a.timestamp).toLocaleTimeString()}
          </div>
        </div>
      ))}
    </div>
  )
}
