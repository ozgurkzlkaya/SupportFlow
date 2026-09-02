using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Common;
using SupportFlow.Api.DTOs.Taxonomy;
using SupportFlow.Api.DTOs.Tickets;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;

namespace SupportFlow.Api.Services;

public sealed class TicketTaxonomyService(AppDbContext dbContext, TimeProvider timeProvider)
{
    private const int CategoryNameMaxLength = 150;
    private const int TagNameMaxLength = 100;
    private const int DescriptionMaxLength = 500;

    public async Task<PagedResponse<TicketCategoryResponse>> GetCategoriesAsync(
        string? search,
        TaxonomyStatusFilter? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.TicketCategories.AsNoTracking();
        query = ApplyCategoryFilters(query, search, status);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(category => category.IsActive)
            .ThenBy(category => category.Name)
            .ThenBy(category => category.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(category => new TicketCategoryResponse
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                IsActive = category.IsActive,
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt,
                UsageCount = dbContext.Tickets.Count(ticket => ticket.CategoryId == category.Id)
            })
            .ToListAsync(cancellationToken);

        return CreatePagedResponse(items, page, pageSize, totalCount);
    }

    public async Task<PagedResponse<TicketTagResponse>> GetTagsAsync(
        string? search,
        TaxonomyStatusFilter? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.TicketTags.AsNoTracking();
        query = ApplyTagFilters(query, search, status);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(tag => tag.IsActive)
            .ThenBy(tag => tag.Name)
            .ThenBy(tag => tag.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(tag => new TicketTagResponse
            {
                Id = tag.Id,
                Name = tag.Name,
                IsActive = tag.IsActive,
                CreatedAt = tag.CreatedAt,
                UpdatedAt = tag.UpdatedAt,
                UsageCount = dbContext.TicketTagAssignments.Count(assignment => assignment.TagId == tag.Id)
            })
            .ToListAsync(cancellationToken);

        return CreatePagedResponse(items, page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<TicketClassificationItemResponse>> GetClassificationCategoriesAsync(
        bool activeOnly,
        CancellationToken cancellationToken = default) =>
        await dbContext.TicketCategories
            .AsNoTracking()
            .Where(category => !activeOnly || category.IsActive)
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .Select(category => new TicketClassificationItemResponse
            {
                Id = category.Id,
                Name = category.Name,
                IsActive = category.IsActive
            })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TicketClassificationItemResponse>> GetClassificationTagsAsync(
        bool activeOnly,
        CancellationToken cancellationToken = default) =>
        await dbContext.TicketTags
            .AsNoTracking()
            .Where(tag => !activeOnly || tag.IsActive)
            .OrderBy(tag => tag.Name)
            .ThenBy(tag => tag.Id)
            .Select(tag => new TicketClassificationItemResponse
            {
                Id = tag.Id,
                Name = tag.Name,
                IsActive = tag.IsActive
            })
            .ToListAsync(cancellationToken);

    public async Task<TaxonomyMutationResult<TicketCategoryResponse>> CreateCategoryAsync(
        CreateTicketCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepareCategory(request.Name, request.Description, out var name, out var normalizedName, out var description))
        {
            return TaxonomyMutationResult<TicketCategoryResponse>.Invalid();
        }

        if (await dbContext.TicketCategories.AnyAsync(category => category.NormalizedName == normalizedName, cancellationToken))
        {
            return TaxonomyMutationResult<TicketCategoryResponse>.Duplicate();
        }

        var category = new TicketCategory
        {
            Name = name,
            NormalizedName = normalizedName,
            Description = description,
            IsActive = true,
            CreatedAt = GetUtcNow()
        };
        dbContext.TicketCategories.Add(category);

        if (!await TrySaveAsync(cancellationToken))
        {
            return TaxonomyMutationResult<TicketCategoryResponse>.Duplicate();
        }

        return TaxonomyMutationResult<TicketCategoryResponse>.Succeeded(ToCategoryResponse(category, 0));
    }

    public async Task<TaxonomyMutationResult<TicketCategoryResponse>> UpdateCategoryAsync(
        int categoryId,
        UpdateTicketCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepareCategory(request.Name, request.Description, out var name, out var normalizedName, out var description))
        {
            return TaxonomyMutationResult<TicketCategoryResponse>.Invalid();
        }

        var category = await dbContext.TicketCategories
            .SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return TaxonomyMutationResult<TicketCategoryResponse>.NotFound();
        }

        if (await dbContext.TicketCategories.AnyAsync(
                item => item.Id != categoryId && item.NormalizedName == normalizedName,
                cancellationToken))
        {
            return TaxonomyMutationResult<TicketCategoryResponse>.Duplicate();
        }

        category.Name = name;
        category.NormalizedName = normalizedName;
        category.Description = description;
        category.UpdatedAt = GetUtcNow();

        if (!await TrySaveAsync(cancellationToken))
        {
            return TaxonomyMutationResult<TicketCategoryResponse>.Duplicate();
        }

        var usageCount = await dbContext.Tickets.CountAsync(ticket => ticket.CategoryId == category.Id, cancellationToken);
        return TaxonomyMutationResult<TicketCategoryResponse>.Succeeded(ToCategoryResponse(category, usageCount));
    }

