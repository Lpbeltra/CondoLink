export interface User {
  id: string
  fullName: string
  email: string
  phoneNumber?: string | null
  isActive: boolean
  createdAt?: string
  updatedAt?: string
}

export interface LoginResponse {
  accessToken: string
  tokenType: string
  expiresIn: number
  user: User
}
