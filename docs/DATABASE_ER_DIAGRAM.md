# Veritabanı ER Diyagramı

Bu diyagramlar `AppDbContext`, model sınıfları ve mevcut EF Core yapılandırmasından üretilmiştir. `ConversationPin` ve `ConversationReadState` talep sahibine polimorfik (`RequesterType`, `RequesterId`) bağlanır; bu nedenle doğrudan FK çizgisi yoktur.

## Çekirdek Ticket Modeli

```mermaid
erDiagram
    Company ||--o{ User : contains
    User o|--o{ Ticket : customer
    User o|--o{ Ticket : assignee
    ExternalRequester o|--o{ Ticket : requester
    TicketCategory o|--o{ Ticket : classifies
    Ticket ||--o{ TicketMessage : has
    User o|--o{ TicketMessage : sends
    ExternalRequester o|--o{ TicketMessage : sends
    Ticket ||--o{ TicketStatusHistory : records
    User ||--o{ TicketStatusHistory : changes
    Ticket ||--o{ Attachment : contains
    TicketMessage o|--o{ Attachment : owns
    User o|--o{ Attachment : uploads
    ExternalRequester o|--o{ Attachment : uploads
    Ticket ||--o{ TicketTagAssignment : tagged
    TicketTag ||--o{ TicketTagAssignment : assigned

    Company { int Id PK string Name bool IsActive }
    User { int Id PK int CompanyId FK string Email UK string Role bool IsActive }
    ExternalRequester { int Id PK string Email string NormalizedEmail UK string DisplayName }
    Ticket { int Id PK string TicketNumber UK int CustomerId FK int ExternalRequesterId FK int AssignedUserId FK int CategoryId FK string Status string Priority datetime CreatedAt }
    TicketMessage { int Id PK int TicketId FK int SenderId FK int ExternalRequesterId FK bool IsInternal string OutboundDeliveryStatus datetime CreatedAt }
    TicketStatusHistory { int Id PK int TicketId FK int ChangedByUserId FK string OldStatus string NewStatus datetime ChangedAt }
    Attachment { int Id PK int TicketId FK int MessageId FK int UploadedByUserId FK int UploadedByExternalRequesterId FK string ContentType long FileSize }
    TicketCategory { int Id PK string Name string NormalizedName UK bool IsActive }
    TicketTag { int Id PK string Name string NormalizedName UK bool IsActive }
    TicketTagAssignment { int TicketId PK int TagId PK }
```

## Gmail, Sohbet ve Audit Destek Modeli

```mermaid
erDiagram
    Ticket ||--o{ TicketEmailThread : aliases
    TicketEmailThread ||--o{ ProcessedEmailMessage : processes
    TicketMessage o|--o{ ProcessedEmailMessage : represents
    User ||--o{ Notification : receives
    Ticket o|--o{ Notification : links
    User ||--o{ ConversationPin : owns
    User ||--o{ ConversationReadState : owns
    User ||--o{ UserAdminAudit : acts
    User ||--o{ UserAdminAudit : target

    TicketEmailThread { int Id PK int TicketId FK string GmailThreadId UK datetime CreatedAt }
    ProcessedEmailMessage { int Id PK int TicketEmailThreadId FK string GmailMessageId UK string InternetMessageId int TicketMessageId FK datetime ProcessedAt }
    Notification { int Id PK int UserId FK int TicketId FK string Type bool IsRead datetime CreatedAt }
    ConversationPin { int Id PK int UserId FK string RequesterType int RequesterId datetime CreatedAt }
    ConversationReadState { int Id PK int UserId FK string RequesterType int RequesterId datetime LastReadAt }
    UserAdminAudit { int Id PK int ActorUserId FK int TargetUserId FK string Action bool OldIsActive bool NewIsActive datetime OccurredAt }
```

## Etkinlik Modeli

```mermaid
erDiagram
    Ticket ||--o{ TicketActivity : has
    User o|--o{ TicketActivity : actor
    ExternalRequester o|--o{ TicketActivity : actor
    TicketStatusHistory o|--o{ TicketActivity : related_status
    TicketMessage o|--o{ TicketActivity : related_message
    Attachment o|--o{ TicketActivity : related_attachment

    TicketActivity { int Id PK int TicketId FK string ActivityType string Source string Visibility datetime OccurredAt datetime RecordedAt string ActorKind int ActorUserId FK int ExternalRequesterId FK int RelatedStatusHistoryId FK int RelatedMessageId FK int RelatedAttachmentId FK int MetadataVersion string MetadataJson }
```

## Önemli Kısıtlar

- `Users.Email`, `Tickets.TicketNumber`, `ExternalRequesters.NormalizedEmail`, `TicketCategories.NormalizedName`, `TicketTags.NormalizedName`, `TicketEmailThreads.GmailThreadId` ve `ProcessedEmailMessages.GmailMessageId` benzersizdir.
- `TicketTagAssignments` bileşik anahtarı `TicketId + TagId` kullanır.
- Pin/read-state için `UserId + RequesterType + RequesterId` benzersizdir.
- `TicketActivities` aktör türü ile FK alanlarının tutarlılığını denetleyen check constraint'lere sahiptir.
- İlişkilerde çoğunlukla `DeleteBehavior.NoAction` kullanılır; geçmiş verisi iş akışı dışı silinmez.
