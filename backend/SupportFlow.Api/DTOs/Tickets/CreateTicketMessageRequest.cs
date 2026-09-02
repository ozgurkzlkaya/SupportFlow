using System.ComponentModel.DataAnnotations;

namespace SupportFlow.Api.DTOs.Tickets;

public class CreateTicketMessageRequest
{
    [Required]
    public string Message { get; set; } = string.Empty;

    public bool IsInternal { get; set; }
}
