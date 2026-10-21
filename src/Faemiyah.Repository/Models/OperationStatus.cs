namespace Faemiyah.Repository.Models;

/// <summary>
/// The outcome of a repository write.
/// </summary>
public enum OperationStatus
{
    /// <summary>
    /// An existing entity was returned instead of adding one.
    /// </summary>
    Found,

    /// <summary>
    /// The entity was inserted.
    /// </summary>
    Added,

    /// <summary>
    /// The entity was updated.
    /// </summary>
    Updated,

    /// <summary>
    /// The entities were deleted. Deleting by a predicate which matches nothing also reports this.
    /// </summary>
    Deleted,

    /// <summary>
    /// The entity to update or delete does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// A unique constraint rejected the write: the entity already exists, or a unique value is taken.
    /// </summary>
    Duplicate,

    /// <summary>
    /// The stored entity was changed or removed since the revision the write was working from.
    /// </summary>
    Conflict,
}
