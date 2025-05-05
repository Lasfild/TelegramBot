using dotenv.net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

DotEnv.Load();

var telegramBotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
var openRouterApiKey = Environment.GetEnvironmentVariable("AI_API_KEY");

if (string.IsNullOrWhiteSpace(openRouterApiKey))
{
    Console.WriteLine("Ошибка: Не задан AI API-ключ.");
    return;
}

if (string.IsNullOrWhiteSpace(telegramBotToken))
{
    Console.WriteLine("Ошибка: Не задан Telegram API-ключ.");
    return;
}

var botClient = new TelegramBotClient(telegramBotToken);
using var cts = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = Array.Empty<UpdateType>()
};

// Стартуем бота
botClient.StartReceiving(
    HandleUpdateAsync,
    HandleErrorAsync,
    receiverOptions,
    cancellationToken: cts.Token);

var me = await botClient.GetMeAsync();
Console.WriteLine($"Бот @{me.Username} запущен");

while (true)
{
    await Task.Delay(1000);
}

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken token)
{
    if (update.Type == UpdateType.Message && update.Message?.Text is { } text)
    {
        var user = update.Message.From;
        var username = string.IsNullOrEmpty(user.Username) ? $"{user.FirstName} {user.LastName}" : $"@{user.Username}";
        var prompt = text;

        Console.WriteLine($"\nПользователь {username} написал: {prompt}\n");

        var reply = await AskOpenRouterAsync(prompt);

        await botClient.SendTextMessageAsync(update.Message.Chat.Id, reply, cancellationToken: token);

        Console.WriteLine($"Бот ответил пользователю {username}: {reply}\n");
    }
}


async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken token)
{
    Console.WriteLine($"Ошибка: {exception.Message}");
}

async Task<string> AskOpenRouterAsync(string prompt)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openRouterApiKey);

    var body = new
    {
        model = "mistralai/mistral-small-3.1-24b-instruct:free",
        messages = new[] { new { role = "user", content = prompt } }
    };

    var response = await client.PostAsJsonAsync("https://openrouter.ai/api/v1/chat/completions", body);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
    return json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;
}
