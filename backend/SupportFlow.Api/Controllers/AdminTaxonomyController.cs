using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportFlow.Api.DTOs.Common;
using SupportFlow.Api.DTOs.Taxonomy;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Services;

namespace SupportFlow.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminTaxonomyController(TicketTaxonomyService taxonomyService) : ControllerBase
{
    [HttpGet("categories")]
    [ProducesResponseType<PagedResponse<TicketCategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<TicketCategoryResponse>>> GetCategories(
        [FromQuery] string? search,
        [FromQuery] TaxonomyStatusFilter? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidListRequest(status, page, pageSize))
        {
            return BadRequest(new { message = "The supplied taxonomy list query is invalid." });
        }

        return Ok(await taxonomyService.GetCategoriesAsync(search, status, page, pageSize, cancellationToken));
    }

    [HttpPost("categories")]
    [ProducesResponseType<TicketCategoryResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketCategoryResponse>> CreateCategory(
        CreateTicketCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await taxonomyService.CreateCategoryAsync(request, cancellationToken);
        return ToActionResult(result, nameof(GetCategories));
    }

    [HttpPatch("categories/{id:int}")]
    [ProducesResponseType<TicketCategoryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketCategoryResponse>> UpdateCategory(
        int id,
        UpdateTicketCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await taxonomyService.UpdateCategoryAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("categories/{id:int}/status")]
    [ProducesResponseType<TicketCategoryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketCategoryResponse>> UpdateCategoryStatus(
        int id,
        UpdateTaxonomyStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await taxonomyService.UpdateCategoryStatusAsync(id, request.IsActive, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("tags")]
    [ProducesResponseType<PagedResponse<TicketTagResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<TicketTagResponse>>> GetTags(
        [FromQuery] string? search,
        [FromQuery] TaxonomyStatusFilter? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidListRequest(status, page, pageSize))
        {
            return BadRequest(new { message = "The supplied taxonomy list query is invalid." });
        }

        return Ok(await taxonomyService.GetTagsAsync(search, status, page, pageSize, cancellationToken));
    }

    [HttpPost("tags")]
    [ProducesResponseType<TicketTagResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketTagResponse>> CreateTag(
        CreateTicketTagRequest request,
        CancellationToken cancellationToken)
    {
        var result = await taxonomyService.CreateTagAsync(request, cancellationToken);
        return ToActionResult(result, nameof(GetTags));
    }

    [HttpPatch("tags/{id:int}")]
    [ProducesResponseType<TicketTagResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketTagResponse>> UpdateTag(
        int id,
        UpdateTicketTagRequest request,
        CancellationToken cancellationToken)
    {
        var result = await taxonomyService.UpdateTagAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("tags/{id:int}/status")]
    [ProducesResponseType<TicketTagResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketTagResponse>> UpdateTagStatus(
        int id,
        UpdateTaxonomyStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await taxonomyService.UpdateTagStatusAsync(id, request.IsActive, cancellationToken);
        return ToActionResult(result);
    }

    private static bool IsValidListRequest(TaxonomyStatusFilter? status, int page, int pageSize) =>
        (!status.HasValue || Enum.IsDefined(status.Value))
        && page > 0
        && pageSize is > 0 and <= 100;

    private ActionResult<T> ToActionResult<T>(TaxonomyMutationResult<T> result, string? createdAction = null) =>
        result.Failure switch
        {
            TaxonomyMutationFailure.None when createdAction is not null => CreatedAtAction(createdAction, result.Value),
            TaxonomyMutationFailure.None => Ok(result.Value),
            TaxonomyMutationFailure.Invalid => BadRequest(new { message = "The supplied taxonomy value is invalid." }),
            TaxonomyMutationFailure.NotFound => NotFound(),
            TaxonomyMutationFailure.Duplicate => Conflict(new { message = "A taxonomy item with the same name already exists." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
}
