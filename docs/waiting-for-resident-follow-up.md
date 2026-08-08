# Semântica de estados de espera

- `WaitingForResident` mantém o valor persistido e representa espera por informação do morador. Sua apresentação é “Aguardando morador” e somente ele usa `ResidentReplyRequirement`/“Responder agora”.
- `WaitingForManager` representa espera por ação da administração. Sua apresentação no portal é “Aguardando você”; não cria requisito de resposta do morador.
- `WaitingForThirdParty` permanece “Aguardando terceiro”.

Os valores existentes do enum inteiro foram preservados; `WaitingForManager` foi acrescentado como novo valor.
