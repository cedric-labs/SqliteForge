namespace SqliteForge.Configuration;

/// <summary>
/// An initial configuration entry inserted into the SQLite <c>AppSettings</c> table
/// when the table is empty on first start.
/// </summary>
/// <param name="Key">Hierarchical setting key (for example <c>App:DefaultCurrency</c>).</param>
/// <param name="Value">Initial text value of the setting.</param>
/// <param name="Description">Functional description of the setting.</param>
public readonly record struct SqliteSettingSeed(string Key, string? Value, string? Description = null);
