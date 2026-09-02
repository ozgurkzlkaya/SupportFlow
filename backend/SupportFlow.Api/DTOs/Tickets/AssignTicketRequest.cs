using System.ComponentModel.DataAnnotations;

namespace SupportFlow.Api.DTOs.Tickets;

public class AssignTicketRequest
{
    [Range(1, int.MaxValue)]
    public int AssignedUserId { get; set; }
}
