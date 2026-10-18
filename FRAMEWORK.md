# SqliteForge — Spécification & Documentation IA du Framework

Ce document est le manifeste technique destiné à l'agent IA et aux développeurs. Il récapitule l'ensemble des capacités, composants et contrats publics exposés par **SqliteForge**.

---

## 🎯 Périmètre & Responsabilité

SqliteForge est la bibliothèque partagée .NET d'infrastructure **SQLite, de configuration dynamique en base et de chiffrement SQLCipher / SQLite3 Multiple Ciphers**. Elle est 100% agnostique du domaine métier applicatif.

---

## 🧱 Composants & Capacités Détaillées

### 1. `SqliteForge.Data` — Connexion & Optimisation WAL
- **`SqliteWalConnectionInterceptor`** (`DbConnectionInterceptor`) :
  - Active automatiquement `PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON;` à chaque ouverture de connexion EF Core (synchrone et asynchrone).
  - Tolère gracieusement les connexions ouvertes en lecture seule (`SQLITE_READONLY = 8`).
- **`IDatabaseConnectionProvider` / `DatabaseConnectionProvider`** :
  - `GetConnectionString()` : Résout dynamiquement la chaîne de connexion SQLite en y injectant la clé de chiffrement active issue de `IDatabaseKeyHolder`.
  - `GetDatabaseFilePath()` : Résout le chemin physique absolu du fichier SQLite ou retourne `:memory:`.
  - `IsDatabaseEncrypted()` / `IsDatabaseLocked()` : Expose l'état de chiffrement et de verrouillage de la base.

### 2. `SqliteForge.Configuration` — Fournisseur `IConfiguration` sur SQLite
- **`SqliteSettingSeed`** (`readonly record struct`) :
  - Value Object immuable définissant un paramètre initial (`Key`, `Value`, `Description`) à insérer dans la table `AppSettings` si celle-ci est vide.
- **`SqliteConfigurationProvider` / `SqliteConfigurationSource`** :
  - Crée automatiquement la table `AppSettings (Key, Value, Description, UpdatedAtUtc)` si absente.
  - Insère transactionnellement les `SqliteSettingSeed` par défaut au premier démarrage.
  - Charge toutes les paires clé/valeur dans `IConfiguration` et notifie les erreurs de chargement via le délégué optionnel `Action<Exception>? onLoadError`.
- **`SqliteConfigurationExtensions`** :
  - `AddSqliteConfiguration(this IConfigurationBuilder builder, string connectionString, ...)`
  - `AddSqliteConfiguration(this IConfigurationBuilder builder, Func<string?> connectionStringResolver, ...)`

### 3. `SqliteForge.Security` — Chiffrement SQLite3MC & Trousseau d'Appareil
- **`IDatabaseKeyHolder` / `DatabaseKeyHolder`** :
  - Détenteur singleton thread-safe (`Lock` sur .NET 9+, `object` sur .NET 8) en mémoire de la clé de déchiffrement (`EncryptionKey`, `IsDatabaseEncrypted`, `IsUnlocked`, `SetKey`, `Clear`).
- **`IDatabaseKeyStore` / `LocalProtectedFileKeyStore`** :
  - Persistance locale sécurisée de la clé ("Se souvenir sur cet appareil").
  - Utilise `ProtectedData` (DPAPI `DataProtectionScope.CurrentUser`) sous Windows et AES-256 avec vecteur d'initialisation préfixé sous Linux/macOS.
- **`IDatabaseEncryptionService` / `SqliteDatabaseEncryptionService`** :
  - `CheckIfFileEncrypted(string filePath)` : Détection statique par inspection des 16 premiers octets (`SQLite format 3\0`).
  - `VerifyPassphrase(string baseConnectionString, string passphrase)` : Vérification de la clé via `SELECT COUNT(*) FROM sqlite_master;`.
  - `ValidatePassphrase(string passphrase)` : Validation des règles de sécurité de la phrase de passe.
  - Cycle de vie complet sans perte de données : `UnlockDatabaseAsync`, `EncryptDatabaseAsync`, `ChangePassphraseAsync` (`PRAGMA rekey`), `DecryptDatabaseToPlainAsync`, `IsRememberOnDeviceEnabledAsync`, `SetRememberOnDeviceAsync`.
  - Supporte un callback asynchrone optionnel `onUnlockedCallback` et le rechargement automatique de `IConfigurationRoot` après déverrouillage.

---

## 📋 Protocole de Maintenance par l'Agent IA

Toute modification de SqliteForge doit respecter :
1. **Rétrocompatibilité :** Conserver tous les contrats publics existants. Utiliser des surcharges ou des paramètres optionnels.
2. **Qualité :** Zéro warning (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`), zéro pragma/suppress, `CancellationToken` systématique sur les opérations asynchrones.
3. **Tests :** Nommage `MethodeCible_ScenarioOuCondition_ResultatAttendu`.
4. **Mise à jour :** Compléter immédiatement ce fichier `FRAMEWORK.md` en cas d'ajout d'une nouvelle capacité.
5. **Validation :** Valider par `./verify.ps1`.
