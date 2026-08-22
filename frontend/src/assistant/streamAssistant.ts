import { getStoredToken } from '../auth/authStorage'
import { getErrorMessageForStatus } from '../services/api'
import type { AssistantConversation, AssistantSource } from './api'

const baseURL = import.meta.env.VITE_API_URL || '/api'

export interface AssistantStreamResult {
  answer: string
  sources: AssistantSource[]
  conversation?: AssistantConversation
}

export interface AssistantStreamHandlers {
  onSources?: (sources: AssistantSource[]) => void
  onToken?: (delta: string) => void
  onDone?: (result: AssistantStreamResult) => void
  onError?: (message: string) => void
}

/**
 * Posts to an assistant endpoint requesting a streamed answer. The backend
 * only actually streams when its feature flag is enabled; otherwise it
 * replies with a normal JSON body exactly like today. This function inspects
 * the response's content type and handles both shapes, so the caller only
 * ever deals with the handlers below regardless of which one the backend
 * used — this keeps the frontend deployable ahead of the flag being turned on.
 */
export async function streamAssistant(
  path: string,
  body: unknown,
  handlers: AssistantStreamHandlers,
  signal: AbortSignal,
): Promise<void> {
  const token = getStoredToken()
  let response: Response
  try {
    response = await fetch(`${baseURL}${path}?stream=true`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify(body),
      signal,
    })
  } catch {
    if (signal.aborted) return
    handlers.onError?.(getErrorMessageForStatus(undefined))
    return
  }

  if (!response.ok) {
    if (!signal.aborted) handlers.onError?.(getErrorMessageForStatus(response.status))
    return
  }

  if (response.headers.get('content-type')?.includes('text/event-stream')) {
    await consumeEventStream(response, handlers, signal)
    return
  }

  try {
    const data = (await response.json()) as {
      answer: string
      sources: AssistantSource[]
      conversation?: AssistantConversation
    }
    handlers.onSources?.(data.sources)
    handlers.onDone?.(data)
  } catch {
    if (!signal.aborted) handlers.onError?.(getErrorMessageForStatus(undefined))
  }
}

async function consumeEventStream(
  response: Response,
  handlers: AssistantStreamHandlers,
  signal: AbortSignal,
): Promise<void> {
  const reader = response.body?.getReader()
  if (!reader) {
    handlers.onError?.(getErrorMessageForStatus(undefined))
    return
  }
  const decoder = new TextDecoder()
  let buffer = ''
  try {
    while (true) {
      const { done, value } = await reader.read()
      if (done) break
      buffer += decoder.decode(value, { stream: true })
      let boundary = buffer.indexOf('\n\n')
      while (boundary !== -1) {
        processFrame(buffer.slice(0, boundary), handlers)
        buffer = buffer.slice(boundary + 2)
        boundary = buffer.indexOf('\n\n')
      }
    }
  } catch {
    if (!signal.aborted) handlers.onError?.(getErrorMessageForStatus(undefined))
  }
}

function processFrame(frame: string, handlers: AssistantStreamHandlers) {
  let eventName = ''
  let data = ''
  for (const line of frame.split('\n')) {
    if (line.startsWith('event:')) eventName = line.slice('event:'.length).trim()
    else if (line.startsWith('data:')) data += line.slice('data:'.length).trim()
  }
  if (!eventName || !data) return
  let payload: unknown
  try {
    payload = JSON.parse(data)
  } catch {
    return
  }
  const record = payload as Record<string, unknown>
  if (eventName === 'sources') handlers.onSources?.((record.sources as AssistantSource[]) ?? [])
  else if (eventName === 'token') handlers.onToken?.((record.delta as string) ?? '')
  else if (eventName === 'done') {
    handlers.onDone?.({
      answer: (record.answer as string) ?? '',
      sources: (record.sources as AssistantSource[]) ?? [],
      conversation: record.conversation as AssistantConversation | undefined,
    })
  } else if (eventName === 'error') {
    handlers.onError?.((record.message as string) ?? getErrorMessageForStatus(undefined))
  }
}
