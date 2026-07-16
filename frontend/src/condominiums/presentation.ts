export function getAccessMessage(isManager: boolean, isResident: boolean) {
  if (isManager && isResident) return 'Você possui acesso como morador e gestor.'
  if (isManager) return 'Você possui acesso à gestão deste condomínio.'
  if (isResident) return 'Você pode acompanhar suas solicitações por aqui.'
  return 'Seu acesso ao condomínio está ativo.'
}
