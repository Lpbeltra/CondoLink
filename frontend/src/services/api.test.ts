import type {
  AxiosAdapter,
  AxiosResponse,
  InternalAxiosRequestConfig,
} from 'axios'
import { describe, expect, it } from 'vitest'
import { api } from './api'

interface CapturedRequest {
  data: unknown
  contentType: string | undefined
}

function captureAdapter(captured: CapturedRequest): AxiosAdapter {
  return async (
    config: InternalAxiosRequestConfig,
  ): Promise<AxiosResponse> => {
    captured.data = config.data
    const contentType = config.headers.get('Content-Type')
    captured.contentType = typeof contentType === 'string'
      ? contentType
      : undefined
    return {
      config,
      data: null,
      headers: {},
      status: 200,
      statusText: 'OK',
    }
  }
}

describe('global HTTP client payload handling', () => {
  it('keeps FormData intact and does not force application/json', async () => {
    const form = new FormData()
    form.append(
      'files',
      new File(['image'], 'foto.jpg', { type: 'image/jpeg' }),
    )
    const captured: CapturedRequest = {
      data: null,
      contentType: undefined,
    }

    await api.post('/multipart-probe', form, {
      adapter: captureAdapter(captured),
    })

    expect(captured.data).toBe(form)
    expect(captured.contentType).not.toBe('application/json')
    expect(form.getAll('files')).toHaveLength(1)
  })

  it('continues serializing ordinary objects as JSON', async () => {
    const captured: CapturedRequest = {
      data: null,
      contentType: undefined,
    }

    await api.post('/json-probe', { value: 42 }, {
      adapter: captureAdapter(captured),
    })

    expect(captured.data).toBe('{"value":42}')
    expect(captured.contentType).toBe('application/json')
  })
})
