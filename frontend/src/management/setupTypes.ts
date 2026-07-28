export interface SetupUnitRow {
  line: number
  block: string | null
  unit: string | null
  floor: string | null
  description: string | null
}

export interface SetupResidentRow {
  line: number
  block: string | null
  unit: string | null
  name: string | null
  email: string | null
  phone: string | null
  relationship: string | null
  resident: string | null
  primaryResidence: string | null
}

export interface SetupDraft {
  noRegistrableUnits: boolean
  units: SetupUnitRow[]
  residents: SetupResidentRow[]
}

export interface SetupIssue {
  line: number
  column: string
  reason: string
}

export interface SetupPreview {
  draft: SetupDraft
  blocks: { identifier: string; existing: boolean }[]
  units: (SetupUnitRow & { unit: string; existing: boolean })[]
  residents: {
    line: number
    block: string | null
    unit: string | null
    name: string
    email: string
    phone: string | null
    relationship: string | null
    resident: boolean
    primaryResidence: boolean
    existingUser: boolean
  }[]
  warnings: SetupIssue[]
  errors: SetupIssue[]
  totals: {
    blocks: number
    units: number
    residents: number
    existingUsers: number
    newUsers: number
  }
}

export interface SetupConfirmation {
  blocksCreated: number
  unitsCreated: number
  residentsLinked: number
  credentials: {
    userId: string
    fullName: string
    email: string
    temporaryPassword: string
  }[]
  message: string
}

export interface GeneratorSegment {
  id: string
  startFloor: number
  endFloor: number
  unitsPerFloor: number
  firstUnit: number
  digits: number
  includeFloorNumber: boolean
  prefix: string
  suffix: string
}

export interface GeneratorTower {
  id: string
  name: string
  segments: GeneratorSegment[]
}
