export const brazilianStates = [
  'AC', 'AL', 'AP', 'AM', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MT', 'MS',
  'MG', 'PA', 'PB', 'PR', 'PE', 'PI', 'RJ', 'RN', 'RS', 'RO', 'RR', 'SC',
  'SP', 'SE', 'TO',
] as const

export function digits(value: string) {
  return value.replace(/\D/g, '')
}

export function formatCpf(value: string | null) {
  if (!value) return 'Não informado'
  return value.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, '$1.$2.$3-$4')
}

export function formatCnpj(value: string | null) {
  if (!value) return 'Não informado'
  return value.replace(
    /(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})/,
    '$1.$2.$3/$4-$5',
  )
}

function allEqual(value: string) {
  return new Set(value).size === 1
}

export function isValidCpf(value: string) {
  const number = digits(value)
  if (number.length !== 11 || allEqual(number)) return false
  const digit = (length: number, weight: number) => {
    let sum = 0
    for (let index = 0; index < length; index += 1)
      sum += Number(number[index]) * (weight - index)
    const remainder = sum % 11
    return remainder < 2 ? 0 : 11 - remainder
  }
  return digit(9, 10) === Number(number[9])
    && digit(10, 11) === Number(number[10])
}

export function isValidCnpj(value: string) {
  const number = digits(value)
  if (number.length !== 14 || allEqual(number)) return false
  const calculate = (length: 12 | 13) => {
    const weights = length === 12
      ? [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
      : [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
    const remainder = number.slice(0, length).split('')
      .reduce((sum, current, index) => sum + Number(current) * weights[index], 0) % 11
    return remainder < 2 ? 0 : 11 - remainder
  }
  return calculate(12) === Number(number[12])
    && calculate(13) === Number(number[13])
}
