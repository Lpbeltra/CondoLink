export interface TemporaryCredentials {
  fullName: string
  email: string
  temporaryPassword: string
}

export function temporaryCredentialsWhatsAppText(
  credentials: TemporaryCredentials,
  origin = window.location.origin,
) {
  const loginUrl = new URL('/login', origin).toString()
  return [
    `Olá, ${credentials.fullName}! Seu acesso à Comvy foi criado.`,
    '',
    `E-mail: ${credentials.email}`,
    '',
    'Senha temporária:',
    `\`${credentials.temporaryPassword}\``,
    '',
    'Acesse:',
    loginUrl,
    '',
    'No primeiro acesso, você deverá criar uma nova senha.',
  ].join('\n')
}
