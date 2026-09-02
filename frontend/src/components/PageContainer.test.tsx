import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { PageContainer, PageContainerScope } from './PageContainer'

describe('PageContainer layout scope', () => {
  it('keeps centered max width by default', () => {
    const { container } = render(<PageContainer data-testid="container" />)
    const element = container.firstElementChild as HTMLElement
    expect(element).toHaveStyle({ maxWidth: '1440px', marginLeft: 'auto', marginRight: 'auto' })
  })

  it('uses full available width inside Overwatch scope', () => {
    const { container } = render(<PageContainerScope fullWidth><PageContainer data-testid="container" /></PageContainerScope>)
    const element = container.firstElementChild as HTMLElement
    expect(element).toHaveStyle({ maxWidth: 'none', marginLeft: '0px', marginRight: '0px' })
  })
})
