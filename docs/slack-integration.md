# FeedbackFlow Slack Integration

This document provides comprehensive instructions for setting up and using the FeedbackFlow Slack integration.

## Overview

The FeedbackFlow Slack integration allows you to:

- Analyze feedback from any supported platform directly in Slack using slash commands
- Receive automatic notifications when analyses are completed
- Get weekly summary reports delivered to specified channels
- Share analysis results with team members
- Export reports directly from Slack

## Setup Instructions

### 1. Create a Slack App

1. Go to [api.slack.com](https://api.slack.com/apps) and click "Create New App"
2. Choose "From scratch" and give your app a name (e.g., "FeedbackFlow")
3. Select your workspace and click "Create App"

### 2. Configure OAuth & Permissions

1. In your app settings, go to "OAuth & Permissions"
2. Under "Scopes" → "Bot Token Scopes", add these permissions:
   - `channels:read` - View basic information about public channels
   - `chat:write` - Send messages as the app
   - `commands` - Add shortcuts and/or slash commands
   - `files:write` - Upload, edit, and delete files
   - `groups:read` - View basic information about private channels
   - `im:write` - Start direct messages with people
   - `users:read` - View people in your workspace

3. Click "Install to Workspace" and authorize the app
4. Copy the "Bot User OAuth Token" (starts with `xoxb-`)

### 3. Set Up Slash Commands

1. In your app settings, go to "Slash Commands"
2. Create the following commands:

   **Primary Command:**
   - Command: `/feedbackflow`
   - Request URL: `https://your-feedbackflow-functions.azurewebsites.net/api/SlackFeedbackCommand`
   - Short Description: `Analyze feedback from any supported platform`
   - Usage Hint: `<url>`

   **Platform-Specific Commands:**
   - Command: `/ff-github`
   - Request URL: `https://your-feedbackflow-functions.azurewebsites.net/api/SlackFeedbackCommand`
   - Short Description: `Analyze GitHub issues or pull requests`
   
   - Command: `/ff-youtube`
   - Request URL: `https://your-feedbackflow-functions.azurewebsites.net/api/SlackFeedbackCommand`
   - Short Description: `Analyze YouTube video comments`
   
   - Command: `/ff-reddit`
   - Request URL: `https://your-feedbackflow-functions.azurewebsites.net/api/SlackFeedbackCommand`
   - Short Description: `Analyze Reddit thread comments`

### 4. Configure Interactivity

1. Go to "Interactivity & Shortcuts"
2. Turn on Interactivity
3. Set Request URL: `https://your-feedbackflow-functions.azurewebsites.net/api/SlackInteractivity`

### 5. Get App Credentials

From your Slack app settings, collect:
- **Bot User OAuth Token** (OAuth & Permissions)
- **Verification Token** (Basic Information → App Credentials)
- **Signing Secret** (Basic Information → App Credentials)

### 6. Configure FeedbackFlow

Add the following to your FeedbackFlow configuration:

```json
{
  "Slack": {
    "BotToken": "xoxb-your-bot-token-here",
    "VerificationToken": "your-verification-token",
    "SigningSecret": "your-signing-secret",
    "DefaultChannelId": "C1234567890",
    "EnableNotifications": true,
    "EnableWeeklyReports": true,
    "NotificationChannels": "C1234567890,C0987654321",
    "ReportChannels": "C1234567890"
  }
}
```

## Usage

### Slash Commands

#### Basic Usage
```
/feedbackflow https://github.com/user/repo/issues/123
```
Analyzes feedback from any supported platform automatically.

#### Platform-Specific Commands
```
/ff-github https://github.com/user/repo/pull/456
/ff-youtube https://youtube.com/watch?v=abc123
/ff-reddit https://reddit.com/r/programming/comments/xyz/
```

### Interactive Features

After an analysis is completed, you'll see action buttons to:

- **📤 Share Analysis** - Share the results with the channel
- **🔄 Regenerate** - Run the analysis again with updated data
- **📊 Export Report** - Generate and download a detailed report

### Automatic Notifications

When enabled, FeedbackFlow will automatically send notifications to configured channels when:

- An analysis is completed
- An error occurs during processing
- Weekly reports are generated

### Weekly Reports

Weekly reports include:
- Total number of analyses performed
- Number of comments processed
- Top performing platforms
- Success rate statistics
- Key insights from the week

## Supported Platforms

FeedbackFlow Slack integration supports all the same platforms as the web application:

- **GitHub** - Issues, pull requests, discussions
- **YouTube** - Video comments
- **Reddit** - Thread comments and discussions
- **Hacker News** - Story comments
- **DevBlogs** - Microsoft DevBlogs articles
- **Twitter/X** - Tweet threads (with API access)
- **BlueSky** - Posts and replies

## Configuration Reference

### Environment Variables

Set these in your Azure Functions configuration:

```
Slack:BotToken=xoxb-your-bot-token
Slack:VerificationToken=your-verification-token
Slack:SigningSecret=your-signing-secret
Slack:DefaultChannelId=C1234567890
Slack:EnableNotifications=true
Slack:EnableWeeklyReports=true
Slack:NotificationChannels=C1234567890,C0987654321
Slack:ReportChannels=C1234567890
```

### Channel Configuration

- **Default Channel**: Primary channel for notifications
- **Notification Channels**: Comma-separated list of channels for analysis notifications
- **Report Channels**: Comma-separated list of channels for weekly reports

## Troubleshooting

### Common Issues

1. **"Invalid Slack token" Error**
   - Verify your verification token is correct
   - Check that the token hasn't expired

2. **Commands Not Working**
   - Ensure slash commands are properly configured
   - Verify the request URLs point to your Azure Functions

3. **No Notifications**
   - Check that notifications are enabled in configuration
   - Verify channel IDs are correct (starts with 'C')
   - Ensure the bot has permission to post in the channels

4. **Interactivity Issues**
   - Verify the interactivity request URL is set correctly
   - Check that the signing secret is configured properly

### Getting Channel IDs

To find a channel ID:
1. Right-click on the channel in Slack
2. Select "Copy link"
3. The channel ID is the last part of the URL (e.g., `C1234567890`)

### Testing the Integration

1. Use the "Test Connection" button in FeedbackFlow settings
2. Try a simple command: `/feedbackflow https://github.com/microsoft/vscode/issues/1`
3. Check Azure Functions logs for any errors

## Security Considerations

- Store sensitive tokens in Azure Key Vault or secure configuration
- Use HTTPS for all webhook URLs
- Regularly rotate Slack app credentials
- Monitor usage and set appropriate rate limits

## Support

For issues with the Slack integration:

1. Check the FeedbackFlow logs in Azure Functions
2. Verify Slack app configuration
3. Test with a simple, known-good URL
4. Contact support with specific error messages

## Advanced Features

### Custom Analysis Prompts

You can configure custom analysis prompts for different platforms in the FeedbackFlow settings. These will be used when analyzing content through Slack commands.

### Webhooks

Set up webhooks to receive notifications about:
- Analysis completions
- Errors and failures
- Weekly report generation

### Rate Limiting

The integration respects Slack's rate limits and includes automatic retry logic for failed requests.
