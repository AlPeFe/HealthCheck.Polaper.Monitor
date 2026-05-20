using GamMonitorService.Domain;
using GamMonitorService.Domain.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace GamMonitorService.Infrastructure;

public sealed class SqliteCheckStateStore(IOptions<MonitorOptions> options) : ICheckStateStore
{
    private readonly string _connectionString = options.Value.Persistence.ConnectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS CheckStates (
                    PluginId TEXT NOT NULL,
                    ItemId TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    ConsecutiveFailures INTEGER NOT NULL,
                    LastCheckedAt TEXT NULL,
                    LastSuccessAt TEXT NULL,
                    LastFailureAt TEXT NULL,
                    LastAlertSentAt TEXT NULL,
                    LastRecoverySentAt TEXT NULL,
                    LastMessage TEXT NULL,
                    PRIMARY KEY (PluginId, ItemId)
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyCollection<CheckState>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT PluginId, ItemId, Name, Status, ConsecutiveFailures, LastCheckedAt, LastSuccessAt,
                   LastFailureAt, LastAlertSentAt, LastRecoverySentAt, LastMessage
            FROM CheckStates ORDER BY PluginId, ItemId;
            """;
        var states = new List<CheckState>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) states.Add(ReadState(reader));
        return states;
    }

    public async Task<CheckState?> GetAsync(string pluginId, string itemId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT PluginId, ItemId, Name, Status, ConsecutiveFailures, LastCheckedAt, LastSuccessAt,
                   LastFailureAt, LastAlertSentAt, LastRecoverySentAt, LastMessage
            FROM CheckStates WHERE PluginId = $pluginId AND ItemId = $itemId;
            """;
        command.Parameters.AddWithValue("$pluginId", pluginId);
        command.Parameters.AddWithValue("$itemId", itemId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadState(reader) : null;
    }

    public async Task UpsertAsync(CheckState state, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO CheckStates (PluginId, ItemId, Name, Status, ConsecutiveFailures, LastCheckedAt, LastSuccessAt, LastFailureAt, LastAlertSentAt, LastRecoverySentAt, LastMessage)
                VALUES ($pluginId, $itemId, $name, $status, $consecutiveFailures, $lastCheckedAt, $lastSuccessAt, $lastFailureAt, $lastAlertSentAt, $lastRecoverySentAt, $lastMessage)
                ON CONFLICT(PluginId, ItemId) DO UPDATE SET Name = excluded.Name, Status = excluded.Status, ConsecutiveFailures = excluded.ConsecutiveFailures,
                    LastCheckedAt = excluded.LastCheckedAt, LastSuccessAt = excluded.LastSuccessAt, LastFailureAt = excluded.LastFailureAt,
                    LastAlertSentAt = excluded.LastAlertSentAt, LastRecoverySentAt = excluded.LastRecoverySentAt, LastMessage = excluded.LastMessage;
                """;
            AddStateParameters(command, state);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private static void AddStateParameters(SqliteCommand command, CheckState state)
    {
        command.Parameters.AddWithValue("$pluginId", state.PluginId);
        command.Parameters.AddWithValue("$itemId", state.ItemId);
        command.Parameters.AddWithValue("$name", state.Name);
        command.Parameters.AddWithValue("$status", state.Status.ToString());
        command.Parameters.AddWithValue("$consecutiveFailures", state.ConsecutiveFailures);
        command.Parameters.AddWithValue("$lastCheckedAt", ToDb(state.LastCheckedAt));
        command.Parameters.AddWithValue("$lastSuccessAt", ToDb(state.LastSuccessAt));
        command.Parameters.AddWithValue("$lastFailureAt", ToDb(state.LastFailureAt));
        command.Parameters.AddWithValue("$lastAlertSentAt", ToDb(state.LastAlertSentAt));
        command.Parameters.AddWithValue("$lastRecoverySentAt", ToDb(state.LastRecoverySentAt));
        command.Parameters.AddWithValue("$lastMessage", (object?)state.LastMessage ?? DBNull.Value);
    }

    private static CheckState ReadState(SqliteDataReader reader) => new(reader.GetString(0), reader.GetString(1), reader.GetString(2), Enum.TryParse<CheckStatus>(reader.GetString(3), out var status) ? status : CheckStatus.Unknown, reader.GetInt32(4), FromDb(reader, 5), FromDb(reader, 6), FromDb(reader, 7), FromDb(reader, 8), FromDb(reader, 9), reader.IsDBNull(10) ? null : reader.GetString(10));
    private static object ToDb(DateTimeOffset? value) => value?.ToString("O") ?? (object)DBNull.Value;
    private static DateTimeOffset? FromDb(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : DateTimeOffset.Parse(reader.GetString(ordinal));
}