    public async Task<TaxonomyMutationResult<TicketCategoryResponse>> UpdateCategoryStatusAsync(
        int categoryId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var category = await dbContext.TicketCategories
            .SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return TaxonomyMutationResult<TicketCategoryResponse>.NotFound();
        }

        if (category.IsActive != isActive)
        {
            category.IsActive = isActive;
            category.UpdatedAt = GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var usageCount = await dbContext.Tickets.CountAsync(ticket => ticket.CategoryId == category.Id, cancellationToken);
        return TaxonomyMutationResult<TicketCategoryResponse>.Succeeded(ToCategoryResponse(category, usageCount));
    }

    public async Task<TaxonomyMutationResult<TicketTagResponse>> CreateTagAsync(
        CreateTicketTagRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepareName(request.Name, TagNameMaxLength, out var name, out var normalizedName))
        {
            return TaxonomyMutationResult<TicketTagResponse>.Invalid();
        }

        if (await dbContext.TicketTags.AnyAsync(tag => tag.NormalizedName == normalizedName, cancellationToken))
        {
            return TaxonomyMutationResult<TicketTagResponse>.Duplicate();
        }

        var tag = new TicketTag
        {
            Name = name,
            NormalizedName = normalizedName,
            IsActive = true,
            CreatedAt = GetUtcNow()
        };
        dbContext.TicketTags.Add(tag);

        if (!await TrySaveAsync(cancellationToken))
        {
            return TaxonomyMutationResult<TicketTagResponse>.Duplicate();
        }

