using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;

namespace KotonohaAssistant.AI.Repositories;

public interface IAssistantDataRepository
{
    Task<List<AssistantData>> GetAssistantDataAsync(string name);
    Task InsertAssistantDataAsync(string id, string name);
    Task<List<ThreadData>> GetThreadDataAsync();
    Task InsertThreadDataAsync(string id);
}

public class AssistantDataRepository(string dbPath) : IAssistantDataRepository
{
    private bool _isInitialized = false;
    private IDbConnection Connection => new SqliteConnection($"Data Source={dbPath}");
    private IDbConnection ReadOnlyConnection => new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");

    private async Task InitializeDatabaseAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        using var connection = Connection;

        connection.Open();

        var createTableQuery = @"
CREATE TABLE IF NOT EXISTS AssistantData (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS ThreadData (
    Id TEXT PRIMARY KEY,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
";
        await connection.ExecuteAsync(createTableQuery);

        _isInitialized = true;
    }

    public async Task<List<AssistantData>> GetAssistantDataAsync(string name)
    {
        await InitializeDatabaseAsync();

        var sql = @"
SELECT *
FROM AssistantData
WHERE Name = @Name
";
        try
        {
            using var connection = ReadOnlyConnection;
            connection.Open();
            var assistants = await connection.QueryAsync<AssistantData>(sql, new
            {
                Name = name
            });

            return [.. assistants];
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return [];
        }
    }

    public async Task InsertAssistantDataAsync(string id, string name)
    {
        await InitializeDatabaseAsync();

        var sql = @"
INSERT INTO AssistantData
    (Id, Name)
VALUES
    (@Id, @Name)
";

        try
        {
            using var connection = Connection;
            connection.Open();

            _ = await connection.ExecuteAsync(sql, new AssistantData
            {
                Id = id,
                Name = name,
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public async Task<List<ThreadData>> GetThreadDataAsync()
    {
        await InitializeDatabaseAsync();

        var sql = @"
SELECT *
FROM ThreadData
";
        try
        {
            using var connection = ReadOnlyConnection;
            connection.Open();
            var assistants = await connection.QueryAsync<ThreadData>(sql);

            return [.. assistants];
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return [];
        }
    }

    public async Task InsertThreadDataAsync(string id)
    {
        await InitializeDatabaseAsync();

        var sql = @"
INSERT INTO ThreadData
    (Id)
VALUES
    (@Id)
";

        try
        {
            using var connection = Connection;
            connection.Open();

            _ = await connection.ExecuteAsync(sql, new AssistantData
            {
                Id = id,
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}

public class AssistantData
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class ThreadData
{
    public string? Id { get; set; }
    public DateTime? CreatedAt { get; set; }
}