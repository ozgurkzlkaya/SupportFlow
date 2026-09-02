import { PushPin, PushPinOutlined, Refresh, SearchOutlined } from '@mui/icons-material'
import {
  Box,
  Chip,
  FormControl,
  IconButton,
  InputAdornment,
  InputLabel,
  List,
  ListItem,
  ListItemButton,
  MenuItem,
  Select,
  Stack,
  TextField,
  Tooltip,
  Typography,
  type SelectChangeEvent,
} from '@mui/material'
import { RequesterTypeChip } from '../common/RequesterTypeChip'
import type {
  ConversationFilter,
  ConversationFilterCounts,
  ConversationSummary,
} from '../../types/conversation'
import { formatRelativeDate } from '../../utils/formatRelativeDate'
import { formatDuration } from '../../utils/formatDuration'

interface ConversationListProps {
  conversations: ConversationSummary[]
  search: string
  selectedKey: string | null
  filter: ConversationFilter
  counts: ConversationFilterCounts
  pinningKey: string | null
  refreshing: boolean
  onSearchChange: (value: string) => void
  onFilterChange: (value: ConversationFilter) => void
  onPinToggle: (conversation: ConversationSummary) => void
  onRefresh: () => void
  onSelect: (conversation: ConversationSummary) => void
}

function getRequesterKey(conversation: ConversationSummary) {
  return `${conversation.requesterType}-${conversation.requesterId}`
}

