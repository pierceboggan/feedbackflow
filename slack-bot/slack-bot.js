const { App } = require('@slack/bolt');
const axios = require('axios');
require('dotenv').config();

// Initialize Slack app
const app = new App({
  token: process.env.SLACK_BOT_TOKEN,
  signingSecret: process.env.SLACK_SIGNING_SECRET,
  socketMode: process.env.SLACK_SOCKET_MODE === 'true',
  appToken: process.env.SLACK_APP_TOKEN,
  port: process.env.PORT || 3000,
});

// FeedbackFlow API configuration
const FEEDBACKFLOW_API_BASE = process.env.FEEDBACKFLOW_API_BASE || 'https://your-feedbackflow-functions.azurewebsites.net/api';
const FEEDBACKFLOW_API_KEY = process.env.FEEDBACKFLOW_API_KEY;

// Platform detection patterns
const PLATFORM_PATTERNS = {
  github: /github\.com/i,
  youtube: /(?:youtube\.com|youtu\.be)/i,
  reddit: /reddit\.com/i,
  hackernews: /news\.ycombinator\.com/i,
  devblogs: /devblogs\.microsoft\.com/i,
  twitter: /(?:twitter\.com|x\.com)/i,
  bluesky: /bsky\.app/i,
};

// Handle /feedbackflow slash command
app.command('/feedbackflow', async ({ command, ack, respond, client }) => {
  try {
    await ack();

    const url = command.text?.trim();
    if (!url) {
      await respond({
        response_type: 'ephemeral',
        text: '❌ Please provide a URL to analyze. Example: `/feedbackflow https://github.com/user/repo/issues/123`',
      });
      return;
    }

    // Send initial response
    await respond({
      response_type: 'ephemeral',
      text: '🔄 Analyzing feedback...',
      blocks: [
        {
          type: 'section',
          text: {
            type: 'mrkdwn',
            text: `🔄 Working on analyzing: \`${url}\``,
          },
        },
      ],
    });

    // Process feedback asynchronously
    setTimeout(async () => {
      try {
        const result = await analyzeFeedback(url);
        await sendAnalysisResult(command.response_url, result, url);
      } catch (error) {
        console.error('Error processing feedback:', error);
        await sendErrorResponse(command.response_url, error.message);
      }
    }, 1000);

  } catch (error) {
    console.error('Error handling /feedbackflow command:', error);
    await respond({
      response_type: 'ephemeral',
      text: '❌ Sorry, something went wrong while processing your request.',
    });
  }
});

// Handle platform-specific commands
const platformCommands = ['ff-github', 'ff-youtube', 'ff-reddit', 'ff-hackernews', 'ff-devblogs'];

platformCommands.forEach(commandName => {
  app.command(`/${commandName}`, async ({ command, ack, respond }) => {
    try {
      await ack();

      const url = command.text?.trim();
      if (!url) {
        const platform = commandName.replace('ff-', '');
        await respond({
          response_type: 'ephemeral',
          text: `❌ Please provide a ${platform} URL to analyze.`,
        });
        return;
      }

      // Send initial response
      const platform = commandName.replace('ff-', '').toUpperCase();
      await respond({
        response_type: 'ephemeral',
        text: `🔄 Analyzing ${platform} feedback...`,
        blocks: [
          {
            type: 'section',
            text: {
              type: 'mrkdwn',
              text: `🔄 Analyzing ${platform} content: \`${url}\``,
            },
          },
        ],
      });

      // Process feedback asynchronously
      setTimeout(async () => {
        try {
          const result = await analyzeFeedback(url);
          await sendAnalysisResult(command.response_url, result, url);
        } catch (error) {
          console.error(`Error processing ${platform} feedback:`, error);
          await sendErrorResponse(command.response_url, error.message);
        }
      }, 1000);

    } catch (error) {
      console.error(`Error handling /${commandName} command:`, error);
    }
  });
});

// Handle interactive components
app.action('share_analysis', async ({ ack, body, client }) => {
  try {
    await ack();
    
    const value = JSON.parse(body.actions[0].value);
    
    await client.chat.postMessage({
      channel: body.channel.id,
      blocks: [
        {
          type: 'header',
          text: {
            type: 'plain_text',
            text: `📊 ${value.platform} Analysis Results (Shared)`,
          },
        },
        {
          type: 'section',
          text: {
            type: 'mrkdwn',
            text: `*Source:* <${value.url}|View Original>`,
          },
        },
        {
          type: 'section',
          text: {
            type: 'mrkdwn',
            text: truncateText(value.analysis, 2500),
          },
        },
        {
          type: 'context',
          elements: [
            {
              type: 'mrkdwn',
              text: `Shared by <@${body.user.id}>`,
            },
          ],
        },
      ],
    });

  } catch (error) {
    console.error('Error sharing analysis:', error);
  }
});

