import { describe, expect, it } from 'vitest'
import {
  createGeneratorSegment,
  generateStructure,
} from './setupGenerator'
import type { GeneratorTower } from './setupTypes'

describe('setup structure generator', () => {
  it('generates multiple towers and different floor segments', () => {
    const towers: GeneratorTower[] = [
      {
        id: 'a',
        name: 'Tower A',
        segments: [
          {
            ...createGeneratorSegment('a1'),
            startFloor: 1,
            endFloor: 2,
            unitsPerFloor: 2,
          },
          {
            ...createGeneratorSegment('a2'),
            startFloor: 7,
            endFloor: 7,
            unitsPerFloor: 1,
            suffix: '-A',
          },
        ],
      },
      {
        id: 'b',
        name: 'Tower B',
        segments: [{
          ...createGeneratorSegment('b1'),
          startFloor: 0,
          endFloor: 0,
          unitsPerFloor: 1,
          includeFloorNumber: false,
          firstUnit: 1,
          digits: 2,
          prefix: 'Store ',
        }],
      },
    ]

    const result = generateStructure(towers)

    expect(result.errors).toEqual([])
    expect(result.units.map(item => item.unit)).toEqual([
      '101', '102', '201', '202', '701-A', 'Store 01',
    ])
    expect(result.units.at(-1)?.floor).toBe('Ground')
  })

  it('reports identifiers duplicated by an invalid segment configuration', () => {
    const segment = {
      ...createGeneratorSegment('segment'),
      startFloor: 1,
      endFloor: 2,
      unitsPerFloor: 1,
      includeFloorNumber: false,
    }

    const result = generateStructure([{
      id: 'tower',
      name: '',
      segments: [segment, { ...segment, id: 'duplicate' }],
    }])

    expect(result.errors.some(issue =>
      issue.reason.includes('duplicada'))).toBe(true)
  })
})
