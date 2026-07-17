import { describe, expect, it } from 'vitest'
import { canSubmitStatus, getRequestActionVisibility, getStatusConfirmation, requestShortcutStatuses } from './requestActions'

describe('request management actions', () => {
  it('offers resolve and cancel shortcuts only for active requests', () => {
    expect(getRequestActionVisibility('Open')).toMatchObject({ resolve: true, cancel: true, reopen: false })
    expect(getRequestActionVisibility('Resolved')).toEqual({ reopen: true, changeStatus: false, changePriority: false, resolve: false, cancel: false })
    expect(getRequestActionVisibility('Cancelled')).toEqual({ reopen: true, changeStatus: false, changePriority: false, resolve: false, cancel: false })
  })

  it('maps the shortcuts to their exact statuses', () => {
    expect(requestShortcutStatuses).toEqual({ resolve: 'Resolved', cancel: 'Cancelled' })
  })

  it('requires explicit confirmation for both shortcuts', () => {
    expect(getStatusConfirmation(requestShortcutStatuses.resolve)).toBe('Deseja marcar esta solicitação como resolvida?')
    expect(getStatusConfirmation(requestShortcutStatuses.cancel)).toBe('Deseja cancelar esta solicitação?')
  })

  it('prevents duplicate status submissions while saving', () => {
    expect(canSubmitStatus('Resolved', false)).toBe(true)
    expect(canSubmitStatus('Resolved', true)).toBe(false)
  })
})
