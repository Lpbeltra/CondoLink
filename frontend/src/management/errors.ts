import axios from 'axios'

const messages: Record<string, string> = {
  'Condominium not found.': 'Condomínio não encontrado.',
  'Inactive condominium cannot receive new units.': 'Este condomínio está inativo.',
  'A unit with the same identifier and block already exists in this condominium.': 'Já existe uma unidade com essa identificação neste bloco.',
  'User must be an active condominium member before being linked to a unit.': 'O usuário precisa ser membro ativo do condomínio.',
  'This unit relationship is already associated with the user.': 'Este vínculo já existe para o usuário.',
  'Inactive unit cannot receive new memberships.': 'Esta unidade está inativa.',
  'A category with this name already exists in the condominium.': 'Já existe uma categoria com este nome.',
  'Inactive condominium cannot receive new categories.': 'Este condomínio está inativo e não aceita novas categorias.',
}

export function managementError(error: unknown) {
  if (!axios.isAxiosError(error)) return 'Não foi possível concluir a operação.'
  const value = (error.response?.data as { error?: string } | undefined)?.error
  return value ? messages[value] ?? value : 'Não foi possível concluir a operação.'
}