        return TaxonomyMutationResult<TicketTagResponse>.Succeeded(ToTagResponse(tag, 0));
    }

    public async Task<TaxonomyMutationResult<TicketTagResponse>> UpdateTagAsync(
        int tagId,
        UpdateTicketTagRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepareName(request.Name, TagNameMaxLength, out var name, out var normalizedName))
        {
            return TaxonomyMutationResult<TicketTagResponse>.Invalid();
        }

        var tag = await dbContext.TicketTags.SingleOrDefaultAsync(item => item.Id == tagId, cancellationToken);
        if (tag is null)
        {
            return TaxonomyMutationResult<TicketTagResponse>.NotFound();
        }

        if (await dbContext.TicketTags.AnyAsync(
                item => item.Id != tagId && item.NormalizedName == normalizedName,
                cancellationToken))
        {
            return TaxonomyMutationResult<TicketTagResponse>.Duplicate();
        }

        tag.Name = name;
        tag.NormalizedName = normalizedName;
        tag.UpdatedAt = GetUtcNow();

        if (!await TrySaveAsync(cancellationToken))
        {
            return TaxonomyMutationResult<TicketTagResponse>.Duplicate();
        }

        var usageCount = await dbContext.TicketTagAssignments.CountAsync(assignment => assignment.TagId == tag.Id, cancellationToken);
        return TaxonomyMutationResult<TicketTagResponse>.Succeeded(ToTagResponse(tag, usageCount));
    }

    public async Task<TaxonomyMutationResult<TicketTagResponse>> UpdateTagStatusAsync(
        int tagId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var tag = await dbContext.TicketTags.SingleOrDefaultAsync(item => item.Id == tagId, cancellationToken);
        if (tag is null)
        {
            return TaxonomyMutationResult<TicketTagResponse>.NotFound();
        }

        if (tag.IsActive != isActive)
        {
            tag.IsActive = isActive;
            tag.UpdatedAt = GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var usageCount = await dbContext.TicketTagAssignments.CountAsync(assignment => assignment.TagId == tag.Id, cancellationToken);
        return TaxonomyMutationResult<TicketTagResponse>.Succeeded(ToTagResponse(tag, usageCount));
    }

    private static IQueryable<TicketCategory> ApplyCategoryFilters(
        IQueryable<TicketCategory> query,
        string? search,
        TaxonomyStatusFilter? status)
    {
        if (status.HasValue)
        {
            query = query.Where(category => category.IsActive == (status == TaxonomyStatusFilter.Active));
        }

        var normalizedSearch = search?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalizedSearch)
            ? query
            : query.Where(category => category.Name.ToUpper().Contains(normalizedSearch));
    }

    private static IQueryable<TicketTag> ApplyTagFilters(
        IQueryable<TicketTag> query,
        string? search,
        TaxonomyStatusFilter? status)
    {
        if (status.HasValue)
        {
            query = query.Where(tag => tag.IsActive == (status == TaxonomyStatusFilter.Active));
        }

        var normalizedSearch = search?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalizedSearch)
            ? query
            : query.Where(tag => tag.Name.ToUpper().Contains(normalizedSearch));
    }

    private static bool TryPrepareCategory(
        string inputName,
        string? inputDescription,
        out string name,
        out string normalizedName,
        out string? description)
    {
        description = string.IsNullOrWhiteSpace(inputDescription) ? null : inputDescription.Trim();
        if (description is { Length: > DescriptionMaxLength }
            || !TryPrepareName(inputName, CategoryNameMaxLength, out name, out normalizedName))
        {
            name = string.Empty;
            normalizedName = string.Empty;
            return false;
        }

        return true;
    }

    private static bool TryPrepareName(string input, int maxLength, out string name, out string normalizedName)
    {
        name = input?.Trim() ?? string.Empty;
        normalizedName = string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > maxLength)
        {
            return false;
        }

        normalizedName = TaxonomyNameNormalizer.Normalize(name);
        return normalizedName.Length <= maxLength;
    }

    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    private DateTime GetUtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static TicketCategoryResponse ToCategoryResponse(TicketCategory category, int usageCount) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Description = category.Description,
        IsActive = category.IsActive,
        CreatedAt = category.CreatedAt,
        UpdatedAt = category.UpdatedAt,
        UsageCount = usageCount
    };

    private static TicketTagResponse ToTagResponse(TicketTag tag, int usageCount) => new()
    {
        Id = tag.Id,
        Name = tag.Name,
        IsActive = tag.IsActive,
        CreatedAt = tag.CreatedAt,
        UpdatedAt = tag.UpdatedAt,
        UsageCount = usageCount
    };

    private static PagedResponse<T> CreatePagedResponse<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount) => new()
        {
            Items = items,
            Pagination = new PaginationMetadata
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
                HasPreviousPage = page > 1,
                HasNextPage = (long)page * pageSize < totalCount
            }
        };
}

public enum TaxonomyMutationFailure
{
    None,
    Invalid,
    NotFound,
    Duplicate
}

public sealed record TaxonomyMutationResult<T>(T? Value, TaxonomyMutationFailure Failure)
{
    public static TaxonomyMutationResult<T> Succeeded(T value) => new(value, TaxonomyMutationFailure.None);
    public static TaxonomyMutationResult<T> Invalid() => new(default, TaxonomyMutationFailure.Invalid);
    public static TaxonomyMutationResult<T> NotFound() => new(default, TaxonomyMutationFailure.NotFound);
    public static TaxonomyMutationResult<T> Duplicate() => new(default, TaxonomyMutationFailure.Duplicate);
}
