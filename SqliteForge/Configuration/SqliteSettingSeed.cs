namespace SqliteForge.Configuration;

/// <summary>
/// Représente une entrée de configuration initiale à insérer dans la table SQLite <c>AppSettings</c>
/// lorsque celle-ci est vide au premier démarrage.
/// </summary>
/// <param name="Key">Clé hiérarchique du paramètre (ex: <c>App:DefaultCurrency</c>).</param>
/// <param name="Value">Valeur textuelle initiale du paramètre.</param>
/// <param name="Description">Description fonctionnelle du paramètre.</param>
public readonly record struct SqliteSettingSeed(string Key, string? Value, string? Description = null);
