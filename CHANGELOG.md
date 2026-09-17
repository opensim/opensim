# Changelog

All notable changes made in this branch (`jules-12566837975670410540-57bce191`) are documented in this file.

## [Unreleased] - 2026-02-26

### ⚡ Performance Improvements
- **MySQL Estate Data (`MySQLEstateData.cs`)**:
  - **SaveBanList Optimization**: Wrapped loop-based estate ban inserts inside a single `MySqlTransaction` and reused prepared parameter instances (`?EstateID`, `?bannedUUID`, `?banningUUID`, `?banTime`) across iterations instead of calling `cmd.Parameters.Clear()` and re-adding parameters on every single record.
  - **SaveUUIDList Optimization**: Wrapped loop-based UUID list inserts (`estate_managers`, `estate_users`, `estate_groups`) in `SaveUUIDList` inside a single `MySqlTransaction` and reused parameter definitions (`?EstateID`, `?uuid`) across iterations.
  - **Impact**:
    - Reduces transaction / write round-trip overhead on MySQL by committing operations in a single atomic transaction block.
    - Prevents N+1 parameter allocations and command parsing overhead for large estate ban and UUID lists.

### 🧪 Tests & Benchmarks
- Added `MySQLEstateDataBenchmarkTests` in `OpenSim/Data/Tests/MySQLEstateDataBenchmarkTests.cs` to test `EstateSettings` ban list structures and verify `MySQLEstateData` list save logic.
