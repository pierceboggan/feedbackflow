using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models;
using SharedDump.Services.Interfaces;

namespace FeedbackFunctions.Slack;

/// <summary>
/// Azure Functions for Slack integration with FeedbackFlow
/// </summary>
/// <remarks>
/// This class provides Slack slash commands and webhook endpoints for
/// interacting with FeedbackFlow's feedback analysis capabilities.
/// </remarks>
public class SlackIntegrationFunctions
{
    private readonly ILogger<SlackIntegrationFunctions> _logger;
    private readonly IGitHubService _githubService;
    private readonly IYouTubeService _youtubeService;
    private readonly IRedditService _redditService;
    private readonly IHackerNewsService _hnService;
    private readonly IDevBlogsService _devBlogsService;
    private readonly IFeedbackAnalyzerService _analyzerService;
    private readonly IConfiguration _configuration;

    public SlackIntegrationFunctions(
        ILogger<SlackIntegrationFunctions> logger,
        IGitHubService githubService,
        IYouTubeService youtubeService,
        IRedditService redditService,
        IHackerNewsService hnService,
        IDevBlogsService devBlogsService,
        IFeedbackAnalyzerService analyzerService,
        IConfiguration configuration)
    {
        _logger = logger;
        _githubService = githubService;
        _youtubeService = youtubeService;
        _redditService = redditService;
        _hnService = hnService;
        _devBlogsService = devBlogsService;
        _analyzerService = analyzerService;
        _configuration = configuration;
    }

