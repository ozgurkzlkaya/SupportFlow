using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportFlow.Api.DTOs.Tickets;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Services;

namespace SupportFlow.Api.Controllers;

[ApiController]
[Route("api/taxonomy")]
[Authorize(Roles = nameof(UserRole.TechnicalStaff) + "," + nameof(UserRole.Admin))]
public sealed class TaxonomyController(TicketTaxonomyService taxonomyService) : ControllerBase
{
    [HttpGet("categories")]
    [ProducesResponseType<IReadOnlyList<TicketClassificationItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketClassificationItemResponse>>> GetCategories(
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default) =>
        Ok(await taxonomyService.GetClassificationCategoriesAsync(activeOnly, cancellationToken));

    [HttpGet("tags")]
    [ProducesResponseType<IReadOnlyList<TicketClassificationItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketClassificationItemResponse>>> GetTags(
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default) =>
        Ok(await taxonomyService.GetClassificationTagsAsync(activeOnly, cancellationToken));
}
