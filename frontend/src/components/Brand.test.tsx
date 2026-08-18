import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { brandIconColors } from '../theme/tokens'
import { Brand } from './Brand'

describe('Brand', () => {
  it('renders one accessible brand name and hides the decorative mark', () => {
    const { container } = render(<Brand />)
    expect(screen.getByRole('img', { name: 'Comvy' })).toBeInTheDocument()
    expect(screen.getByText('Comvy')).toBeInTheDocument()
    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true')
  })

  it('uses the shared colors for the circular mark, bubble and C', () => {
    const { container } = render(<Brand compact />)
    expect(container.querySelector('circle')).toHaveAttribute('fill', brandIconColors.background)
    const paths = container.querySelectorAll('path')
    expect(paths[0]).toHaveAttribute('fill', brandIconColors.foreground)
    expect(paths[1]).toHaveAttribute('fill', brandIconColors.accent)
    expect(screen.queryByText('Comvy')).not.toBeInTheDocument()
  })
})
