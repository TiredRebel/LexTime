namespace LexTime.Domain.Entities;

/// <summary>
/// An organisation the firm bills. Work for a client is organised into matters.
/// </summary>
public sealed class Client
{
    /// <summary>Surrogate key assigned by the database.</summary>
    public int ClientId { get; set; }

    /// <summary>
    /// Short code the firm uses to refer to this client in conversation and on reports,
    /// for example <c>ACME</c>. Unique across all clients.
    /// </summary>
    public required string ClientCode { get; set; }

    /// <summary>Full legal or trading name.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Whether new time may be recorded against this client's matters. Forward-looking
    /// only: a client who leaves keeps their billing history, and that history still
    /// appears in reports covering the period they were active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>When the row was created, in UTC.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Matters belonging to this client.</summary>
    public ICollection<Matter> Matters { get; } = [];
}
