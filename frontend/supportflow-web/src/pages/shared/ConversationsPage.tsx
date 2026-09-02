import { ArrowBackOutlined, ForumOutlined } from '@mui/icons-material'
import { Alert, Box, Button, CircularProgress, Paper, Stack } from '@mui/material'
import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { ConversationHeader } from '../../components/conversations/ConversationHeader'
import { ConversationList } from '../../components/conversations/ConversationList'
import { ConversationTimeline } from '../../components/conversations/ConversationTimeline'
import { EmptyStateCard } from '../../components/common/EmptyStateCard'
import { PageHeader } from '../../components/common/PageHeader'
import {
  getConversation,
  getConversations,
  markConversationRead,
  pinConversation,
  unpinConversation,
} from '../../services/conversationService'
import { retryMessageDelivery } from '../../services/ticketService'
import type {
  ConversationDetail,
  ConversationFilter,
  ConversationFilterCounts,
  ConversationSummary,
} from '../../types/conversation'
import { getFriendlyApiErrorMessage } from '../../utils/getApiErrorMessage'

function getConversationKey(conversation: ConversationSummary) {
  return `${conversation.requesterType}-${conversation.requesterId}`
}

const summaryRefreshIntervalMs = 60_000
const emptyCounts: ConversationFilterCounts = {
  all: 0,
  awaitingReply: 0,
  unread: 0,
  pinned: 0,
}