app.action('regenerate_analysis', async ({ ack, body, respond }) => {
  try {
    await ack();
    
    const url = body.actions[0].value;
    
    await respond({
      response_type: 'ephemeral',
      text: '🔄 Regenerating analysis...',
      replace_original: false,
    });

    setTimeout(async () => {
      try {
        const result = await analyzeFeedback(url);
        await sendAnalysisResult(body.response_url, result, url);
      } catch (error) {
        await sendErrorResponse(body.response_url, error.message);
      }
    }, 1000);

  } catch (error) {
    console.error('Error regenerating analysis:', error);
  }
});

app.action('export_analysis', async ({ ack, body, client }) => {
  try {
    await ack();
    
    const value = JSON.parse(body.actions[0].value);
    
    // Create a text file with the analysis
    const analysisContent = `FeedbackFlow Analysis Report
=============================

Platform: ${value.platform}
Source: ${value.url}
Generated: ${new Date().toISOString()}

Analysis Results:
${value.analysis}

---
Generated by FeedbackFlow Slack Bot
`;

    await client.files.uploadV2({
      channel_id: body.channel.id,
      filename: `feedbackflow-analysis-${Date.now()}.txt`,
      file_uploads: [
        {
          content: analysisContent,
          filename: `feedbackflow-analysis-${Date.now()}.txt`,
        },
      ],
      initial_comment: `📊 Analysis report exported by <@${body.user.id}>`,
    });

  } catch (error) {
    console.error('Error exporting analysis:', error);
  }
});

// Handle app home opened events
app.event('app_home_opened', async ({ event, client }) => {
  try {
    await client.views.publish({
      user_id: event.user,
      view: {
        type: 'home',
        blocks: [
          {
            type: 'header',
            text: {
              type: 'plain_text',
              text: '📊 FeedbackFlow Bot',
            },
          },
          {
            type: 'section',
            text: {
              type: 'mrkdwn',
              text: '*Welcome to FeedbackFlow!* 👋\n\nAnalyze feedback from multiple platforms directly in Slack.',
            },
          },
          {
            type: 'divider',
          },
          {
            type: 'section',
            text: {
              type: 'mrkdwn',
              text: '*Available Commands:*\n\n• `/feedbackflow <url>` - Analyze any supported platform\n• `/ff-github <url>` - GitHub issues/PRs\n• `/ff-youtube <url>` - YouTube videos\n• `/ff-reddit <url>` - Reddit threads\n• `/ff-hackernews <url>` - Hacker News stories\n• `/ff-devblogs <url>` - Microsoft DevBlogs',
            },
          },
          {
            type: 'section',
            text: {
              type: 'mrkdwn',
              text: '*Supported Platforms:*\n\n🐙 GitHub • 📺 YouTube • 🤖 Reddit • 📰 Hacker News • 📝 DevBlogs • 🐦 Twitter • ☁️ BlueSky',
            },
          },
          {
            type: 'divider',
          },
          {
            type: 'section',
            text: {
              type: 'mrkdwn',
              text: '💡 *Tip:* After analysis, use the action buttons to share results, regenerate analysis, or export reports!',
            },
          },
        ],
      },
    });
  } catch (error) {
    console.error('Error publishing home view:', error);
  }
});

// Core analysis function
async function analyzeFeedback(url) {
  const platform = detectPlatform(url);
  const endpoint = getApiEndpoint(platform);
  
  const config = {
    method: 'GET',
    url: `${FEEDBACKFLOW_API_BASE}/${endpoint}`,
    params: getApiParams(platform, url),
    headers: {
      'x-functions-key': FEEDBACKFLOW_API_KEY,
    },
    timeout: 120000, // 2 minutes
  };

  const response = await axios(config);
  
  if (response.status !== 200) {
    throw new Error(`API returned status ${response.status}`);
  }

  return {
    platform: platform.charAt(0).toUpperCase() + platform.slice(1),
    data: response.data,
    analysis: formatAnalysisResult(response.data),
  };
}

function detectPlatform(url) {
  for (const [platform, pattern] of Object.entries(PLATFORM_PATTERNS)) {
    if (pattern.test(url)) {
      return platform;
    }
  }
  return 'unknown';
}

function getApiEndpoint(platform) {
  const endpoints = {
    github: 'GetGitHubFeedback',
    youtube: 'GetYouTubeFeedback',
    reddit: 'GetRedditFeedback',
    hackernews: 'GetHackerNewsFeedback',
    devblogs: 'GetDevBlogsFeedback',
    twitter: 'GetTwitterFeedback',
    bluesky: 'GetBlueSkyFeedback',
  };
  
  return endpoints[platform] || 'GetGitHubFeedback';
}

