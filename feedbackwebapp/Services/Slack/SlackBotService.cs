using System.Text;
using System.Text.Json;

namespace FeedbackWebApp.Services.Slack;

/// <summary>
/// Service for integrating with Slack to send notifications and updates
/// </summary>
public interface ISlackBotService
{
    Task SendFeedbackAnalysisAsync(string channelId, string platform, string sourceUrl, string analysis);
    Task SendReportSummaryAsync(string channelId, string reportTitle, string summary, string reportUrl);
    Task SendErrorNotificationAsync(string channelId, string errorMessage, string? context = null);
    Task SendWeeklyReportAsync(string channelId, WeeklyReportSummary summary);
}

public class SlackBotService : ISlackBotService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SlackBotService> _logger;

    public SlackBotService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SlackBotService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendFeedbackAnalysisAsync(string channelId, string platform, string sourceUrl, string analysis)
    {
        try
        {
            var message = CreateFeedbackAnalysisMessage(channelId, platform, sourceUrl, analysis);
            await SendSlackMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send feedback analysis to Slack channel {ChannelId}", channelId);
        }
    }

    public async Task SendReportSummaryAsync(string channelId, string reportTitle, string summary, string reportUrl)
    {
        try
        {
            var message = CreateReportSummaryMessage(channelId, reportTitle, summary, reportUrl);
            await SendSlackMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send report summary to Slack channel {ChannelId}", channelId);
        }
    }

    public async Task SendErrorNotificationAsync(string channelId, string errorMessage, string? context = null)
    {
        try
        {
            var message = CreateErrorNotificationMessage(channelId, errorMessage, context);
            await SendSlackMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send error notification to Slack channel {ChannelId}", channelId);
        }
    }

    public async Task SendWeeklyReportAsync(string channelId, WeeklyReportSummary summary)
    {
        try
        {
            var message = CreateWeeklyReportMessage(channelId, summary);
            await SendSlackMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send weekly report to Slack channel {ChannelId}", channelId);
        }
    }

    private object CreateFeedbackAnalysisMessage(string channelId, string platform, string sourceUrl, string analysis)
    {
        // Truncate analysis for Slack message limits
        var truncatedAnalysis = TruncateText(analysis, 2500);
        
        return new
        {
            channel = channelId,
            blocks = new object[]
            {
                new
                {
                    type = "header",
                    text = new
                    {
                        type = "plain_text",
                        text = $"📊 {platform} Feedback Analysis Complete"
                    }
                },
                new
                {
                    type = "section",
                    fields = new object[]
                    {
                        new
                        {
                            type = "mrkdwn",
                            text = $"*Platform:*\n{platform}"
                        },
                        new
                        {
                            type = "mrkdwn",
                            text = $"*Source:*\n<{sourceUrl}|View Original>"
                        }
                    }
                },
                new
                {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = $"*Analysis Results:*\n{truncatedAnalysis}"
                    }
                },
                new
                {
                    type = "actions",
                    elements = new object[]
                    {
                        new
                        {
                            type = "button",
                            text = new
                            {
                                type = "plain_text",
                                text = "📤 Share Analysis",
                                emoji = true
                            },
                            action_id = "share_analysis",
                            style = "primary"
                        },
                        new
                        {
                            type = "button",
                            text = new
                            {
                                type = "plain_text",
                                text = "📊 Full Report",
                                emoji = true
                            },
                            action_id = "view_full_report",
                            url = sourceUrl
                        }
                    }
                }
            }
        };
    }

    private object CreateReportSummaryMessage(string channelId, string reportTitle, string summary, string reportUrl)
    {
        return new
        {
            channel = channelId,
            blocks = new object[]
            {
                new
                {
                    type = "header",
                    text = new
                    {
                        type = "plain_text",
                        text = "📋 Report Generated"
                    }
                },
                new
                {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = $"*{reportTitle}*\n\n{TruncateText(summary, 1500)}"
                    },
                    accessory = new
                    {
                        type = "button",
                        text = new
                        {
                            type = "plain_text",
                            text = "View Report",
                            emoji = true
                        },
                        url = reportUrl,
                        action_id = "view_report"
                    }
                }
            }
        };
    }

    private object CreateErrorNotificationMessage(string channelId, string errorMessage, string? context)
    {
        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new
                {
                    type = "plain_text",
                    text = "⚠️ FeedbackFlow Error"
                }
            },
            new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = $"*Error:* {errorMessage}"
                }
            }
        };

        if (!string.IsNullOrEmpty(context))
        {
            blocks.Add(new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = $"*Context:* {context}"
                }
            });
        }

        return new
        {
            channel = channelId,
            blocks = blocks
        };
    }

    private object CreateWeeklyReportMessage(string channelId, WeeklyReportSummary summary)
    {
        return new
        {
            channel = channelId,
            blocks = new object[]
            {
                new
                {
                    type = "header",
                    text = new
                    {
                        type = "plain_text",
                        text = "📊 Weekly FeedbackFlow Report"
                    }
                },
                new
                {
                    type = "section",
                    fields = new object[]
                    {
                        new
                        {
                            type = "mrkdwn",
                            text = $"*Reports Generated:*\n{summary.TotalReports}"
                        },
                        new
                        {
                            type = "mrkdwn",
                            text = $"*Comments Analyzed:*\n{summary.TotalComments:N0}"
                        },
                        new
                        {
                            type = "mrkdwn",
                            text = $"*Top Platform:*\n{summary.TopPlatform}"
                        },
                        new
                        {
                            type = "mrkdwn",
                            text = $"*Success Rate:*\n{summary.SuccessRate:P0}"
                        }
                    }
                },
                new
                {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = $"*Key Insights:*\n{TruncateText(summary.KeyInsights, 1000)}"
                    }
                },
                new
                {
                    type = "actions",
                    elements = new object[]
                    {
                        new
                        {
                            type = "button",
                            text = new
                            {
                                type = "plain_text",
                                text = "📊 View Detailed Report",
                                emoji = true
                            },
                            action_id = "view_weekly_report",
                            style = "primary"
                        }
                    }
                }
            }
        };
    }

    private async Task SendSlackMessageAsync(object message)
    {
        var botToken = _configuration["Slack:BotToken"];
        
        if (string.IsNullOrEmpty(botToken))
        {
            _logger.LogWarning("Slack bot token not configured. Message not sent.");
            return;
        }

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {botToken}");

        var response = await _httpClient.PostAsync("https://slack.com/api/chat.postMessage", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to send Slack message. Status: {StatusCode}, Content: {Content}", 
                response.StatusCode, errorContent);
        }
    }

    private string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        
        return text.Substring(0, maxLength - 3) + "...";
    }
}

public class WeeklyReportSummary
{
    public int TotalReports { get; set; }
    public int TotalComments { get; set; }
    public string TopPlatform { get; set; } = "";
    public double SuccessRate { get; set; }
    public string KeyInsights { get; set; } = "";
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
}
