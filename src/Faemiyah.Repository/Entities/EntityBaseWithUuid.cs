using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Faemiyah.Repository.Entities;

/// <summary>
/// Base entity class with an unique identifier.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
[Index(nameof(UniqueId), IsUnique = true)]
public abstract class EntityBaseWithUuid<TKey> : EntityBase<TKey>
    where TKey : struct, IEquatable<TKey>
{
    /// <summary>
    /// The public identifier of this database entity.
    /// </summary>
    [Required]
    public Guid UniqueId { get; set; } = Guid.NewGuid();
}
