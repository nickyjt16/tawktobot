// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.BotBuilderSamples
{
    public class QnABot : ActivityHandler
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<QnABot> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AzureBlobTranscriptStore _myTranscripts = new AzureBlobTranscriptStore("DefaultEndpointsProtocol=https;AccountName=tawktostorage;AccountKey=KxYyQveb4dERoPfPM+i/zru8xFY31JLmmqKYZnklJ1oP5q82FjNiGYU9qnLLGFN38yGqnPDjrSUmZZlFhLeGWg==;EndpointSuffix=core.windows.net", "tawktostorage");

        public QnABot(IConfiguration configuration, ILogger<QnABot> logger, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            await _myTranscripts.LogActivityAsync(turnContext.Activity);

            List<string> storedTranscripts = new List<string>();
            PagedResult<Microsoft.Bot.Builder.TranscriptInfo> pagedResult = null;
            var pageSize = 0;
            do
            {
                pagedResult = await _myTranscripts.ListTranscriptsAsync("emulator", pagedResult?.ContinuationToken);
                pageSize = pagedResult.Items.Count();

                // transcript item contains ChannelId, Created, Id.
                // save the channelIds found by "ListTranscriptsAsync" to a local list.
                foreach (var item in pagedResult.Items)
                {
                    storedTranscripts.Add(item.Id);
                }
            } while (pagedResult.ContinuationToken != null);

            var httpClient = _httpClientFactory.CreateClient();

            var qnaMaker = new QnAMaker(new QnAMakerEndpoint
            {
                KnowledgeBaseId = _configuration["QnAKnowledgebaseId"],
                EndpointKey = _configuration["QnAEndpointKey"],
                Host = _configuration["QnAEndpointHostName"]
            },
            null,
            httpClient);

            _logger.LogInformation("Calling QnA Maker");

            var options = new QnAMakerOptions { Top = 1 };

            // The actual call to the QnA Maker service.
            var response = await qnaMaker.GetAnswersAsync(turnContext, options);
            if (response != null && response.Length > 0)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(response[0].Answer), cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("No QnA Maker answers were found."), cancellationToken);
            }
        }
    }
}
