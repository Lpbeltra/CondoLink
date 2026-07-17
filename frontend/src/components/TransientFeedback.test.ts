import { describe, expect, it } from 'vitest'
import { feedbackDuration } from './feedbackDuration'

describe('feedback duration', () => {
  it('automatically closes transient messages', () => { expect(feedbackDuration('success')).toBe(3500); expect(feedbackDuration('info')).toBe(4000); expect(feedbackDuration('warning')).toBe(5500) })
  it('keeps errors visible until manual close', () => expect(feedbackDuration('error')).toBeNull())
})
