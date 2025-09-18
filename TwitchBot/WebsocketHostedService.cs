using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Core;
using Microsoft.Extensions.Hosting;
using TwitchLib.EventSub.Websockets.Handler.Channel.ChannelPoints.Redemptions;
using System.Windows.Threading;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api;
using System.Windows;
using TwitchLib.Api.Helix;
using TwitchLib.Api.Interfaces;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.Api.Helix.Models.Subscriptions;
using System.Collections;
using TwitchBot.Utility_Code;

namespace TwitchBot
{
    class EventSubTypeAndVersion
    {
        string type;
        string version;

        public EventSubTypeAndVersion(string type, string version)
        {
            this.type = type;
            this.version = version;
        }

        public String GetTypeString()
        {
            return type;
        }

        public String GetVersionString()
        {
            return version;
        }
    }

    public class WebsocketHostedService : IHostedService
    {
        private readonly ILogger<WebsocketHostedService> _logger;
        private readonly EventSubWebsocketClient _eventSubWebsocketClient;
        TwitchAPI _TwitchAPI = GlobalObjects._TwitchAPI;

        bool shutdownWebsocket = false;

        public WebsocketHostedService(ILogger<WebsocketHostedService> logger, EventSubWebsocketClient eventSubWebsocketClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventSubWebsocketClient = eventSubWebsocketClient ?? throw new ArgumentNullException(nameof(eventSubWebsocketClient));


            _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
            _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
            _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
            _eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;

            _eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointsCustomRewardRedemptionAdd;
            _eventSubWebsocketClient.ChannelAdBreakBegin += OnChannelAdBreakBegin;


            _eventSubWebsocketClient.ChannelFollow += OnChannelFollow;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Uri eventSubWebsocketUri = new System.Uri("wss://eventsub.wss.twitch.tv/ws?keepalive_timeout_seconds=10");
            await _eventSubWebsocketClient.ConnectAsync(eventSubWebsocketUri);  //defaults to wss://eventsub.wss.twitch.tv/ws
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            shutdownWebsocket = true;

            if(GlobalObjects.TwitchBroadcasterUserId != "-1")
            {
                if(GlobalObjects.EventSubSubscribedEvents != null && GlobalObjects.EventSubSubscribedEvents.Length > 0)
                {
                    foreach (var subscription in GlobalObjects.EventSubSubscribedEvents)
                    {
                        //_logger.LogInformation($"EventSub Subscription {subscription.Id} status: {subscription.Status}");
                        if (subscription.Status == "enable")
                        {
                            try
                            {
                                bool deleteSubResult = await 
                                    .Helix.EventSub.DeleteEventSubSubscriptionAsync(subscription.Id);

                                if (deleteSubResult)
                                {
                                    _logger.LogInformation($"EventSub: Subscription {subscription.Id} deleted succesfully");
                                    WPFUtility.WriteToLog($"EventSub: Subscription {subscription.Id} deleted succesfully");
                                }
                                else
                                {
                                    _logger.LogError($"EventSub: Subscription {subscription.Id} deleted unsuccesfully");
                                    WPFUtility.WriteToLog($"EventSub: Subscription {subscription.Id} deleted unsuccesfully");
                                }
                            }
                            catch (Exception except)
                            {
                                _logger.LogError($"EventSub: Error occurred when deleting subscription {subscription.Id}\t{except.Message}");
                                WPFUtility.WriteToLog($"EventSub: Error occurred when deleting subscription {subscription.Id}\t{except.Message}");
                            }
                        }
                    }
                }
            }

            await _eventSubWebsocketClient.DisconnectAsync();
        }

