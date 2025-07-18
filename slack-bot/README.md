# FeedbackFlow Slack Bot

A Node.js Slack bot that integrates with FeedbackFlow to provide feedback analysis directly in Slack.

## Features

- **Multi-platform Analysis**: Analyze feedback from GitHub, YouTube, Reddit, Hacker News, DevBlogs, Twitter, and BlueSky
- **Slash Commands**: Simple commands for quick analysis
- **Interactive Components**: Share, regenerate, and export analysis results
- **Real-time Processing**: Get results delivered directly to Slack
- **File Export**: Export analysis reports as text files

## Quick Start

### 1. Prerequisites

- Node.js 18+ installed
- A Slack workspace where you can install apps
- Access to a deployed FeedbackFlow instance

### 2. Installation

```bash
cd slack-bot
npm install
```

### 3. Environment Setup

```bash
cp .env.example .env
# Edit .env with your configuration
```

### 4. Slack App Setup

1. Create a new Slack app at [api.slack.com](https://api.slack.com/apps)
2. Configure OAuth scopes (see main documentation)
3. Set up slash commands
4. Enable Socket Mode (recommended)
5. Install the app to your workspace

### 5. Run the Bot

```bash
# Development mode with auto-restart
npm run dev

# Production mode
npm start
```

## Available Commands

- `/feedbackflow <url>` - Analyze any supported URL
- `/ff-github <url>` - Analyze GitHub content
- `/ff-youtube <url>` - Analyze YouTube videos
- `/ff-reddit <url>` - Analyze Reddit threads
- `/ff-hackernews <url>` - Analyze Hacker News stories
- `/ff-devblogs <url>` - Analyze DevBlogs articles

## Interactive Features

After analysis completion, users can:

- **📤 Share Analysis** - Share results with the channel
- **🔄 Regenerate** - Re-run the analysis with fresh data
- **📊 Export Report** - Download a detailed text report

## Configuration

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `SLACK_BOT_TOKEN` | Bot OAuth token (xoxb-...) | Yes |
| `SLACK_SIGNING_SECRET` | App signing secret | Yes |
| `SLACK_SOCKET_MODE` | Enable Socket Mode | No (recommended) |
| `SLACK_APP_TOKEN` | App token for Socket Mode | If Socket Mode |
| `FEEDBACKFLOW_API_BASE` | FeedbackFlow API base URL | Yes |
| `FEEDBACKFLOW_API_KEY` | FeedbackFlow API key | Yes |
| `PORT` | Server port | No (default: 3000) |

### Deployment Options

#### Option 1: Socket Mode (Recommended)
- Set `SLACK_SOCKET_MODE=true`
- No need for public URLs or webhooks
- Great for development and internal deployments

#### Option 2: HTTP Mode
- Set `SLACK_SOCKET_MODE=false`
- Requires public HTTPS endpoints
- Better for production deployments

## Deployment

### Heroku

```bash
# Login to Heroku
heroku login

# Create app
heroku create your-feedbackflow-bot

# Set environment variables
heroku config:set SLACK_BOT_TOKEN=xoxb-your-token
heroku config:set SLACK_SIGNING_SECRET=your-secret
heroku config:set FEEDBACKFLOW_API_BASE=your-api-url
heroku config:set FEEDBACKFLOW_API_KEY=your-api-key

# Deploy
git push heroku main
```

### Azure Container Instances

```bash
# Build and push to registry
docker build -t feedbackflow-slack-bot .
docker tag feedbackflow-slack-bot your-registry.azurecr.io/feedbackflow-slack-bot
docker push your-registry.azurecr.io/feedbackflow-slack-bot

# Deploy to Azure
az container create \
  --resource-group your-rg \
  --name feedbackflow-slack-bot \
  --image your-registry.azurecr.io/feedbackflow-slack-bot \
  --environment-variables \
    SLACK_BOT_TOKEN=xoxb-your-token \
    SLACK_SIGNING_SECRET=your-secret \
    FEEDBACKFLOW_API_BASE=your-api-url \
    FEEDBACKFLOW_API_KEY=your-api-key
```

### AWS Lambda

Use the Serverless Framework or AWS SAM to deploy as a Lambda function.

## Development

### Running Locally

```bash
# Install dependencies
npm install

# Start development server
npm run dev
```

### Linting

```bash
# Check code style
npm run lint

# Fix auto-fixable issues
npm run lint:fix
```

### Testing Commands

1. Invite the bot to a channel: `/invite @FeedbackFlow`
2. Test basic functionality: `/feedbackflow https://github.com/microsoft/vscode/issues/1`
3. Try platform-specific commands
4. Test interactive buttons

## Troubleshooting

### Common Issues

1. **"Command not recognized"**
   - Verify slash commands are set up in Slack app settings
   - Check that the bot is installed in the workspace

2. **"Bot not responding"**
   - Check bot token is valid
   - Verify signing secret is correct
   - Check server logs for errors

3. **"Analysis failed"**
   - Verify FeedbackFlow API is accessible
   - Check API key is valid
   - Ensure URL format is supported

4. **Socket Mode issues**
   - Verify app token is set
   - Check that Socket Mode is enabled in Slack app settings

### Debug Mode

Enable debug logging:

```bash
LOG_LEVEL=debug npm start
```

### Checking Logs

Monitor the application logs for detailed error information:

```bash
# Heroku
heroku logs --tail

# Local development
# Logs appear in console
```

## Architecture

```
Slack → Slack Bot → FeedbackFlow API → Analysis Services
                     ↓
                Azure Functions
                     ↓
            GitHub/YouTube/Reddit/etc.
```

## Security

- Store sensitive tokens as environment variables
- Use HTTPS for all communications
- Regularly rotate Slack app credentials
- Monitor usage and set rate limits

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## Support

For issues with the Slack bot:

1. Check the application logs
2. Verify Slack app configuration
3. Test with a known-good URL
4. Open an issue with error details

## License

MIT License - see LICENSE file for details.
