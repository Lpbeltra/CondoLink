import axios from 'axios'
import type { RequestAttachment } from './types'

export const maximumAttachmentCount = 6
export const maximumAttachmentSize = 15 * 1024 * 1024

const allowedAttachmentTypes: Record<string, readonly string[]> = {
  '.jpg': ['image/jpeg'],
  '.jpeg': ['image/jpeg'],
  '.png': ['image/png'],
  '.webp': ['image/webp'],
  '.pdf': ['application/pdf'],
}

export interface AttachmentSelectionResult {
  files: File[]
  error: string | null
}

export function selectAttachmentFiles(
  current: File[],
  incoming: File[],
): AttachmentSelectionResult {
  const files = [...current, ...incoming]

  if (files.length > maximumAttachmentCount) {
    return {
      files: current,
      error: 'É permitido enviar no máximo 6 arquivos.',
    }
  }

  if (files.some(file => file.size > maximumAttachmentSize)) {
    return {
      files: current,
      error: 'Cada arquivo pode possuir no máximo 15 MB.',
    }
  }

  if (files.some(file => file.size <= 0)) {
    return {
      files: current,
      error: 'Não é possível enviar arquivos vazios.',
    }
  }

  const hasUnsupportedFile = files.some(file => {
    const extensionIndex = file.name.lastIndexOf('.')
    const extension = extensionIndex >= 0
      ? file.name.slice(extensionIndex).toLowerCase()
      : ''
    return !allowedAttachmentTypes[extension]?.includes(file.type.toLowerCase())
  })

  if (hasUnsupportedFile) {
    return {
      files: current,
      error: 'Formato não suportado. Envie somente JPG, PNG, WebP ou PDF.',
    }
  }

  if (files.some(file => !file.name.trim() || file.name.length > 255)) {
    return {
      files: current,
      error: 'O nome do arquivo é inválido ou possui mais de 255 caracteres.',
    }
  }

  return { files, error: null }
}

export function removeSelectedAttachment(files: File[], index: number) {
  return files.filter((_, fileIndex) => fileIndex !== index)
}

export function calculateUploadProgress(loaded: number, total?: number) {
  if (!total || total <= 0) return 0
  return Math.min(100, Math.max(0, Math.round((loaded * 100) / total)))
}

export function appendUploadedAttachments(
  current: RequestAttachment[],
  uploaded: RequestAttachment[],
) {
  return [...current, ...uploaded]
}

export function removeUploadedAttachment(
  current: RequestAttachment[],
  attachmentId: string,
) {
  return current.filter(item => item.id !== attachmentId)
}

export function formatAttachmentSize(bytes: number) {
  return bytes < 1024 * 1024
    ? `${Math.ceil(bytes / 1024)} KB`
    : `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

export function getAttachmentErrorMessage(error: unknown) {
  if (axios.isAxiosError<{ error?: string }>(error)) {
    if (!error.response) {
      return 'Não foi possível conectar ao servidor. Tente novamente.'
    }

    const serverMessage = error.response.data?.error
    if (typeof serverMessage === 'string' && serverMessage.trim()) {
      return serverMessage.trim()
    }

    if (error.response.status === 400) {
      return 'Não foi possível enviar o arquivo. Revise a seleção e tente novamente.'
    }
    if (error.response.status === 401) {
      return 'Sua sessão expirou. Entre novamente.'
    }
    if (error.response.status === 403) {
      return 'Você não possui permissão para acessar os anexos desta solicitação.'
    }
    if (error.response.status === 404) {
      return 'O anexo ou a solicitação não foi encontrado.'
    }
  }

  return 'Não foi possível concluir a operação com o anexo.'
}
