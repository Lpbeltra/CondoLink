import { Alert, Snackbar } from '@mui/material'
import { feedbackDuration, type FeedbackSeverity } from './feedbackDuration'

export function TransientFeedback({ message, severity, onClose }: { message: string; severity: FeedbackSeverity; onClose: () => void }) {
  return <Snackbar open={Boolean(message)} autoHideDuration={feedbackDuration(severity)} onClose={(_,reason)=>{if(reason!=='clickaway')onClose()}} anchorOrigin={{vertical:'bottom',horizontal:'center'}}><Alert severity={severity} variant="filled" role="status" onClose={onClose} sx={{width:'100%'}}>{message}</Alert></Snackbar>
}
