# SqliteForge

<p align="center">
  <a href="https://ko-fi.com/curvecoder"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Support curvecoder on Ko-fi" /></a>
</p>

High-performance, resilient SQLite infrastructure library for .NET featuring automatic Write-Ahead Logging (WAL) concurrency interception, dynamic connection string resolution, transparent SQLite Multiple Ciphers / SQLCipher encryption, and SQLite-backed `IConfiguration` providers.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Framework](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-purple.svg)](https://dotnet.microsoft.com/)
[![Documentation: English](https://img.shields.io/badge/Docs-English-blue.svg)](FRAMEWORK.md)
[![Documentation: Français](https://img.shields.io/badge/Docs-Français-green.svg)](FRAMEWORK.fr.md)

> 📖 **Framework Documentation**: [English Specification](FRAMEWORK.md) | [Spécification en français](FRAMEWORK.fr.md)

---

## Key Capabilities

### 1. High-Concurrency Connection Interception (`SqliteForge.Data`)
- **`SqliteWalConnectionInterceptor`** (`DbConnectionInterceptor`):
  - Automatically activates optimized PRAGMA directives upon every Entity Framework Core connection opening:
    - `PRAGMA journal_mode = WAL;` (Write-Ahead Logging)
    - `PRAGMA synchronous = NORMAL;`
    - `PRAGMA busy_timeout = 5000;`
    - `PRAGMA foreign_keys = ON;`
  - Gracefully handles read-only connections (`SQLITE_READONLY = 8`).
- **`IDatabaseConnectionProvider` & `DatabaseConnectionProvider`**:
  - Dynamically injects master passphrases into connection strings via `IDatabaseKeyHolder`.
  - Determines absolute database file paths and handles `:memory:` databases seamlessly.

### 2. Dynamic SQLite Configuration Provider (`SqliteForge.Configuration`)
- **`SqliteConfigurationProvider` & `SqliteConfigurationSource`**:
  - Automatically provisions an `AppSettings` table (`Key`, `Value`, `Description`, `UpdatedAtUtc`) on first launch.
  - Automatically seeds default application parameters transactionally.
  - Plugs directly into ASP.NET Core / Microsoft.Extensions.Configuration (`IConfiguration`).

### 3. Encryption & Keystore Lifecycle (`SqliteForge.Security`)
- **`IDatabaseKeyHolder`**: In-memory, thread-safe holder for runtime decryption passphrases.
- **`LocalProtectedFileKeyStore`**: Device-bound persistent secret storage using Windows DPAPI (`ProtectedData` with `CurrentUser` scope) and AES-256 fallback on Linux.
- **`SqliteDatabaseEncryptionService`**:
  - Transparent passphrase verification, database locking/unlocking, and rekeying (`PRAGMA rekey`).
  - Encrypt existing plaintext databases to encrypted format or decrypt in-place.

---

## Installation

Add the package via the .NET CLI:

```bash
dotnet add package SqliteForge --version 1.0.0
```

---

## Quick Example

```csharp
using Microsoft.EntityFrameworkCore;
using SqliteForge.Data;

// 1. Configure EF Core with WAL interceptor
var optionsBuilder = new DbContextOptionsBuilder<MyDbContext>()
    .UseSqlite("Data Source=app.db")
    .AddInterceptors(new SqliteWalConnectionInterceptor());

// 2. Initialize connection provider with key holder
var keyHolder = new DatabaseKeyHolder();
var connectionProvider = new DatabaseConnectionProvider("Data Source=app.db", keyHolder);
```

---

## License

This project is licensed under the [MIT License](LICENSE) &copy; 2026 curvecoder.