    /// <summary>
    /// Handles Slack slash command for feedback analysis
    /// </summary>
    [Function("SlackFeedbackCommand")]
    public async Task<HttpResponseData> SlackFeedbackCommand(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Processing Slack feedback command");

        try
        {
            // Parse Slack form data
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var formData = ParseSlackFormData(requestBody);

            if (!ValidateSlackRequest(formData))
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Invalid Slack token");
                return unauthorizedResponse;
            }

            var command = formData.GetValueOrDefault("command", "");
            var text = formData.GetValueOrDefault("text", "");
            var userId = formData.GetValueOrDefault("user_id", "");
            var channelId = formData.GetValueOrDefault("channel_id", "");
            var responseUrl = formData.GetValueOrDefault("response_url", "");

            // Acknowledge the command immediately
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            switch (command)
            {
                case "/feedbackflow":
                    await response.WriteStringAsync(JsonSerializer.Serialize(new
                    {
                        response_type = "ephemeral",
                        text = "🔄 Analyzing feedback...",
                        blocks = new[]
                        {
                            new
                            {
                                type = "section",
                                text = new
                                {
                                    type = "mrkdwn",
                                    text = $"🔄 Working on analyzing: `{text}`"
                                }
                            }
                        }
                    }));

                    // Process feedback asynchronously
                    _ = Task.Run(async () => await ProcessFeedbackAsync(text, responseUrl, userId));
                    break;

                case "/ff-github":
                    await HandleGitHubCommand(response, text, responseUrl);
                    break;

                case "/ff-youtube":
                    await HandleYouTubeCommand(response, text, responseUrl);
                    break;

                case "/ff-reddit":
                    await HandleRedditCommand(response, text, responseUrl);
                    break;

                default:
                    await response.WriteStringAsync(JsonSerializer.Serialize(new
                    {
                        response_type = "ephemeral",
                        text = "❌ Unknown command. Use `/feedbackflow <url>` to analyze feedback."
                    }));
                    break;
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Slack command");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                response_type = "ephemeral",
                text = "❌ Sorry, something went wrong while processing your request."
            }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Handles Slack interactive components (buttons, menus, etc.)
    /// </summary>
    [Function("SlackInteractivity")]
    public async Task<HttpResponseData> SlackInteractivity(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Processing Slack interactivity request");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var formData = ParseSlackFormData(requestBody);
            
            var payloadJson = formData.GetValueOrDefault("payload", "");
            var payload = JsonSerializer.Deserialize<SlackInteractionPayload>(payloadJson);

            if (payload?.Actions?.FirstOrDefault() is var action && action != null)
            {
                switch (action.ActionId)
                {
                    case "share_analysis":
                        await HandleShareAnalysis(payload);
                        break;
                    case "regenerate_analysis":
                        await HandleRegenerateAnalysis(payload);
                        break;
                    case "export_analysis":
                        await HandleExportAnalysis(payload);
                        break;
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Slack interactivity");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            return errorResponse;
        }
    }

    /// <summary>
    /// Webhook endpoint for receiving analysis reports
    /// </summary>
    [Function("SlackReportWebhook")]
    public async Task<HttpResponseData> SlackReportWebhook(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Processing Slack report webhook");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var reportData = JsonSerializer.Deserialize<AnalysisReport>(requestBody);

            if (reportData != null)
            {
                await SendReportToSlack(reportData);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing report webhook");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            return errorResponse;
        }
    }

    private async Task ProcessFeedbackAsync(string text, string responseUrl, string userId)
    {
        try
        {
            // Determine the platform based on URL
            var (platform, analysisResult) = await AnalyzeFeedbackByUrl(text);

            // Create rich Slack message with analysis
            var slackMessage = CreateAnalysisMessage(analysisResult, platform, text);

            // Send delayed response to Slack
            await SendDelayedResponse(responseUrl, slackMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in async feedback processing");
            await SendDelayedResponse(responseUrl, new
            {
                response_type = "ephemeral",
                text = "❌ Error analyzing feedback. Please try again or contact support."
            });
        }
    }

    private async Task<(string platform, string analysisResult)> AnalyzeFeedbackByUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return ("unknown", "Please provide a valid URL to analyze.");
        }

        url = url.Trim();

        try
        {
            // GitHub
            if (url.Contains("github.com"))
            {
                var result = await _githubService.GetGitHubFeedbackAsync(url);
                var analysis = await _analyzerService.AnalyzeCommentsAsync(
                    JsonSerializer.Serialize(result), "github");
                return ("GitHub", analysis);
            }

            // YouTube
            if (url.Contains("youtube.com") || url.Contains("youtu.be"))
            {
                var videoId = ExtractYouTubeVideoId(url);
                if (!string.IsNullOrEmpty(videoId))
                {
                    var result = await _youtubeService.GetYouTubeVideoAsync(videoId);
                    var analysis = await _analyzerService.AnalyzeCommentsAsync(
                        JsonSerializer.Serialize(result), "youtube");
                    return ("YouTube", analysis);
                }
            }

            // Reddit
            if (url.Contains("reddit.com"))
            {
                var result = await _redditService.GetRedditThreadAsync(url);
                var analysis = await _analyzerService.AnalyzeCommentsAsync(
                    JsonSerializer.Serialize(result), "reddit");
                return ("Reddit", analysis);
            }

            // Hacker News
            if (url.Contains("news.ycombinator.com"))
            {
                var storyId = ExtractHackerNewsStoryId(url);
                if (!string.IsNullOrEmpty(storyId))
                {
                    var result = await _hnService.GetHackerNewsStoryAsync(int.Parse(storyId));
                    var analysis = await _analyzerService.AnalyzeCommentsAsync(
                        JsonSerializer.Serialize(result), "hackernews");
                    return ("Hacker News", analysis);
                }
            }

            // DevBlogs
            if (url.Contains("devblogs.microsoft.com"))
            {
                var result = await _devBlogsService.FetchArticleWithCommentsAsync(url);
                var analysis = await _analyzerService.AnalyzeCommentsAsync(
                    JsonSerializer.Serialize(result), "devblogs");
                return ("DevBlogs", analysis);
            }

            return ("unknown", "Unsupported URL format. Please provide a GitHub, YouTube, Reddit, Hacker News, or DevBlogs URL.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing feedback for URL: {Url}", url);
            return ("error", $"Error analyzing feedback: {ex.Message}");
        }
    }

    private object CreateAnalysisMessage(string analysisResult, string platform, string originalUrl)
    {
        // Truncate analysis if too long for Slack
        var truncatedAnalysis = TruncateForSlack(analysisResult);
        
        return new
        {
            response_type = "in_channel",
            blocks = new object[]
            {
                new
                {
                    type = "header",
                    text = new
                    {
                        type = "plain_text",
                        text = $"📊 {platform} Feedback Analysis"
                    }
                },
                new
                {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = $"*Source:* {originalUrl}"
                    }
                },
                new
                {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = truncatedAnalysis
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
                                text = "📤 Share Analysis"
                            },
                            action_id = "share_analysis",
                            value = JsonSerializer.Serialize(new { analysis = analysisResult, platform, url = originalUrl })
                        },
                        new
                        {
                            type = "button",
                            text = new
                            {
                                type = "plain_text",
                                text = "🔄 Regenerate"
                            },
                            action_id = "regenerate_analysis",
                            value = originalUrl
                        },
                        new
                        {
                            type = "button",
                            text = new
                            {
                                type = "plain_text",
                                text = "📊 Export Report"
                            },
                            action_id = "export_analysis",
                            value = JsonSerializer.Serialize(new { analysis = analysisResult, platform, url = originalUrl })
                        }
                    }
                }
            }
        };
    }

    private async Task HandleGitHubCommand(HttpResponseData response, string text, string responseUrl)
    {
        await response.WriteStringAsync(JsonSerializer.Serialize(new
        {
            response_type = "ephemeral",
            text = "🔄 Analyzing GitHub feedback...",
            blocks = new[]
            {
                new
                {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = $"🔄 Analyzing GitHub URL: `{text}`"
                    }
                }
            }
        }));

        _ = Task.Run(async () => await ProcessFeedbackAsync(text, responseUrl, ""));
    }

    private async Task HandleYouTubeCommand(HttpResponseData response, string text, string responseUrl)
    {
        await response.WriteStringAsync(JsonSerializer.Serialize(new
        {
            response_type = "ephemeral",
            text = "🔄 Analyzing YouTube feedback...",
            blocks = new[]
            {
                new
                {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = $"🔄 Analyzing YouTube video: `{text}`"
                    }
                }
            }
        }));

        _ = Task.Run(async () => await ProcessFeedbackAsync(text, responseUrl, ""));
    }

    private async Task HandleRedditCommand(HttpResponseData response, string text, string responseUrl)
    {
        await response.WriteStringAsync(JsonSerializer.Serialize(new
        {
            response_type = "ephemeral",
            text = "🔄 Analyzing Reddit feedback...",
            blocks = new[]
            {
                new
                {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = $"🔄 Analyzing Reddit thread: `{text}`"
                    }
                }
            }
        }));

        _ = Task.Run(async () => await ProcessFeedbackAsync(text, responseUrl, ""));
    }

    private async Task HandleShareAnalysis(SlackInteractionPayload payload)
    {
        // Implementation for sharing analysis
        _logger.LogInformation("Sharing analysis for user {UserId}", payload.User?.Id);
    }

    private async Task HandleRegenerateAnalysis(SlackInteractionPayload payload)
    {
        // Implementation for regenerating analysis
        _logger.LogInformation("Regenerating analysis for user {UserId}", payload.User?.Id);
    }

    private async Task HandleExportAnalysis(SlackInteractionPayload payload)
    {
        // Implementation for exporting analysis
        _logger.LogInformation("Exporting analysis for user {UserId}", payload.User?.Id);
    }

    private async Task SendReportToSlack(AnalysisReport report)
    {
        // Implementation for sending reports to Slack channels
        _logger.LogInformation("Sending report {ReportId} to Slack", report.Id);
    }

    private async Task SendDelayedResponse(string responseUrl, object message)
    {
        using var httpClient = new HttpClient();
        var json = JsonSerializer.Serialize(message);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        await httpClient.PostAsync(responseUrl, content);
    }

    private Dictionary<string, string> ParseSlackFormData(string formData)
    {
        var result = new Dictionary<string, string>();
        var pairs = formData.Split('&');

        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=', 2);
            if (keyValue.Length == 2)
            {
                var key = System.Web.HttpUtility.UrlDecode(keyValue[0]);
                var value = System.Web.HttpUtility.UrlDecode(keyValue[1]);
                result[key] = value;
            }
        }

        return result;
    }

    private bool ValidateSlackRequest(Dictionary<string, string> formData)
    {
        var slackToken = _configuration["SlackVerificationToken"];
        var requestToken = formData.GetValueOrDefault("token", "");
        
        return !string.IsNullOrEmpty(slackToken) && slackToken == requestToken;
    }

    private string ExtractYouTubeVideoId(string url)
    {
        // Extract video ID from YouTube URL
        var patterns = new[]
        {
            @"(?:https?://)?(?:www\.)?(?:youtube\.com/watch\?v=|youtu\.be/)([a-zA-Z0-9_-]{11})",
            @"(?:https?://)?(?:www\.)?youtube\.com/embed/([a-zA-Z0-9_-]{11})"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(url, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return string.Empty;
    }

    private string ExtractHackerNewsStoryId(string url)
    {
        var match = System.Text.RegularExpressions.Regex.Match(url, @"item\?id=(\d+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private string TruncateForSlack(string text, int maxLength = 3000)
    {
        if (text.Length <= maxLength) return text;
        
        return text.Substring(0, maxLength - 3) + "...";
    }
}

// Supporting classes for Slack integration
public class SlackInteractionPayload
{
    public string? Type { get; set; }
    public SlackUser? User { get; set; }
    public SlackAction[]? Actions { get; set; }
    public string? ResponseUrl { get; set; }
}

public class SlackUser
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

public class SlackAction
{
    public string? ActionId { get; set; }
    public string? Value { get; set; }
    public string? Type { get; set; }
}

public class AnalysisReport
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Platform { get; set; }
    public DateTime CreatedAt { get; set; }
}