export function ConversationList({
  conversations,
  search,
  selectedKey,
  filter,
  counts,
  pinningKey,
  refreshing,
  onSearchChange,
  onFilterChange,
  onPinToggle,
  onRefresh,
  onSelect,
}: ConversationListProps) {
  return (
    <Stack spacing={2} sx={{ height: '100%', minHeight: 0 }}>
      <Stack direction="row" sx={{ alignItems: 'flex-start', justifyContent: 'space-between' }}>
        <Box>
          <Typography sx={{ fontWeight: 800 }} variant="h6">
            Müşteriler
          </Typography>
          <Typography color="text.secondary" variant="body2">
            Tüm ticketlardaki public iletişim
          </Typography>
        </Box>
        <Tooltip title="Yenile">
          <span>
            <IconButton aria-label="Yenile" disabled={refreshing} onClick={onRefresh} size="small">
              <Refresh fontSize="small" />
            </IconButton>
          </span>
        </Tooltip>
      </Stack>

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { sm: 'center' } }}>
        <TextField
          fullWidth
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder="Müşteri, e-posta veya talep ara"
          size="small"
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchOutlined fontSize="small" />
                </InputAdornment>
              ),
            },
          }}
          value={search}
        />
        <FormControl size="small" sx={{ flexShrink: 0, minWidth: 220, width: { xs: '100%', sm: 'auto' } }}>
          <InputLabel id="conversation-pin-filter-label">Görünüm</InputLabel>
          <Select
            label="Görünüm"
            labelId="conversation-pin-filter-label"
            onChange={(event: SelectChangeEvent) => onFilterChange(event.target.value as ConversationFilter)}
            value={filter}
          >
            <MenuItem value="All">Tümü ({counts.all})</MenuItem>
            <MenuItem value="AwaitingReply">Yanıt Bekleyenler ({counts.awaitingReply})</MenuItem>
            <MenuItem value="Unread">Okunmamışlar ({counts.unread})</MenuItem>
            <MenuItem value="Pinned">Sabitlenenler ({counts.pinned})</MenuItem>
          </Select>
        </FormControl>
      </Stack>

      {conversations.length === 0 ? (
        <Typography color="text.secondary" sx={{ px: 0.5, py: 3, textAlign: 'center' }} variant="body2">
          {search.trim()
            ? 'Aramanızla eşleşen görüşme bulunamadı.'
            : 'Henüz müşteri görüşmesi bulunmuyor.'}
        </Typography>
      ) : (
        <List disablePadding sx={{ overflowY: 'auto', pr: 0.5 }}>
          {conversations.map((conversation) => {
            const isSelected = getRequesterKey(conversation) === selectedKey

            const isPinning = pinningKey === getRequesterKey(conversation)

            return (
              <ListItem
                disablePadding
                key={getRequesterKey(conversation)}
                secondaryAction={
                  <Tooltip title={conversation.isPinned ? 'Sabitlemeyi kaldır' : 'Başa tuttur'}>
                    <span>
                      <IconButton
                        aria-label={conversation.isPinned ? 'Sabitlemeyi kaldır' : 'Başa tuttur'}
                        disabled={isPinning}
                        edge="end"
                        onClick={(event) => {
                          event.preventDefault()
                          event.stopPropagation()
                          onPinToggle(conversation)
                        }}
                        size="small"
                        sx={{ color: conversation.isPinned ? 'primary.main' : 'text.disabled' }}
                      >
                        {conversation.isPinned ? <PushPin fontSize="small" /> : <PushPinOutlined fontSize="small" />}
                      </IconButton>
                    </span>
                  </Tooltip>
                }
              >
                <ListItemButton
                  onClick={() => onSelect(conversation)}
                  selected={isSelected}
                  sx={{
                    alignItems: 'flex-start',
                    border: '1px solid',
                    borderColor: isSelected ? 'primary.light' : 'transparent',
                    borderRadius: 3,
                    bgcolor: isSelected ? 'rgba(124, 58, 237, 0.055)' : 'transparent',
                    mb: 0.75,
                    px: 1.25,
                    py: 1.2,
                    pr: 5.5,
                    '&.Mui-selected': {
                      bgcolor: 'rgba(124, 58, 237, 0.055)',
                    },
                    '&.Mui-selected:hover': {
                      bgcolor: 'rgba(124, 58, 237, 0.085)',
                    },
                  }}
                >
                  <Stack spacing={0.55} sx={{ minWidth: 0, width: '100%' }}>
                    <Stack direction="row" spacing={1} sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
                      <Typography noWrap sx={{ fontWeight: conversation.isUnread ? 800 : 750 }} variant="body2">
                        {conversation.displayName}
                      </Typography>
                      <Typography color="text.secondary" sx={{ flexShrink: 0 }} variant="caption">
                        {formatRelativeDate(conversation.lastActivityAt)}
                      </Typography>
                    </Stack>
                    <Typography color="text.secondary" noWrap variant="caption">
                      {conversation.email}
                    </Typography>
                    <Stack direction="row" spacing={0.65} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                      <RequesterTypeChip requesterType={conversation.requesterType} />
                      {conversation.slaBreachedTicketCount > 0 ? (
                        <Chip
                          color="error"
                          label={`SLA İhlali (${conversation.slaBreachedTicketCount})`}
                          size="small"
                          variant="outlined"
                        />
                      ) : conversation.slaDueSoonTicketCount > 0 ? (
                        <Chip
                          color="warning"
                          label={`SLA Yaklaşıyor (${conversation.slaDueSoonTicketCount})`}
                          size="small"
                          variant="outlined"
                        />
                      ) : null}
                      {conversation.isUnread ? (
                        <Box aria-label="Okunmamış ileti" sx={{ bgcolor: 'primary.main', borderRadius: '50%', height: 8, width: 8 }} />
                      ) : null}
                      <Typography color="text.secondary" variant="caption">
                        {conversation.ticketCount} talep
                      </Typography>
                    </Stack>
                    {conversation.isAwaitingStaffReply && conversation.oldestAwaitingMinutes !== null ? (
                      <Stack spacing={0.15}>
                        <Typography color="warning.dark" sx={{ fontWeight: 700 }} variant="caption">
                          {conversation.awaitingReplyCount === 1
                            ? `Yanıt bekliyor · ${formatDuration(conversation.oldestAwaitingMinutes)}`
                            : `${conversation.awaitingReplyCount} talep yanıt bekliyor`}
                        </Typography>
                        {conversation.awaitingReplyCount > 1 ? (
                          <Typography color="text.secondary" variant="caption">
                            En uzun: {formatDuration(conversation.oldestAwaitingMinutes)}
                          </Typography>
                        ) : null}
                      </Stack>
                    ) : null}
                    <Typography
                      color="text.secondary"
                      sx={{ display: '-webkit-box', overflow: 'hidden', WebkitBoxOrient: 'vertical', WebkitLineClamp: 2 }}
                      variant="body2"
                    >
                      {conversation.lastMessagePreview}
                    </Typography>
                  </Stack>
                </ListItemButton>
              </ListItem>
            )
          })}
        </List>
      )}
    </Stack>
  )
}
