import type { CriticalAlert } from '../App'

interface StatBarProps {
  alerts: CriticalAlert[]
}

interface Stat {
  label: string
  value: string
  accent?: string
}

/**
 * Compact KPI row derived from the live alert feed (the last 50 alerts pushed
 * over SignalR this session). Gives the dashboard a scannable summary above the
 * detail panels rather than making the operator read the feed to gauge severity.
 */
export function StatBar({ alerts }: StatBarProps) {
  const machinesAffected = new Set(alerts.map((a) => a.machineId)).size
  const peakTemp = alerts.reduce((max, a) => Math.max(max, a.temperature), 0)

  const stats: Stat[] = [
    { label: 'Live alerts', value: String(alerts.length) },
    { label: 'Machines affected', value: String(machinesAffected) },
    {
      label: 'Peak temperature',
      value: peakTemp > 0 ? `${peakTemp.toFixed(1)}°C` : '—',
      accent: peakTemp > 0 ? 'var(--alert)' : undefined,
    },
  ]

  return (
    <div
      role="group"
      aria-label="Alert summary"
      style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
        gap: '1rem',
        marginBottom: '2rem',
      }}
    >
      {stats.map((s) => (
        <div
          key={s.label}
          style={{
            background: 'var(--surface)',
            border: '1px solid var(--border)',
            borderRadius: 12,
            padding: '1rem 1.25rem',
          }}
        >
          <div style={{ color: 'var(--muted)', fontSize: '0.8rem' }}>{s.label}</div>
          <div
            style={{
              fontFamily: 'JetBrains Mono',
              fontSize: '1.75rem',
              fontWeight: 600,
              marginTop: 4,
              color: s.accent ?? 'var(--text)',
            }}
          >
            {s.value}
          </div>
        </div>
      ))}
    </div>
  )
}
