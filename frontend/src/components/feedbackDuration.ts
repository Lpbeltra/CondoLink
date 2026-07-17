export type FeedbackSeverity = 'success' | 'info' | 'warning' | 'error'
export const feedbackDuration = (severity: FeedbackSeverity) => severity === 'success' ? 3500 : severity === 'info' ? 4000 : severity === 'warning' ? 5500 : null
