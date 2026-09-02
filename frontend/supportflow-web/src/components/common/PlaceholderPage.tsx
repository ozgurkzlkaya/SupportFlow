import { Container, Paper, Typography } from '@mui/material'

interface PlaceholderPageProps {
  title: string
  description: string
}

export function PlaceholderPage({ title, description }: PlaceholderPageProps) {
  return (
    <Container maxWidth="sm" sx={{ py: 8 }}>
      <Paper elevation={1} sx={{ p: 4 }}>
        <Typography component="h1" gutterBottom variant="h4">
          {title}
        </Typography>
        <Typography color="text.secondary">{description}</Typography>
      </Paper>
    </Container>
  )
}
