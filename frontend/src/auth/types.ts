export interface User {
  id: string
  fullName: string
  email: string
  phoneNumber?: string | null
  isActive: boolean
  createdAt?: string
  updatedAt?: string
  roles?: string[]
  hasAdministratorAccess?: boolean
}

export interface LoginResponse {
  requiresPasswordChange: false
  accessToken: string
  tokenType: string
  expiresIn: number
  user: User
}

export interface PasswordChangeRequiredResponse {
  requiresPasswordChange: true
  email: string
}

export type LoginOutcome =
  | { requiresPasswordChange: false }
  | {
      requiresPasswordChange: true
      email: string
      temporaryPassword: string
    }
