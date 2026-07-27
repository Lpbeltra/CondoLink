import { describe, expect, it } from 'vitest'
import {
  digits, formatCnpj, formatCpf, isValidCnpj, isValidCpf,
} from './registration'

describe('Brazilian registration data', () => {
  it('normalizes and formats CPF/CNPJ', () => {
    expect(digits('04.252.011/0001-10')).toBe('04252011000110')
    expect(formatCpf('52998224725')).toBe('529.982.247-25')
    expect(formatCnpj('04252011000110')).toBe('04.252.011/0001-10')
  })

  it('validates official check digits', () => {
    expect(isValidCpf('529.982.247-25')).toBe(true)
    expect(isValidCpf('529.982.247-24')).toBe(false)
    expect(isValidCnpj('04.252.011/0001-10')).toBe(true)
    expect(isValidCnpj('04.252.011/0001-11')).toBe(false)
  })
})