        private async Task OnWebsocketConnected(object? sender, WebsocketConnectedArgs e)
        {
            _logger.LogInformation($"Websocket {_eventSubWebsocketClient.SessionId} connected!");


            //checks if userId is set to ensure EventSub subscriptions happen only when _TwitchAPI is connected
            //(not sure how to manually trigger connecting by a WPF button press)
            if (!e.IsRequestedReconnect && GlobalObjects.TwitchBroadcasterUserId != "-1")   
            {
                //Subscribe to topics via the TwitchApi.Helix.EventSub object, this example shows how to subscribe to the channel follow event used in the example above.

                //var conditions = new Dictionary<string, string>()
                //{
                //    { "broadcaster_user_id", _TwitchAPI.Settings.ClientId }
                //};

                //var subscriptionResponse = await _TwitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.follow", "2", conditions,
                //EventSubTransportMethod.Websocket, _eventSubWebsocketClient.SessionId, accessToken: _TwitchAPI.Helix.EventSub.GetAccessTokenAsync().Result);

                //You can find more examples on the subscription types and their requirements here https://dev.twitch.tv/docs/eventsub/eventsub-subscription-types/
                //Prerequisite: Twitchlib.Api nuget package installed (included in the Twitchlib package automatically)



                /*
                Make sure to check https://dev.twitch.tv/docs/eventsub/eventsub-subscription-types to find the specific type string
                for each subscription (e.g. "channel.subscribe" to listen for new sunscriptions (resubs are NOT shown here))
                along with if other conditons are required (e.g. moderator user id or user id)
                */

                List<EventSubTypeAndVersion> eventSubSubscriptionList = new List<EventSubTypeAndVersion>();

                eventSubSubscriptionList.Add(new EventSubTypeAndVersion("channel.channel_points_custom_reward_redemption.add", "1"));
                eventSubSubscriptionList.Add(new EventSubTypeAndVersion("channel.ad_break.begin", "1"));


                var conditions = new Dictionary<string, string>()
                {
                    { "broadcaster_user_id", GlobalObjects.TwitchBroadcasterUserId }
                };


                foreach(var eventSubSubscription in eventSubSubscriptionList)
                {
                    await _TwitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    eventSubSubscription.GetTypeString(),
                    eventSubSubscription.GetVersionString(),
                    conditions,
                    EventSubTransportMethod.Websocket,
                    _eventSubWebsocketClient.SessionId);    //can return a response about individual subscription made. saving for printing info on all subscriptions instead
                }

                var currentSubscriptions = _TwitchAPI.Helix.EventSub.GetEventSubSubscriptionsAsync().Result;
                _logger.LogInformation($"EventSub: Total Subscriptions Made: {currentSubscriptions.Total}\tTotal Cost of Subscriptions: {currentSubscriptions.MaxTotalCost}");

                foreach (var subscription in currentSubscriptions.Subscriptions)
                {
                    //ensures that only active subscriptions are reported on. May still have subscriptions with status "websocket_disconnected"
                    //after restart or reconnect (possibly caused by not explicitly calling DeleteEventSubSubscriptionAsync() before last disconnection)
                    if (subscription.Status == "enabled")
                    {
                        _logger.LogInformation($"EventSub: New subscription made of type: {subscription.Type}" +
                        $"\tID: {subscription.Id}\tCost {subscription.Cost}\tStatus: {subscription.Status}\t" +
                        $"Creation Date: {subscription.CreatedAt}");

                        WPFUtility.WriteToLog($"EventSub: New subscription made of type: {subscription.Type}");
                    }
                }

            }

        }


        private async Task OnWebsocketDisconnected(object? sender, EventArgs e)
        {
            _logger.LogError($"EventSub: Websocket {_eventSubWebsocketClient.SessionId} disconnected!");

            //ensures code doesn't try to reconnect to websocket after user closes application
            if (!shutdownWebsocket)
            {
                // Don't do this in production. You should implement a better reconnect strategy with exponential backoff
                while (!await _eventSubWebsocketClient.ReconnectAsync())
                {
                    _logger.LogError("EventSub: Websocket reconnect failed!");
                    await Task.Delay(1000);
                }
            }
        }

        private async Task OnWebsocketReconnected(object? sender, EventArgs e)
        {
            _logger.LogWarning($"EventSub: Websocket {_eventSubWebsocketClient.SessionId} reconnected");
        }

        private async Task OnErrorOccurred(object? sender, ErrorOccuredArgs e)
        {
            _logger.LogError($"EventSub: Websocket {_eventSubWebsocketClient.SessionId} - Error \t{e.Message}");
        }





        private async Task OnChannelPointsCustomRewardRedemptionAdd(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
        {
            if (!GlobalObjects.botIsActive)
                return;

            var pointsRedemption = e.Notification.Payload.Event;
            TwitchPointsRedeems.OnChannelPointsRewardRedeemed(e);
            _logger.LogInformation($"EventSub: Points Redemption of {pointsRedemption.Reward.Title} by {pointsRedemption.UserName} at {pointsRedemption.RedeemedAt}");
        }

        private async Task OnChannelFollow(object? sender, ChannelFollowArgs e)
        {
            if (!GlobalObjects.botIsActive)
                return;

            var eventData = e.Notification.Payload.Event;
            _logger.LogInformation($"EventSub: {eventData.UserName} followed {eventData.BroadcasterUserName} at {eventData.FollowedAt}");
        }

        private async Task OnChannelAdBreakBegin(object? sender, ChannelAdBreakBeginArgs e)
        {
            if (!GlobalObjects.botIsActive)
                return;

            new Thread(delegate () {
                    TwitchUtility.OnCommercial_NewThread(e);
                }).Start();

                _logger.LogInformation($"EventSub: Ad break started at {e.Notification.Payload.Event.StartedAt} for {e.Notification.Payload.Event.DurationSeconds} seconds");
        }
    }
}
