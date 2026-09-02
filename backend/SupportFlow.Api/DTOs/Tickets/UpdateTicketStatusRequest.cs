using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.Tickets;

public class UpdateTicketStatusRequest
{
    [JsonRequired]
    [EnumDataType(typeof(TicketStatus))]
    public TicketStatus Status { get; set; }

    public string? Note { get; set; }
}
