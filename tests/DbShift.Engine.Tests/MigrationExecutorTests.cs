using DbShift.Core.Enums;
using DbShift.Core.Interfaces;
using DbShift.Core.ValueObjects;
using DbShift.Engine.Execution;
using DbShift.Engine.Parsing;
using Xunit;

namespace DbShift.Engine.Tests;

public class MigrationExecutorTests
{
    private readonly InMemoryMigrationTracker _tracker;
    private readonly InMemoryMigrationLockManager _lockManager;
    private readonly InMemoryAuditLogger _auditLogger;
    private readonly InMemoryEnvironmentProvider _environmentProvider;
    private readonly MigrationExecutor _executor;

    public MigrationExecutorTests()
    {
        _tracker = new InMemoryMigrationTracker();
        _lockManager = new InMemoryMigrationLockManager();
        _auditLogger = new InMemoryAuditLogger();
        _environmentProvider = new InMemoryEnvironmentProvider();

        var parser = new ScriptParser();
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<MigrationExecutor>();

        _executor = new MigrationExecutor(
            _tracker,
            _lockManager,
            parser,
            _environmentProvider,
            _auditLogger,
            logger);
    }

    [Fact]
    public async Task ValidateAsync_WithNoMigrations_ReturnsValid()
    {
        // Act
        var result = await _executor.ValidateAsync("development");

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task DryRunAsync_WithNoPendingMigrations_ReturnsEmptyPlan()
    {
        // Arrange
        var context = new MigrationContext
        {
            Environment = "development",
            ExecutedBy = "test"
        };

        // Act
        var result = await _executor.DryRunAsync(context);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ExecutionPlan);
    }

    [Fact]
    public async Task RepairAsync_WithValidVersion_ReturnsSuccess()
    {
        // Arrange
        var record = new Core.Entities.MigrationRecord
        {
            Version = "001",
            Name = "TestMigration",
            Environment = "development",
            Status = MigrationStatus.Failed
        };
        await _tracker.AddAsync(record);

        // Act
        var result = await _executor.RepairAsync("development", "001");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("001", result.RepairedMigrations);
    }

    [Fact]
    public async Task RollbackAsync_WithNoAppliedMigrations_ReturnsSuccess()
    {
        // Arrange
        var request = new Core.ValueObjects.RollbackRequest
        {
            Version = "last",
            Count = 1,
            Environment = "development",
            ExecutedBy = "test"
        };

        // Act
        var result = await _executor.RollbackAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.RolledBackMigrations);
    }
}