function getApiParams(platform, url) {
  switch (platform) {
    case 'github':
      return { url };
    case 'youtube':
      const videoId = extractYouTubeVideoId(url);
      return { videos: videoId };
    case 'reddit':
      return { url };
    case 'hackernews':
      const storyId = extractHackerNewsStoryId(url);
      return { ids: storyId };
    case 'devblogs':
      return { articleUrl: url };
    case 'twitter':
      return { tweet: url };
    case 'bluesky':
      return { post: url };
    default:
      return { url };
  }
}

function extractYouTubeVideoId(url) {
  const patterns = [
    /(?:youtube\.com\/watch\?v=|youtu\.be\/)([a-zA-Z0-9_-]{11})/,
    /youtube\.com\/embed\/([a-zA-Z0-9_-]{11})/,
  ];
  
  for (const pattern of patterns) {
    const match = url.match(pattern);
    if (match) return match[1];
  }
  
  return '';
}

function extractHackerNewsStoryId(url) {
  const match = url.match(/item\?id=(\d+)/);
  return match ? match[1] : '';
}

function formatAnalysisResult(data) {
  if (typeof data === 'string') {
    return data;
  }
  
  if (data.analysis) {
    return data.analysis;
  }
  
  if (data.summary) {
    return data.summary;
  }
  
  return 'Analysis completed successfully. Please check the original source for detailed results.';
}

async function sendAnalysisResult(responseUrl, result, originalUrl) {
  const message = {
    response_type: 'in_channel',
    blocks: [
      {
        type: 'header',
        text: {
          type: 'plain_text',
          text: `📊 ${result.platform} Feedback Analysis Complete`,
        },
      },
      {
        type: 'section',
        fields: [
          {
            type: 'mrkdwn',
            text: `*Platform:*\n${result.platform}`,
          },
          {
            type: 'mrkdwn',
            text: `*Source:*\n<${originalUrl}|View Original>`,
          },
        ],
      },
      {
        type: 'section',
        text: {
          type: 'mrkdwn',
          text: `*Analysis Results:*\n${truncateText(result.analysis, 2500)}`,
        },
      },
      {
        type: 'actions',
        elements: [
          {
            type: 'button',
            text: {
              type: 'plain_text',
              text: '📤 Share Analysis',
              emoji: true,
            },
            action_id: 'share_analysis',
            style: 'primary',
            value: JSON.stringify({
              analysis: result.analysis,
              platform: result.platform,
              url: originalUrl,
            }),
          },
          {
            type: 'button',
            text: {
              type: 'plain_text',
              text: '🔄 Regenerate',
              emoji: true,
            },
            action_id: 'regenerate_analysis',
            value: originalUrl,
          },
          {
            type: 'button',
            text: {
              type: 'plain_text',
              text: '📊 Export Report',
              emoji: true,
            },
            action_id: 'export_analysis',
            value: JSON.stringify({
              analysis: result.analysis,
              platform: result.platform,
              url: originalUrl,
            }),
          },
        ],
      },
    ],
  };

  await axios.post(responseUrl, message);
}

async function sendErrorResponse(responseUrl, errorMessage) {
  const message = {
    response_type: 'ephemeral',
    blocks: [
      {
        type: 'section',
        text: {
          type: 'mrkdwn',
          text: `❌ *Error analyzing feedback:*\n${errorMessage}`,
        },
      },
      {
        type: 'section',
        text: {
          type: 'mrkdwn',
          text: '💡 *Troubleshooting tips:*\n• Check that the URL is valid and accessible\n• Ensure the platform is supported\n• Try again in a few moments',
        },
      },
    ],
  };

  await axios.post(responseUrl, message);
}

function truncateText(text, maxLength = 3000) {
  if (text.length <= maxLength) return text;
  return text.substring(0, maxLength - 3) + '...';
}

// Global error handler
app.error(async (error) => {
  console.error('Slack app error:', error);
});

// Start the app
(async () => {
  try {
    await app.start();
    console.log('⚡️ FeedbackFlow Slack bot is running!');
    console.log(`🚀 Server running on port ${process.env.PORT || 3000}`);
    
    if (process.env.SLACK_SOCKET_MODE === 'true') {
      console.log('🔌 Running in Socket Mode');
    } else {
      console.log('🌐 Running in HTTP Mode');
    }
  } catch (error) {
    console.error('Failed to start the bot:', error);
    process.exit(1);
  }
})();

// Graceful shutdown
process.on('SIGINT', async () => {
  console.log('⏸️  Shutting down FeedbackFlow Slack bot...');
  await app.stop();
  process.exit(0);
});

process.on('SIGTERM', async () => {
  console.log('⏸️  Shutting down FeedbackFlow Slack bot...');
  await app.stop();
  process.exit(0);
});
