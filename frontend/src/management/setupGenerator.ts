import type {
  GeneratorTower,
  SetupIssue,
  SetupUnitRow,
} from './setupTypes'

export interface GeneratorResult {
  units: SetupUnitRow[]
  errors: SetupIssue[]
}

export function generateStructure(towers: GeneratorTower[]): GeneratorResult {
  const units: SetupUnitRow[] = []
  const errors: SetupIssue[] = []
  const identifiers = new Set<string>()
  let line = 1

  towers.forEach((tower, towerIndex) => {
    const block = tower.name.trim() || null
    if (towers.length > 1 && !block) {
      errors.push({
        line: towerIndex + 1,
        column: 'Tower',
        reason: 'Informe o nome de cada torre quando houver mais de uma.',
      })
    }
    tower.segments.forEach((segment, segmentIndex) => {
      if (segment.startFloor > segment.endFloor) {
        errors.push({
          line: segmentIndex + 1,
          column: 'Floor',
          reason: 'O andar inicial não pode ser maior que o andar final.',
        })
        return
      }
      if (segment.unitsPerFloor < 1 || segment.unitsPerFloor > 100) {
        errors.push({
          line: segmentIndex + 1,
          column: 'UnitsPerFloor',
          reason: 'Informe de 1 a 100 unidades por andar.',
        })
        return
      }
      const generatedCount = (
        segment.endFloor - segment.startFloor + 1
      ) * segment.unitsPerFloor
      if (generatedCount + units.length > 5000) {
        errors.push({
          line: segmentIndex + 1,
          column: 'UnitsPerFloor',
          reason: 'O gerador aceita no máximo 5000 unidades por lote.',
        })
        return
      }
      let sequential = segment.firstUnit
      for (
        let floor = segment.startFloor;
        floor <= segment.endFloor;
        floor++
      ) {
        for (let position = 0; position < segment.unitsPerFloor; position++) {
          const number = segment.includeFloorNumber
            ? String(segment.firstUnit + position).padStart(
                segment.digits,
                '0',
              )
            : String(sequential++).padStart(segment.digits, '0')
          const identifier = segment.includeFloorNumber
            ? `${segment.prefix}${floor}${number}${segment.suffix}`
            : `${segment.prefix}${number}${segment.suffix}`
          const key = `${block?.toLocaleLowerCase() ?? ''}\u001f${identifier.toLocaleLowerCase()}`
          if (identifiers.has(key)) {
            errors.push({
              line,
              column: 'Unit',
              reason: `A configuração gera a unidade duplicada ${identifier}.`,
            })
          } else {
            identifiers.add(key)
            units.push({
              line,
              block,
              unit: identifier,
              floor: floor === 0 ? 'Ground' : String(floor),
              description: null,
            })
          }
          line++
        }
      }
    })
  })

  return { units, errors }
}

export const createGeneratorSegment = (
  id: string = crypto.randomUUID(),
): GeneratorTower['segments'][number] => ({
  id,
  startFloor: 1,
  endFloor: 6,
  unitsPerFloor: 4,
  firstUnit: 1,
  digits: 2,
  includeFloorNumber: true,
  prefix: '',
  suffix: '',
})
