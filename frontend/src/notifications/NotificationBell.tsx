import { useCallback, useEffect, useRef, useState } from 'react'
import NotificationsNoneRoundedIcon from '@mui/icons-material/NotificationsNoneRounded'
import DoneAllRoundedIcon from '@mui/icons-material/DoneAllRounded'
import {
  Alert, Badge, Box, Button, CircularProgress, Divider, IconButton, List, ListItemButton,
  Menu, Tooltip, Typography, alpha,
} from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { useCondominium } from '../condominiums/CondominiumContext'
import {
  listNotifications, markAllNotificationsRead, markNotificationRead,
} from './api'
import {
  countUnread, formatBadgeCount, formatRelativeTime, isUnread,
  markAllReadLocally, markReadLocally, notificationLink,
} from './presentation'
import type { AppNotification } from './types'
import { useVisiblePolling } from '../hooks/useVisiblePolling'

export function NotificationBell() {
  const navigate = useNavigate()
  const { isManager } = useCondominium()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)
  const [items, setItems] = useState<AppNotification[]>([])
  const [unreadCount, setUnreadCount] = useState(0)
  const [isLoading, setIsLoading] = useState(false)
  const [feedback, setFeedback] = useState('')
  const loadVersion = useRef(0)

  const refresh = useCallback(async () => {
    const version = ++loadVersion.current
    try {
      const result = await listNotifications({ take: 20 })
      if (version !== loadVersion.current) return
      setItems(result.items)
      setUnreadCount(result.unreadCount)
    } catch {
      // A failed poll must not surface an error over the whole app shell;
      // the next tick retries.
    }
  }, [])

  useEffect(() => {
    void refresh()
    return () => {
      loadVersion.current += 1
    }
  }, [refresh])
  const poll = useCallback(() => refresh(), [refresh])
  useVisiblePolling(poll)

  const open = async (event: React.MouseEvent<HTMLElement>) => {
    setAnchor(event.currentTarget)
    setIsLoading(true)
    await refresh()
    setIsLoading(false)
  }

  const handleSelect = async (notification: AppNotification) => {
    setAnchor(null)
    if (isUnread(notification)) {
      // Optimistic: reflect the read state immediately, then persist.
      setItems((current) => markReadLocally(current, notification.id, new Date().toISOString()))
      setUnreadCount((current) => Math.max(0, current - 1))
      try {
        await markNotificationRead(notification.id)
      } catch {
        void refresh()
      }
    }
    const link = notificationLink(notification, isManager)
    if (link) navigate(link)
  }

  const handleMarkAll = async () => {
    // Invalidate a refresh started while the menu was opening so its stale
    // unread count cannot restore the badge after this optimistic update.
    loadVersion.current += 1
    setItems((current) => markAllReadLocally(current, new Date().toISOString()))
    setUnreadCount(0)
    try {
      await markAllNotificationsRead()
      setFeedback('Todas as notificações foram marcadas como lidas.')
    } catch {
      setFeedback('')
      void refresh()
    }
  }

  const badge = formatBadgeCount(unreadCount)

  return (
    <>
      <Tooltip title="Notificações">
        <IconButton
          onClick={open}
          color="inherit"
          aria-label={
            unreadCount > 0
              ? `Notificações, ${unreadCount} não lidas`
              : 'Notificações'
          }
          aria-haspopup="menu"
          aria-expanded={anchor ? 'true' : undefined}
          sx={{ minWidth: 44, minHeight: 44 }}
        >
          <Badge badgeContent={badge} color="error" overlap="circular">
            <NotificationsNoneRoundedIcon fontSize="small" />
          </Badge>
        </IconButton>
      </Tooltip>

      <Menu
        anchorEl={anchor}
        open={Boolean(anchor)}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { width: { xs: 320, sm: 380 }, maxHeight: 460 } } }}
      >
        <Box
          px={2}
          py={1.5}
          display="flex"
          alignItems="center"
          justifyContent="space-between"
          gap={1}
        >
          <Typography fontWeight={750}>Notificações</Typography>
          <Button
            size="small"
            startIcon={<DoneAllRoundedIcon />}
            onClick={handleMarkAll}
            disabled={countUnread(items) === 0}
          >
            Limpar todas
          </Button>
        </Box>
        <Divider />
        {feedback && (
          <Alert severity="success" sx={{ m: 1 }}>
            {feedback}
          </Alert>
        )}

        {isLoading && items.length === 0 ? (
          <Box display="grid" sx={{ placeItems: 'center' }} py={4}>
            <CircularProgress size={24} />
          </Box>
        ) : items.length === 0 ? (
          <Box px={2} py={4} textAlign="center">
            <Typography color="text.secondary" fontSize=".9rem">
              Nenhuma notificação por aqui.
            </Typography>
          </Box>
        ) : (
          <List disablePadding>
            {items.map((notification) => (
              <ListItemButton
                key={notification.id}
                onClick={() => { void handleSelect(notification) }}
                sx={(theme) => ({
                  alignItems: 'flex-start',
                  gap: 1,
                  py: 1.25,
                  bgcolor: isUnread(notification)
                    ? alpha(theme.palette.primary.main, 0.07)
                    : 'transparent',
                })}
              >
                <Box flex={1} minWidth={0}>
                  <Box display="flex" justifyContent="space-between" gap={1}>
                    <Typography
                      fontSize=".875rem"
                      fontWeight={isUnread(notification) ? 750 : 600}
                      noWrap
                    >
                      {notification.title}
                    </Typography>
                    <Typography color="text.secondary" fontSize=".75rem" flexShrink={0}>
                      {formatRelativeTime(notification.createdAt)}
                    </Typography>
                  </Box>
                  <Typography color="text.secondary" fontSize=".8125rem" mt={0.25}>
                    {notification.body}
                  </Typography>
                </Box>
                {/* Unread is conveyed by weight and background too, not colour alone. */}
                {isUnread(notification) && (
                  <Box
                    width={8}
                    height={8}
                    mt={0.75}
                    borderRadius="50%"
                    bgcolor="primary.main"
                    flexShrink={0}
                    aria-hidden
                  />
                )}
              </ListItemButton>
            ))}
          </List>
        )}
      </Menu>
    </>
  )
}
