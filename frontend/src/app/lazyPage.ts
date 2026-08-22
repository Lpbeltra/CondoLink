import { lazy, type ComponentType } from 'react'

export function lazyPage<K extends string>(
  factory: () => Promise<Record<K, ComponentType>>,
  name: K,
) {
  return lazy(() => factory().then((module) => ({ default: module[name] })))
}
