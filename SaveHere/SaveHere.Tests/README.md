# SaveHere Unit Tests

This project contains unit tests for the SaveHere application services.

## Test Coverage

The following services are covered by unit tests:

### Core Services
- **FileManagerService** - File system operations (create, delete, rename, list)
- **DownloadQueueService** - Download queue management and file downloading
- **UserManagementService** - User account management and registration settings
- **MediaConversionService** - Media file conversion using FFmpeg

### Utility Services
- **ShortLinkService** - Short link generation and mapping for file downloads
- **SimpleVersionCheckerService** - Version checking and update notifications

## Running Tests

To run all tests:
```bash
dotnet test
```

To run tests with coverage:
```bash
dotnet test --collect:"XPlat Code Coverage"
```

To run tests for a specific service:
```bash
dotnet test --filter "FileManagerServiceTests"
```

## Test Structure

Tests are organized by service in the `Services/` directory:
- `FileManagerServiceTests.cs`
- `DownloadQueueServiceTests.cs`
- `UserManagementServiceTests.cs`
- `MediaConversionServiceTests.cs`
- `ShortLinkServiceTests.cs`
- `SimpleVersionCheckerServiceTests.cs`

## Testing Dependencies

The tests use the following frameworks and libraries:
- **xUnit** - Testing framework
- **Moq** - Mocking framework for dependencies
- **Microsoft.EntityFrameworkCore.InMemory** - In-memory database for testing
- **Microsoft.AspNetCore.Identity.EntityFrameworkCore** - Identity testing support

## Test Patterns

- **Arrange-Act-Assert** pattern is used consistently
- **Mocking** is used for external dependencies
- **In-memory databases** are used for Entity Framework tests
- **Parameterized tests** using `[Theory]` and `[InlineData]` for testing multiple scenarios
- **Proper cleanup** using `IDisposable` pattern where needed