import { useCallback, useEffect, useState } from 'react'

export interface MachineNote {
  id: number
  machineId: string
  text: string
  author: string
  createdAt: string
}

interface MachineNotesProps {
  machineId: string | null
}

const API_BASE = '/api'

const cardStyle: React.CSSProperties = {
  background: 'var(--surface)',
  border: '1px solid var(--border)',
  borderRadius: 8,
  padding: '0.75rem 1rem',
}

const inputStyle: React.CSSProperties = {
  width: '100%',
  boxSizing: 'border-box',
  background: 'var(--bg, #111)',
  border: '1px solid var(--border)',
  borderRadius: 6,
  color: 'inherit',
  padding: '0.5rem',
  font: 'inherit',
}

const buttonStyle: React.CSSProperties = {
  border: 'none',
  borderRadius: 6,
  padding: '0.5rem 1rem',
  cursor: 'pointer',
  fontWeight: 600,
}

export function MachineNotes({ machineId }: MachineNotesProps) {
  const [notes, setNotes] = useState<MachineNote[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Add-form state
  const [text, setText] = useState('')
  const [author, setAuthor] = useState('')
  const [saving, setSaving] = useState(false)

  // Inline-edit state: which note is being edited, and its draft values
  const [editingId, setEditingId] = useState<number | null>(null)
  const [editText, setEditText] = useState('')
  const [editAuthor, setEditAuthor] = useState('')

  const load = useCallback(async () => {
    if (!machineId) return
    setLoading(true)
    try {
      const res = await fetch(`${API_BASE}/machines/${machineId}/notes`)
      if (!res.ok) throw new Error(`Failed to load notes (${res.status})`)
      setNotes(await res.json())
      setError(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Unknown error')
    } finally {
      setLoading(false)
    }
  }, [machineId])

  // Reload whenever the selected machine changes.
  useEffect(() => {
    setEditingId(null)
    load()
  }, [load])

  async function addNote(e: React.FormEvent) {
    e.preventDefault()
    if (!machineId || !text.trim() || !author.trim()) return
    setSaving(true)
    try {
      const res = await fetch(`${API_BASE}/machines/${machineId}/notes`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ text: text.trim(), author: author.trim() }),
      })
      if (!res.ok) throw new Error(`Failed to save note (${res.status})`)
      setText('')
      setAuthor('')
      setError(null)
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Unknown error')
    } finally {
      setSaving(false)
    }
  }

  function startEdit(note: MachineNote) {
    setEditingId(note.id)
    setEditText(note.text)
    setEditAuthor(note.author)
  }

  async function saveEdit(id: number) {
    if (!machineId || !editText.trim() || !editAuthor.trim()) return
    try {
      const res = await fetch(`${API_BASE}/machines/${machineId}/notes/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ text: editText.trim(), author: editAuthor.trim() }),
      })
      if (!res.ok) throw new Error(`Failed to update note (${res.status})`)
      setEditingId(null)
      setError(null)
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Unknown error')
    }
  }

  async function deleteNote(id: number) {
    if (!machineId) return
    try {
      const res = await fetch(`${API_BASE}/machines/${machineId}/notes/${id}`, {
        method: 'DELETE',
      })
      if (!res.ok) throw new Error(`Failed to delete note (${res.status})`)
      setError(null)
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Unknown error')
    }
  }

  if (!machineId) {
    return (
      <div style={{ ...cardStyle, color: 'var(--muted)', textAlign: 'center', padding: '2rem' }}>
        No machine selected.
      </div>
    )
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
      {/* Add-note form */}
      <form onSubmit={addNote} style={{ ...cardStyle, display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
        <textarea
          aria-label="Note text"
          placeholder="Add a note for this machine…"
          value={text}
          maxLength={2000}
          rows={2}
          onChange={(e) => setText(e.target.value)}
          style={{ ...inputStyle, resize: 'vertical' }}
        />
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <input
            aria-label="Author"
            placeholder="Your name"
            value={author}
            maxLength={100}
            onChange={(e) => setAuthor(e.target.value)}
            style={{ ...inputStyle, flex: 1, minWidth: 120 }}
          />
          <button
            type="submit"
            disabled={saving || !text.trim() || !author.trim()}
            style={{
              ...buttonStyle,
              background: 'var(--accent)',
              color: '#000',
              opacity: saving || !text.trim() || !author.trim() ? 0.5 : 1,
            }}
          >
            {saving ? 'Saving…' : 'Add note'}
          </button>
        </div>
      </form>

      {error && (
        <div style={{ ...cardStyle, borderColor: 'var(--alert)', color: 'var(--alert)' }}>
          {error}
        </div>
      )}

      {/* Notes list */}
      {loading ? (
        <div style={{ ...cardStyle, color: 'var(--muted)', textAlign: 'center' }}>Loading notes…</div>
      ) : notes.length === 0 ? (
        <div style={{ ...cardStyle, color: 'var(--muted)', textAlign: 'center', padding: '2rem' }}>
          No notes yet for {machineId}.
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', maxHeight: 400, overflowY: 'auto' }}>
          {notes.map((n) => (
            <div key={n.id} style={cardStyle}>
              {editingId === n.id ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                  <textarea
                    aria-label="Edit note text"
                    value={editText}
                    maxLength={2000}
                    rows={2}
                    onChange={(e) => setEditText(e.target.value)}
                    style={{ ...inputStyle, resize: 'vertical' }}
                  />
                  <input
                    aria-label="Edit author"
                    value={editAuthor}
                    maxLength={100}
                    onChange={(e) => setEditAuthor(e.target.value)}
                    style={inputStyle}
                  />
                  <div style={{ display: 'flex', gap: '0.5rem' }}>
                    <button
                      onClick={() => saveEdit(n.id)}
                      disabled={!editText.trim() || !editAuthor.trim()}
                      style={{ ...buttonStyle, background: 'var(--accent)', color: '#000' }}
                    >
                      Save
                    </button>
                    <button
                      onClick={() => setEditingId(null)}
                      style={{ ...buttonStyle, background: 'transparent', border: '1px solid var(--border)', color: 'inherit' }}
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : (
                <>
                  <div style={{ whiteSpace: 'pre-wrap' }}>{n.text}</div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 8, gap: 8, flexWrap: 'wrap' }}>
                    <span style={{ color: 'var(--muted)', fontSize: '0.75rem' }}>
                      {n.author} · {new Date(n.createdAt).toLocaleString()}
                    </span>
                    <span style={{ display: 'flex', gap: '0.5rem' }}>
                      <button
                        onClick={() => startEdit(n)}
                        style={{ ...buttonStyle, padding: '0.25rem 0.75rem', background: 'transparent', border: '1px solid var(--border)', color: 'inherit', fontSize: '0.75rem' }}
                      >
                        Edit
                      </button>
                      <button
                        onClick={() => deleteNote(n.id)}
                        style={{ ...buttonStyle, padding: '0.25rem 0.75rem', background: 'transparent', border: '1px solid var(--alert)', color: 'var(--alert)', fontSize: '0.75rem' }}
                      >
                        Delete
                      </button>
                    </span>
                  </div>
                </>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
