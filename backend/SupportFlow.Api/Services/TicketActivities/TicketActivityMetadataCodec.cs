using System.Text.Json;
using SupportFlow.Api.Enums;

namespace SupportFlow.Api.Services.TicketActivities;

public interface ITicketActivityMetadataCodec
{
    TicketActivitySerializedMetadata Serialize(TicketActivityType activityType, TicketActivityMetadata? metadata);
    TicketActivityMetadata? Deserialize(TicketActivityType activityType, int metadataVersion, string metadataJson);
}

public sealed record TicketActivitySerializedMetadata(int Version, string Json);

public sealed class TicketActivityMetadataCodec : ITicketActivityMetadataCodec
{
    public const int CurrentMetadataVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public TicketActivitySerializedMetadata Serialize(
        TicketActivityType activityType,
        TicketActivityMetadata? metadata)
    {
        ValidateMetadata(activityType, metadata);

        var json = metadata is null
            ? "{}"
            : JsonSerializer.Serialize(metadata, metadata.GetType(), SerializerOptions);

        return new TicketActivitySerializedMetadata(CurrentMetadataVersion, json);
    }

    public TicketActivityMetadata? Deserialize(
        TicketActivityType activityType,
        int metadataVersion,
        string metadataJson)
    {
        if (metadataVersion != CurrentMetadataVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(metadataVersion), "The metadata version is unsupported.");
        }

        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            throw new ArgumentException("Activity metadata JSON is required.", nameof(metadataJson));
        }

        try
        {
            return activityType switch
            {
                TicketActivityType.StatusChanged => DeserializeRequired<StatusChangedTicketActivityMetadata>(activityType, metadataJson),
                TicketActivityType.PriorityChanged => DeserializeRequired<PriorityChangedTicketActivityMetadata>(activityType, metadataJson),
                TicketActivityType.AssignmentChanged => DeserializeRequired<AssignmentChangedTicketActivityMetadata>(activityType, metadataJson),
                TicketActivityType.ClassificationChanged => DeserializeRequired<ClassificationChangedTicketActivityMetadata>(activityType, metadataJson),
                TicketActivityType.TicketCreated or TicketActivityType.MessageCreated or TicketActivityType.InternalNoteCreated or TicketActivityType.AttachmentAdded
                    => DeserializeEmpty(activityType, metadataJson),
                _ => throw new ArgumentOutOfRangeException(nameof(activityType), "The activity type is unsupported.")
            };
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Activity metadata JSON is invalid.", nameof(metadataJson), exception);
        }
    }

    private static T DeserializeRequired<T>(TicketActivityType activityType, string metadataJson)
        where T : TicketActivityMetadata
    {
        var metadata = JsonSerializer.Deserialize<T>(metadataJson, SerializerOptions)
            ?? throw new ArgumentException("Activity metadata is required.", nameof(metadataJson));
        ValidateMetadata(activityType, metadata);
        return metadata;
    }

    private static TicketActivityMetadata? DeserializeEmpty(TicketActivityType activityType, string metadataJson)
    {
        using var document = JsonDocument.Parse(metadataJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || document.RootElement.EnumerateObject().Any())
        {
            throw new ArgumentException("This activity type does not support metadata.", nameof(metadataJson));
        }

        ValidateMetadata(activityType, null);
        return null;
    }

    private static void ValidateMetadata(TicketActivityType activityType, TicketActivityMetadata? metadata)
    {
        switch (activityType)
        {
            case TicketActivityType.StatusChanged when metadata is StatusChangedTicketActivityMetadata status:
                if (!Enum.IsDefined(status.OldStatus) || !Enum.IsDefined(status.NewStatus) || status.OldStatus == status.NewStatus)
                {
                    throw new ArgumentException("Status metadata must describe a real transition.", nameof(metadata));
                }

                return;
            case TicketActivityType.PriorityChanged when metadata is PriorityChangedTicketActivityMetadata priority:
                if (!Enum.IsDefined(priority.OldPriority) || !Enum.IsDefined(priority.NewPriority) || priority.OldPriority == priority.NewPriority)
                {
                    throw new ArgumentException("Priority metadata must describe a real transition.", nameof(metadata));
                }

                return;
            case TicketActivityType.AssignmentChanged when metadata is AssignmentChangedTicketActivityMetadata assignment:
                ValidateAssignment(assignment);
                return;
            case TicketActivityType.ClassificationChanged when metadata is ClassificationChangedTicketActivityMetadata classification:
                ValidateClassification(classification);
                return;
            case TicketActivityType.TicketCreated or TicketActivityType.MessageCreated or TicketActivityType.InternalNoteCreated or TicketActivityType.AttachmentAdded when metadata is null:
                return;
            default:
                throw new ArgumentException("The activity type and metadata contract do not match.", nameof(metadata));
        }
    }

    private static void ValidateAssignment(AssignmentChangedTicketActivityMetadata metadata)
    {
        ValidateUserSnapshot(metadata.OldUserId, metadata.OldDisplayName, nameof(metadata));
        ValidateUserSnapshot(metadata.NewUserId, metadata.NewDisplayName, nameof(metadata));

        if (metadata.OldUserId == metadata.NewUserId)
        {
            throw new ArgumentException("Assignment metadata must describe a real change.", nameof(metadata));
        }
    }

    private static void ValidateClassification(ClassificationChangedTicketActivityMetadata metadata)
    {
        ValidateCategorySnapshot(metadata.OldCategoryId, metadata.OldCategoryName, nameof(metadata));
        ValidateCategorySnapshot(metadata.NewCategoryId, metadata.NewCategoryName, nameof(metadata));

        var added = ValidateTagSnapshots(metadata.AddedTags, nameof(metadata));
        var removed = ValidateTagSnapshots(metadata.RemovedTags, nameof(metadata));
        if (metadata.OldCategoryId == metadata.NewCategoryId && added.Count == 0 && removed.Count == 0)
        {
            throw new ArgumentException("Classification metadata must describe a real change.", nameof(metadata));
        }

        if (added.Overlaps(removed))
        {
            throw new ArgumentException("A tag cannot be both added and removed.", nameof(metadata));
        }
    }

    private static void ValidateUserSnapshot(int? userId, string? displayName, string parameterName)
    {
        if (userId.HasValue != !string.IsNullOrWhiteSpace(displayName)
            || userId is <= 0
            || displayName?.Length > 201)
        {
            throw new ArgumentException("Assignment snapshots must pair a valid user id with a display name.", parameterName);
        }
    }

    private static void ValidateCategorySnapshot(int? categoryId, string? categoryName, string parameterName)
    {
        if (categoryId.HasValue != !string.IsNullOrWhiteSpace(categoryName)
            || categoryId is <= 0
            || categoryName?.Length > 150)
        {
            throw new ArgumentException("Category snapshots must pair a valid category id with a name.", parameterName);
        }
    }

    private static HashSet<int> ValidateTagSnapshots(
        IReadOnlyList<TicketActivityTagSnapshot>? tags,
        string parameterName)
    {
        if (tags is null)
        {
            throw new ArgumentException("Tag snapshots are required.", parameterName);
        }

        var ids = new HashSet<int>();
        foreach (var tag in tags)
        {
            if (tag.Id <= 0 || string.IsNullOrWhiteSpace(tag.Name) || tag.Name.Length > 100 || !ids.Add(tag.Id))
            {
                throw new ArgumentException("Tag snapshots must contain unique valid ids and names.", parameterName);
            }
        }

        return ids;
    }
}