export function ConversationsPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const routeBase = location.pathname.startsWith('/admin') ? '/admin' : '/technical'
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState<ConversationFilter>('All')
  const [conversations, setConversations] = useState<ConversationSummary[]>([])
  const [counts, setCounts] = useState<ConversationFilterCounts>(emptyCounts)
  const [selected, setSelected] = useState<ConversationSummary | null>(null)
  const [detail, setDetail] = useState<ConversationDetail | null>(null)
  const [loadingList, setLoadingList] = useState(true)
  const [loadingDetail, setLoadingDetail] = useState(false)
  const [refreshingSummaries, setRefreshingSummaries] = useState(false)
  const [error, setError] = useState('')
  const [refreshWarning, setRefreshWarning] = useState('')
  const [retryingMessageId, setRetryingMessageId] = useState<number | null>(null)
  const [pinningKey, setPinningKey] = useState<string | null>(null)
  const summaryRequestInFlight = useRef(false)
  const summaryRefreshQueued = useRef(false)
  const summaryQuery = useRef({ search, filter })

  async function refreshSummaries(mode: 'initial' | 'manual' | 'background') {
    if (summaryRequestInFlight.current) {
      if (mode !== 'background') summaryRefreshQueued.current = true
      return false
    }

    summaryRequestInFlight.current = true
    if (mode === 'initial') {
      setLoadingList(true)
      setError('')
    }
    if (mode === 'manual') {
      setRefreshingSummaries(true)
      setRefreshWarning('')
    }

    try {
      const response = await getConversations(summaryQuery.current.search, summaryQuery.current.filter)
      setConversations(response.items)
      setCounts(response.counts)
      setSelected((current) =>
        current
          ? response.items.find((item) => getConversationKey(item) === getConversationKey(current)) ?? current
          : null,
      )
      return true
    } catch (requestError: unknown) {
      if (mode === 'background') {
        setRefreshWarning('Yeni görüşme özeti alınamadı. Mevcut liste korunuyor.')
      } else {
        setError(
          getFriendlyApiErrorMessage(requestError, {
            fallback: 'Görüşmeler yüklenemedi. Lütfen tekrar deneyin.',
            forbiddenMessage: 'Bu alanı görüntüleme yetkiniz bulunmuyor.',
            unavailableMessage: 'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
          }),
        )
      }
      return false
    } finally {
      summaryRequestInFlight.current = false
      if (mode === 'initial') setLoadingList(false)
      if (mode === 'manual') setRefreshingSummaries(false)
      if (summaryRefreshQueued.current) {
        summaryRefreshQueued.current = false
        void refreshSummaries('initial')
      }
    }
  }

  useEffect(() => {
    summaryQuery.current = { search, filter }
    void refreshSummaries('initial')
  }, [filter, search])

  useEffect(() => {
    const refreshWhenVisible = () => {
      if (document.visibilityState === 'visible') {
        void refreshSummaries('background')
      }
    }

    const intervalId = window.setInterval(refreshWhenVisible, summaryRefreshIntervalMs)
    document.addEventListener('visibilitychange', refreshWhenVisible)
    return () => {
      window.clearInterval(intervalId)
      document.removeEventListener('visibilitychange', refreshWhenVisible)
    }
  }, [])

  async function selectConversation(conversation: ConversationSummary) {
    setSelected(conversation)
    setDetail(null)
    setLoadingDetail(true)
    setError('')

    try {
      const response = await getConversation(conversation.requesterType, conversation.requesterId)
      await markConversationRead(conversation.requesterType, conversation.requesterId)
      setDetail({ ...response, isUnread: false })
      setSelected({ ...conversation, isUnread: false })
      await refreshSummaries('manual')
    } catch (requestError: unknown) {
      setError(
        getFriendlyApiErrorMessage(requestError, {
          fallback: 'Görüşme geçmişi yüklenemedi. Lütfen tekrar deneyin.',
          notFoundMessage: 'Seçilen müşteri görüşmesi bulunamadı.',
          forbiddenMessage: 'Bu görüşmeyi görüntüleme yetkiniz bulunmuyor.',
          unavailableMessage: 'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setLoadingDetail(false)
    }
  }

  async function refreshSelectedConversation() {
    if (!selected) return

    const conversationResponse = await getConversation(selected.requesterType, selected.requesterId)

    setDetail(conversationResponse)
    await refreshSummaries('manual')
  }

  async function toggleConversationPin(conversation: ConversationSummary) {
    const conversationKey = getConversationKey(conversation)
    if (pinningKey) return

    const nextPinned = !conversation.isPinned
    setPinningKey(conversationKey)
    setError('')

    try {
      if (nextPinned) {
        await pinConversation(conversation.requesterType, conversation.requesterId)
      } else {
        await unpinConversation(conversation.requesterType, conversation.requesterId)
      }

      await refreshSummaries('manual')

      if (selected && getConversationKey(selected) === conversationKey) {
        setSelected(
          (current) => current && getConversationKey(current) === conversationKey
            ? { ...current, isPinned: nextPinned }
            : current,
        )
      }

      setDetail((currentDetail) =>
        currentDetail &&
        currentDetail.requesterType === conversation.requesterType &&
        currentDetail.requesterId === conversation.requesterId
          ? { ...currentDetail, isPinned: nextPinned }
          : currentDetail,
      )
    } catch (requestError: unknown) {
      setError(
        getFriendlyApiErrorMessage(requestError, {
          fallback: 'Görüşme sabitlemesi güncellenemedi. Lütfen tekrar deneyin.',
          forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
          notFoundMessage: 'Görüşme bulunamadı.',
          unavailableMessage: 'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setPinningKey(null)
    }
  }

  async function retryConversationMessage(ticketId: number, messageId: number) {
    if (retryingMessageId) return

    setRetryingMessageId(messageId)
    setError('')
    try {
      await retryMessageDelivery(ticketId, messageId)
      await refreshSelectedConversation()
    } catch (requestError: unknown) {
      try {
        await refreshSelectedConversation()
      } catch {
        // The delivery state remains available on the next detail refresh.
      }

      setError(
        getFriendlyApiErrorMessage(requestError, {
          fallback: 'E-posta yeniden gönderilemedi. Lütfen tekrar deneyin.',
          forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
          notFoundMessage: 'Mesaj veya talep bulunamadı.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setRetryingMessageId(null)
    }
  }

  function openTicket(ticketId: number) {
    navigate(`${routeBase}/tickets/${ticketId}`)
  }

  return (
    <Stack spacing={2.5}>
      <PageHeader
        description="Müşteri ve harici müşteri taleplerindeki public iletişimi tek görünümde inceleyin."
        eyebrow="İletişim Merkezi"
        title="Sohbetler"
      />

      {error ? <Alert severity="error">{error}</Alert> : null}
      {refreshWarning ? <Alert severity="warning">{refreshWarning}</Alert> : null}

      <Box
        sx={{
          display: 'grid',
          gap: 2,
          gridTemplateColumns: { md: 'minmax(280px, 360px) minmax(0, 1fr)' },
          minHeight: { md: 'calc(100vh - 270px)' },
        }}
      >
        <Paper
          sx={{
            display: { xs: selected ? 'none' : 'block', md: 'block' },
            minHeight: 420,
            p: 2,
          }}
        >
          {loadingList ? (
            <Box sx={{ display: 'grid', minHeight: 240, placeItems: 'center' }}>
              <CircularProgress aria-label="Görüşmeler yükleniyor" />
            </Box>
          ) : (
            <ConversationList
              conversations={conversations}
              counts={counts}
              filter={filter}
              onFilterChange={setFilter}
              onPinToggle={(conversation) => void toggleConversationPin(conversation)}
              onRefresh={() => void refreshSummaries('manual')}
              onSearchChange={setSearch}
              onSelect={(conversation) => void selectConversation(conversation)}
              pinningKey={pinningKey}
              refreshing={refreshingSummaries}
              search={search}
              selectedKey={selected ? getConversationKey(selected) : null}
            />
          )}
        </Paper>

        <Paper
          sx={{
            display: { xs: selected ? 'block' : 'none', md: 'block' },
            minHeight: 420,
            p: { xs: 2, sm: 2.5 },
          }}
        >
          {!selected ? (
            <EmptyStateCard
              description="Bir müşteri seçerek görüşme geçmişini görüntüleyin."
              icon={<ForumOutlined />}
              title="Görüşme seçilmedi"
            />
          ) : loadingDetail ? (
            <Box sx={{ display: 'grid', minHeight: 300, placeItems: 'center' }}>
              <CircularProgress aria-label="Görüşme geçmişi yükleniyor" />
            </Box>
          ) : detail ? (
            <Stack spacing={2.5}>
              <Button
                onClick={() => {
                  setSelected(null)
                  setDetail(null)
                }}
                startIcon={<ArrowBackOutlined />}
                sx={{ alignSelf: 'flex-start', display: { md: 'none' } }}
              >
                Müşteri listesine dön
              </Button>
              <ConversationHeader
                conversation={detail}
                onViewTickets={() => navigate(`${routeBase}/tickets`)}
              />
              <ConversationTimeline
                onMessagePersisted={refreshSelectedConversation}
                onOpenTicket={openTicket}
                onRetryDelivery={(ticketId, messageId) => void retryConversationMessage(ticketId, messageId)}
                requesterName={detail.displayName}
                retryingMessageId={retryingMessageId}
                tickets={detail.tickets}
              />
            </Stack>
          ) : null}
        </Paper>
      </Box>
    </Stack>
  )
}
