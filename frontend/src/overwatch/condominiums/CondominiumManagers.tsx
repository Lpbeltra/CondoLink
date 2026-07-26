import { ManagerRelationships } from '../managers/ManagerRelationships'

export function CondominiumManagers({
  condominiumId,
  onChanged,
}: {
  condominiumId: string
  onChanged?: () => void
}) {
  return (
    <ManagerRelationships
      condominiumId={condominiumId}
      onChanged={onChanged}
    />
  )
}
