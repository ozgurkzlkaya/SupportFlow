using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.Tickets;

public class UpdateTicketPriorityRequest
{
    [JsonRequired]
    [EnumDataType(typeof(TicketPriority))]
    public TicketPriority Priority { get; set; }
}
