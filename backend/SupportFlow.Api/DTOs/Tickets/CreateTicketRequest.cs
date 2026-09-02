using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.Tickets;

public class CreateTicketRequest
{
    [Required]
    [StringLength(250)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [JsonRequired]
    [EnumDataType(typeof(TicketPriority))]
    public TicketPriority Priority { get; set; }
}
