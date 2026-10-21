using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Faemiyah.Repository.Entities;

/// <summary>
/// Base entity class for all entities.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
public abstract class EntityBase<TKey>
    where TKey : struct, IEquatable<TKey>
{
    /// <summary>
    /// The internal database key.
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    [Required]
    public TKey Id { get; set; }

    /// <summary>
    /// The revision of this entity, used for optimistic concurrency checks.
    /// </summary>
    /// <remarks>
    /// Maps to the PostgreSQL <b>xmin</b> system column, so the database maintains it. The property must stay a
    /// <see cref="uint"/> for that mapping to apply; on any other type the attribute silently produces an ordinary
    /// column that nothing populates.
    /// </remarks>
    [Timestamp]
    public uint ConcurrencyToken { get; set; }

    /// <summary>
    /// The time when this entity was last written, in UTC. Assigned by the repository on add and update.
    /// </summary>
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
