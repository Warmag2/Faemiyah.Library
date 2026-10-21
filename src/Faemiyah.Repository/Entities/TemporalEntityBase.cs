using System;
using Microsoft.EntityFrameworkCore;

namespace Faemiyah.Repository.Entities;

/// <summary>
/// Base entity class for all temporal entities.
/// </summary>
/// <remarks>
/// Both dates must be UTC, because they map to <b>timestamp with time zone</b> and writing any other
/// <see cref="DateTimeKind"/> fails. They are instants rather than dates, so use midnight UTC for whole-day periods.
/// </remarks>
/// <typeparam name="TKey">The key type.</typeparam>
[Index(nameof(DateBegin), IsUnique = false)]
[Index(nameof(DateEnd), IsUnique = false)]
public abstract class TemporalEntityBase<TKey> : EntityBase<TKey>
    where TKey : struct, IEquatable<TKey>
{
    /// <summary>
    /// When this entity becomes valid, inclusive.
    /// </summary>
    /// <remarks>
    /// Unset means the period has not been decided yet, which validity queries treat as having no period rather than
    /// an unbounded one, so such an entity is never valid.
    /// </remarks>
    public DateTime? DateBegin { get; set; }

    /// <summary>
    /// When this entity becomes invalid, inclusive. Unset means the period is still open.
    /// </summary>
    public DateTime? DateEnd { get; set; }
}
