namespace SupportFlow.Api.Enums;

public enum QueueAgingBucket
{
    New,
    ShortWait,
    Waiting,
    LongWait,
    Overdue,
    CriticalWait
}
