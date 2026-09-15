import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { CriticalAlert } from '../App'

const HUB_URL = '/hubs/alerts'

export type ConnectionStatus =
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'disconnected'

export function useAlertHub() {
  const [alerts, setAlerts] = useState<CriticalAlert[]>([])
  const [status, setStatus] = useState<ConnectionStatus>('connecting')

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .build()

    connection.on('ReceiveAlert', (alert: CriticalAlert) => {
      setAlerts((prev) => [alert, ...prev].slice(0, 50))
    })

    // Track the full connection lifecycle so the UI never claims "Live" while the
    // socket is actually down — automatic reconnect fires these as it drops/recovers.
    connection.onreconnecting(() => setStatus('reconnecting'))
    connection.onreconnected(() => setStatus('connected'))
    connection.onclose(() => setStatus('disconnected'))

    setStatus('connecting')
    connection
      .start()
      .then(() => setStatus('connected'))
      .catch(() => setStatus('disconnected'))

    return () => {
      connection.stop()
    }
  }, [])

  return { alerts, status }
}
