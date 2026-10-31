<p align="right">
  <b>English</b> | <a href="FRAMEWORK.fr.md">Français</a>
</p>

# SqliteForge — Framework Specification & AI Documentation

This document is the technical manifest intended for AI agents and software engineers. It outlines all capabilities, architectural components, and public contracts exposed by **SqliteForge**.

---

## 🎯 Scope & Responsibility

SqliteForge is a shared .NET library providing **SQLite database infrastructure, dynamic database-backed configuration, and transparent encryption via SQLCipher / SQLite3 Multiple Ciphers**. It is 100% domain- and application-agnostic.

---

## 🧱 Components & Detailed Capabilities

### 1. `SqliteForge.Data` — Connection & WAL Optimization
- **`SqliteWalConnectionInterceptor`** (`DbConnectionInterceptor`):
  - Automatically activates `PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON;` upon every EF Core connection opening (synchronous and asynchronous).
  - Gracefully ignores read-only opened connections (`SQLITE_READONLY = 8`).
- **`IDatabaseConnectionProvider` / `DatabaseConnectionProvider`**:
  - `GetConnectionString()`: Dynamically constructs the SQLite connection string, injecting the active encryption key from `IDatabaseKeyHolder`.
  - `GetDatabaseFilePath()`: Resolves the physical absolute file path or returns `:memory:`.
  - `IsDatabaseEncrypted()` / `IsDatabaseLocked()`: Exposes encryption state and unlock status.

### 2. `SqliteForge.Configuration` — SQLite-backed `IConfiguration` Provider
- **`SqliteSettingSeed`** (`readonly record struct`):
  - Immutable Value Object defining initial configuration values (`Key`, `Value`, `Description`) seeded into `AppSettings` when empty.
- **`SqliteConfigurationProvider` / `SqliteConfigurationSource`**:
  - Automatically provisions the `AppSettings (Key, Value, Description, UpdatedAtUtc)` table if missing.
  - Transactionally seeds default `SqliteSettingSeed` instances on first start.
  - Loads all key-value pairs into `IConfiguration` and reports load anomalies via the optional `Action<Exception>? onLoadError` callback.
- **`SqliteConfigurationExtensions`**:
  - `AddSqliteConfiguration(this IConfigurationBuilder builder, string connectionString, ...)`
  - `AddSqliteConfiguration(this IConfigurationBuilder builder, Func<string?> connectionStringResolver, ...)`

### 3. `SqliteForge.Security` — SQLite3MC Encryption & Device Key Ring
- **`IDatabaseKeyHolder` / `DatabaseKeyHolder`**:
  - In-memory thread-safe singleton key holder (`Lock` on .NET 9+, `object` on .NET 8) managing passphrase lifecycle (`EncryptionKey`, `IsDatabaseEncrypted`, `IsUnlocked`, `SetKey`, `Clear`).
- **`IDatabaseKeyStore` / `LocalProtectedFileKeyStore`**:
  - Secure local key persistence ("Remember on this device").
  - Uses Windows DPAPI (`ProtectedData` with `DataProtectionScope.CurrentUser`) on Windows and AES-256 with prefixed IV on Linux/macOS.
  - Default location: `LocalApplicationData/<application>/dbkey.dat`; when `LocalApplicationData` is empty (service account without a home directory), `dbkey.dat` sits in the application folder. A key that cannot be written does not fail `UnlockDatabaseAsync` or the re-key: a warning is logged and the passphrase is requested again at the next start.
- **`IDatabaseEncryptionService` / `SqliteDatabaseEncryptionService`**:
  - `CheckIfFileEncrypted(string filePath)`: Static inspection of the first 16 bytes (`SQLite format 3\0`).
  - `VerifyPassphrase(string baseConnectionString, string passphrase)`: Validates encryption credentials via `SELECT COUNT(*) FROM sqlite_master;`.
  - `ValidatePassphrase(string passphrase)`: Enforces passphrase security policies.
  - Complete zero-data-loss encryption lifecycle: `UnlockDatabaseAsync`, `EncryptDatabaseAsync`, `ChangePassphraseAsync` (`PRAGMA rekey`), `DecryptDatabaseToPlainAsync`, `IsRememberOnDeviceEnabledAsync`, `SetRememberOnDeviceAsync`.
  - Supports optional asynchronous unlock callbacks `onUnlockedCallback` and automatic reloading of `IConfigurationRoot` post-unlock.

---

## 📋 Maintenance Protocol for AI Agents

All enhancements to SqliteForge must comply with:
1. **Backward Compatibility:** Preserve all existing public contracts. Use overloads or optional parameters.
2. **Code Cleanliness:** Zero warnings (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`), zero pragmas/suppressions, systematic `CancellationToken` on all async methods.
3. **Testing Standards:** Test naming convention `TargetMethod_ScenarioOrCondition_ExpectedResult`.
4. **Documentation:** Update both `FRAMEWORK.md` and `FRAMEWORK.fr.md` immediately upon introducing or modifying capabilities.
5. **Validation:** Full pass of `./verify.ps1`.
