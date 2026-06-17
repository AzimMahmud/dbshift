using DbShift.CLI.Commands;
using DbShift.Core.Interfaces;
using DbShift.Core.ValueObjects;
using DbShift.Engine.Execution;
using DbShift.Engine.Parsing;
using DbShift.Infrastructure.Database;
using DbShift.Infrastructure.Database.Providers;
using DbShift.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging;

namespace DbShift.CLI.Helpers;

/// <summary>
/// Composition root for a single CLI invocation. Resolves the database provider from
/// configuration and wires either live (relational) or in-memory implementations.
/// </summary>
public sealed class CliHost
{
    public MigrationExecutor Executor { get; }
    public IConfigLoader ConfigLoader { get; }
    public MigrationConfiguration? Config { get; }
    public string EnvironmentName { get; }
    public bool IsLive { get; }
    public string? ConnectionString { get; }
    public string ProviderName { get; }
    public string ScriptsPath { get; }
    public string BasePath { get; }

    private CliHost(MigrationExecutor executor, IConfigLoader configLoader, MigrationConfiguration? config,
        string environmentName, bool isLive, string? connectionString, string providerName, string scriptsPath, string basePath)
    {
        Executor = executor;
        ConfigLoader = configLoader;
        Config = config;
        EnvironmentName = environmentName;
        IsLive = isLive;
        ConnectionString = connectionString;
        ProviderName = providerName;
        ScriptsPath = scriptsPath;
        BasePath = basePath;
    }

    public static CliHost Create(CommandContext context)
    {
        var basePath = string.IsNullOrWhiteSpace(context.ConfigBasePath)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(context.ConfigBasePath);

        var configLoader = new FileSystemConfigLoader(basePath);

        MigrationConfiguration? config = null;
        try
        {
            config = configLoader.LoadMigrationConfiguration();
        }
        catch (Exception)
        {
            // Some commands (create, info, help) work fine without a config file.
        }

        var environment = string.IsNullOrWhiteSpace(context.EnvironmentName) ? "local" : context.EnvironmentName;

        var connectionString = ResolveConnectionString(context, config);
        var providerName = !string.IsNullOrWhiteSpace(context.Provider) ? context.Provider : (config?.Provider ?? "postgresql");
        var scriptsPath = ResolveScriptsPath(basePath, config);
        var commandTimeout = config?.CommandTimeoutSeconds ?? 3600;

        var preferInMemory = context.UseInMemory || string.IsNullOrWhiteSpace(connectionString);
        var logger = new NoOpLogger<MigrationExecutor>();

        IMigrationTracker tracker;
        IMigrationLockManager lockManager;
        IAuditLogger auditLogger;
        IEnvironmentProvider environmentProvider;
        IMigrationScriptExecutor? scriptExecutor;

        if (preferInMemory)
        {
            tracker = new InMemoryMigrationTracker();
            lockManager = new InMemoryMigrationLockManager();
            auditLogger = new InMemoryAuditLogger();
            environmentProvider = new InMemoryEnvironmentProvider();
            scriptExecutor = null;
        }
        else
        {
            var provider = DatabaseProviderFactory.Create(providerName);
            tracker = new RelationalMigrationTracker(provider, connectionString!);
            lockManager = new RelationalMigrationLockManager(provider, connectionString!);
            auditLogger = new RelationalAuditLogger(provider, connectionString!);
            environmentProvider = new ConfigEnvironmentProvider(configLoader);
            scriptExecutor = new RelationalMigrationExecutor(provider);
            providerName = provider.Name;
        }

        var executor = new MigrationExecutor(
            tracker, lockManager, new ScriptParser(), environmentProvider, auditLogger, logger,
            scriptExecutor, connectionString, commandTimeout, scriptsPath);

        return new CliHost(executor, configLoader, config, environment, !preferInMemory, connectionString, providerName, scriptsPath, basePath);
    }

    private static string? ResolveConnectionString(CommandContext context, MigrationConfiguration? config)
    {
        if (!string.IsNullOrWhiteSpace(context.ConnectionString))
        {
            return context.ConnectionString;
        }

        var fromEnv = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        return string.IsNullOrWhiteSpace(config?.ConnectionString) ? null : config.ConnectionString;
    }

    private static string ResolveScriptsPath(string basePath, MigrationConfiguration? config)
    {
        if (config is null)
        {
            return Path.Combine(basePath, "Database", "Migrations");
        }

        var configured = config.ScriptsPath;
        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(basePath, configured));
    }

    /// <summary>A minimal no-op logger so the CLI has no hard dependency on a logging framework.</summary>
    private sealed class NoOpLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
