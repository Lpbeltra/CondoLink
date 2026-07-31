import { api } from '../services/api'

export interface PhoneVerificationStatus {
  maskedPhoneNumber: string | null
  confirmed: boolean
  activeChallenge: boolean
  expiresAt: string | null
  canResend: boolean
  canResendAt: string | null
}

export interface PhoneVerificationStart {
  status: 'started' | 'already_confirmed'
  expiresAt?: string
}

export interface PhoneVerificationConfirmation {
  status: 'confirmed' | 'already_confirmed'
}

export const getPhoneVerificationStatus = async () =>
  (await api.get<PhoneVerificationStatus>('/users/me/phone-verification')).data

export const startPhoneVerification = async () =>
  (await api.post<PhoneVerificationStart>('/users/me/phone-verification')).data

export const confirmPhoneVerification = async (code: string) =>
  (await api.post<PhoneVerificationConfirmation>(
    '/users/me/phone-verification/confirm',
    { code },
  )).data
