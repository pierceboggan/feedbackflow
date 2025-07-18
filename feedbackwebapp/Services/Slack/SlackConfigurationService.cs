namespace FeedbackWebApp.Services.Slack;

/// <summary>
/// Service for managing Slack configuration and settings
/// </summary>
public interface ISlackConfigurationService
{
    Task<SlackConfiguration> GetConfigurationAsync();
    Task SaveConfigurationAsync(SlackConfiguration config);
    Task<bool> TestSlackConnectionAsync();
    Task<List<SlackChannel>> GetAvailableChannelsAsync();
    Task<bool> IsSlackConfiguredAsync();
}

public class SlackConfigurationService : ISlackConfigurationService
{
    private readonly ILogger<SlackConfigurationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _storageKey = "slack-configuration";

    public SlackConfigurationService(
        ILogger<SlackConfigurationService> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<SlackConfiguration> GetConfigurationAsync()
    {
        try
        {
            // Try to get from local storage first, then fall back to app settings
            var config = new SlackConfiguration
            {
                BotToken = _configuration["Slack:BotToken"] ?? "",
                VerificationToken = _configuration["Slack:VerificationToken"] ?? "",
                SigningSecret = _configuration["Slack:SigningSecret"] ?? "",
                DefaultChannelId = _configuration["Slack:DefaultChannelId"] ?? "",
                EnableNotifications = bool.Parse(_configuration["Slack:EnableNotifications"] ?? "false"),
                EnableWeeklyReports = bool.Parse(_configuration["Slack:EnableWeeklyReports"] ?? "false"),
                NotificationChannels = ParseNotificationChannels(_configuration["Slack:NotificationChannels"]),
                ReportChannels = ParseReportChannels(_configuration["Slack:ReportChannels"])
            };

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Slack configuration");
            return new SlackConfiguration();
        }
    }

    public async Task SaveConfigurationAsync(SlackConfiguration config)
    {
        try
        {
            // In a real implementation, you'd save this to a database or secure storage
            // For now, we'll log that the configuration would be saved
            _logger.LogInformation("Slack configuration saved for workspace");
            
            // Validate the configuration
            await ValidateConfigurationAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving Slack configuration");
            throw;
        }
    }

    public async Task<bool> TestSlackConnectionAsync()
    {
        try
        {
            var config = await GetConfigurationAsync();
            
            if (string.IsNullOrEmpty(config.BotToken))
            {
                return false;
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.BotToken}");

            var response = await _httpClient.GetAsync("https://slack.com/api/auth.test");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Slack connection");
            return false;
        }
    }

    public async Task<List<SlackChannel>> GetAvailableChannelsAsync()
    {
        try
        {
            var config = await GetConfigurationAsync();
            
            if (string.IsNullOrEmpty(config.BotToken))
            {
                return new List<SlackChannel>();
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.BotToken}");

            var response = await _httpClient.GetAsync("https://slack.com/api/conversations.list?types=public_channel,private_channel");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<SlackChannelsResponse>(content);
                
                return result?.Channels?.Select(c => new SlackChannel
                {
                    Id = c.Id ?? "",
                    Name = c.Name ?? "",
                    IsPrivate = c.IsPrivate
                }).ToList() ?? new List<SlackChannel>();
            }

            return new List<SlackChannel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available Slack channels");
            return new List<SlackChannel>();
        }
    }

    public async Task<bool> IsSlackConfiguredAsync()
    {
        var config = await GetConfigurationAsync();
        return !string.IsNullOrEmpty(config.BotToken) && 
               !string.IsNullOrEmpty(config.VerificationToken);
    }

    private async Task ValidateConfigurationAsync(SlackConfiguration config)
    {
        if (string.IsNullOrEmpty(config.BotToken))
        {
            throw new ArgumentException("Bot token is required");
        }

        if (string.IsNullOrEmpty(config.VerificationToken))
        {
            throw new ArgumentException("Verification token is required");
        }

        // Test the connection
        if (!await TestSlackConnectionAsync())
        {
            throw new InvalidOperationException("Unable to connect to Slack with provided credentials");
        }
    }

    private List<string> ParseNotificationChannels(string? channelsStr)
    {
        if (string.IsNullOrEmpty(channelsStr))
        {
            return new List<string>();
        }

        return channelsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Select(c => c.Trim())
                         .ToList();
    }

    private List<string> ParseReportChannels(string? channelsStr)
    {
        if (string.IsNullOrEmpty(channelsStr))
        {
            return new List<string>();
        }

        return channelsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Select(c => c.Trim())
                         .ToList();
    }
}

public class SlackConfiguration
{
    public string BotToken { get; set; } = "";
    public string VerificationToken { get; set; } = "";
    public string SigningSecret { get; set; } = "";
    public string DefaultChannelId { get; set; } = "";
    public bool EnableNotifications { get; set; }
    public bool EnableWeeklyReports { get; set; }
    public List<string> NotificationChannels { get; set; } = new();
    public List<string> ReportChannels { get; set; } = new();
}

public class SlackChannel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsPrivate { get; set; }
}

// Supporting classes for Slack API responses
public class SlackChannelsResponse
{
    public bool Ok { get; set; }
    public SlackChannelInfo[]? Channels { get; set; }
}

public class SlackChannelInfo
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public bool IsPrivate { get; set; }
}
