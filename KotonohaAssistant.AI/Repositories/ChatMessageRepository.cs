using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using OpenAI.Chat;
using static KotonohaAssistant.AI.Prompts.InitialConversation;

namespace KotonohaAssistant.AI.Repositories;

public interface IChatMessageRepository
{
    Task<long> GetLatestConversationIdAsync();
    Task<int> CreateNewConversationIdAsync();
    Task<IEnumerable<ChatMessage>> GetAllChatMessagesAsync(long conversationId);
    Task<IEnumerable<Message>> GetAllMessageAsync(long conversationId);
    Task InsertChatMessagesAsync(IEnumerable<ChatMessage> chatMessages, long conversationId);
}

public class ChatMessageRepository(string dbPath) : IChatMessageRepository
{
    private bool _isInitialized = false;
    private readonly string _connectionString = $"Data Source={dbPath}";

    private IDbConnection Connection => new SqliteConnection(_connectionString);

    private async Task InitializeDatabaseAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        using var connection = Connection;

        connection.Open();

        var createTableQuery = @"
CREATE TABLE IF NOT EXISTS Message (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ConversationId INTEGER NOT NULL,
    Type TEXT NOT NULL,
    Content TEXT NULL,
    ToolCalls TEXT NULL,
    ToolId TEXT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ConversationId) REFERENCES Conversation(Id)
);

CREATE TABLE IF NOT EXISTS Conversation (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
";
        await connection.ExecuteAsync(createTableQuery);

        _isInitialized = true;
    }

    public async Task<long> GetLatestConversationIdAsync()
    {
        await InitializeDatabaseAsync();

        var sql = "SELECT * FROM Conversation ORDER BY CreatedAt DESC LIMIT 1;";
        try
        {
            using var connection = Connection;
            connection.Open();

            var conversation = await connection.QueryFirstAsync<Conversation>(sql);

            return conversation.Id;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    public async Task<int> CreateNewConversationIdAsync()
    {
        await InitializeDatabaseAsync();

        var sql = "INSERT INTO Conversation DEFAULT VALUES; SELECT last_insert_rowid();";
        try
        {
            using var connection = Connection;
            connection.Open();

            var id = await connection.ExecuteScalarAsync<int>(sql);

            return id;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    public async Task<IEnumerable<ChatMessage>> GetAllChatMessagesAsync(long conversationId)
    {
        await InitializeDatabaseAsync();

        var sql = """
            SELECT *
            FROM Message
            WHERE ConversationId = @Id
            ORDER BY Id
            """;
        try
        {
            using var connection = Connection;
            connection.Open();
            var messages = await connection.QueryAsync<Message>(sql, new { Id = conversationId });
            if (messages is null)
            {
                return [];
            }

            return messages
                .Select(m => m.ToChatMessage())
                .OfType<ChatMessage>()
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public async Task<IEnumerable<Message>> GetAllMessageAsync(long conversationId)
    {
        await InitializeDatabaseAsync();

        var sql = """
            SELECT *
            FROM Message
            WHERE ConversationId = @Id
            ORDER BY Id
            """;
        try
        {
            using var connection = Connection;
            connection.Open();
            var messages = await connection.QueryAsync<Message>(sql, new { Id = conversationId });
            if (messages is null)
            {
                return [];
            }

            return [.. messages];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public async Task InsertChatMessagesAsync(IEnumerable<ChatMessage> chatMessages, long conversationId)
    {
        await InitializeDatabaseAsync();

        if (!chatMessages.Any())
        {
            return; // 空のリストなら何もしない
        }

        var messages = new List<Message>();
        foreach (var chatMessage in chatMessages)
        {
            var type = chatMessage switch
            {
                UserChatMessage => "User",
                AssistantChatMessage => "Assistant",
                ToolChatMessage => "Tool",
                SystemChatMessage => "System",
                _ => throw new InvalidOperationException($"Unknown message type: {chatMessage.GetType()}")
            };

            var content = chatMessage.Content.Select(c => c.Text).ToList();
            var toolCalls = chatMessage is AssistantChatMessage assistant
                ? assistant.ToolCalls.Select(t => new Function
                {
                    Id = t.Id,
                    Name = t.FunctionName,
                    Arguments = ConvertBinaryDataToString(t.FunctionArguments)
                })
                : [];

            if (toolCalls.Any())
            {
                var a = JsonSerializer.Serialize(toolCalls);
            }

            var message = new Message
            {
                ConversationId = conversationId,
                Type = type,
                Content = JsonSerializer.Serialize(content),
                ToolCalls = JsonSerializer.Serialize(toolCalls),
                ToolId = chatMessage is ToolChatMessage tool ? tool.ToolCallId : null
            };

            messages.Add(message);
        }

        var sql = "INSERT INTO Message (ConversationId, Type, Content, ToolCalls, ToolId) VALUES (@ConversationId, @Type, @Content, @ToolCalls, @ToolId)";
        try
        {
            using var connection = Connection;
            connection.Open();

            await connection.ExecuteAsync(sql, messages);
        }
        catch (Exception)
        {
        }

    }

    private static string ConvertBinaryDataToString(BinaryData data)
    {
        return Convert.ToBase64String(data.ToArray());
    }
}

public class Message
{
    public long? Id { get; set; }
    public long? ConversationId { get; set; }
    public string? Type { get; set; }
    public string? Content { get; set; }
    public string? ToolCalls { get; set; }
    public string? ToolId { get; set; }
    public DateTime? CreatedAt { get; set; }

    public ChatMessage ToChatMessage()
    {
        var texts = JsonSerializer.Deserialize<List<string>>(Content ?? "[]");
        var content = (texts ?? []).Select(ChatMessageContentPart.CreateTextPart);
        var functions = JsonSerializer.Deserialize<List<Function>>(ToolCalls ?? "[]");
        var tollCalls = (functions ?? []).Select(tc => ChatToolCall.CreateFunctionToolCall(tc.Id, tc.Name, ConvertStringToBinaryData(tc.Arguments)));

        return Type switch
        {
            "User" => new UserChatMessage(content),
            "Assistant" when tollCalls.Any() => new AssistantChatMessage(tollCalls),
            "Assistant" => new AssistantChatMessage(content),
            "Tool" => new ToolChatMessage(this.ToolId, content),
            "System" => new SystemChatMessage(content),
            _ => throw new ArgumentOutOfRangeException(nameof(Type))
        };
    }

    private static BinaryData ConvertStringToBinaryData(string? base64String)
    {
        byte[] byteArray = Convert.FromBase64String(base64String ?? string.Empty);
        return new BinaryData(byteArray);
    }
}

public class Conversation
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Function
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Arguments { get; set; }
}
