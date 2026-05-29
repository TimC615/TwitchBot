using Newtonsoft.Json;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateRedemptionStatus;
using TwitchLib.Api.Helix.Models.Channels.SendChatMessage;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.Models;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.PubSub.Events;

namespace TwitchBot.Utility_Code
{
    class TwitchUtility
    {
        public async static Task CheckAccessToken()
        {
            //Log("Checking AccessToken...");

            var tokenResponse = GlobalObjects._TwitchAPI.Auth.ValidateAccessTokenAsync();
            ValidateAccessTokenResponse tokenResult = await tokenResponse;

            //tokenResult is null if current Access Token is invalid
            //added ExpiresIn case to allow for the code needing an access token to fully execute
            if (tokenResult == null || tokenResult.ExpiresIn <= 10)
            {
                WPFUtility.WriteToLog("CheckAccessToken: Bad token, refreshing");

                try
                {
                    var result = GlobalObjects._TwitchAPI.Auth.RefreshAuthTokenAsync(Properties.Settings.Default.TwitchClientReftreshToken, Properties.Settings.Default.clientsecret); //start the process of refreshing tokens
                    RefreshResponse response = await result;    //retreive the results of token refresh

                    //Log($"Old Access: {_TwitchAPI.Settings.AccessToken}\t\tOld Refresh: {CachedRefreshToken}");

                    Properties.Settings.Default.TwitchAccessToken = response.AccessToken;
                    GlobalObjects._TwitchAPI.Settings.AccessToken = response.AccessToken;

                    Properties.Settings.Default.TwitchClientReftreshToken = response.RefreshToken;

                    //Log($"New AccessToken: {_TwitchAPI.Settings.AccessToken}\t\tNew Refresh: {CachedRefreshToken}");
                }
                catch (Exception except)
                {
                    WPFUtility.WriteToLog($"CheckAuthToken Error: {except.Message}");
                }
            }
        }

        //Used as a centralized handler of points redemption status updating.
        //Returns true if everything worked as expected and returns false if an error was encountered.
        //Can only be used on rewards that were created through this bot specifically.
        public async static void UpdateCustomPointRedemptionStatus(string broadcasterId, string rewardId, List<string> redemptionIds, UpdateCustomRewardRedemptionStatusRequest updateStatusRequest)
        {
            try
            {
                UpdateRedemptionStatusResponse test =  await GlobalObjects._TwitchAPI.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
                    broadcasterId, 
                    rewardId, 
                    redemptionIds, 
                    updateStatusRequest);
            }
            catch(Exception except)
            {
                WPFUtility.WriteToLog($"Error updating custom points redemtion status: {except.Message}");
            }
        }

        async static public void OnCommercial_NewThread(ChannelAdBreakBeginArgs e)
        {
            int commercialBreakLength = e.Payload.Event.DurationSeconds;
            int threadSleepLength = commercialBreakLength * 1000;

            WPFUtility.WriteToLog("EventSub OnCommercial: Ads started for " + commercialBreakLength + " seconds");

            await MainWindow.AppWindow.CheckAccessToken();

            string commercialBreakMessage = "";
            if (commercialBreakLength >= 60)
            {
                double commercialBreakLengthMin = (double)commercialBreakLength / 60;
                commercialBreakMessage = $"Ads have started and will last for {commercialBreakLengthMin} minutes. Feel free to stretch a bit, hydrate, or just chill out in chat!";
            }
            else
                commercialBreakMessage = $"Ads have started and will last for {commercialBreakLength} seconds. Feel free to stretch a bit, hydrate, or just chill out in chat!";

            TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, commercialBreakMessage, sendMessageAsChatBot: true);

            Thread.Sleep(threadSleepLength);
            WPFUtility.WriteToLog("EventSub OnCommercial: Ads have finished");
            //GlobalObjects._TwitchClient.SendMessage(GlobalObjects.TwitchChannelName, "Ad break is now done!");
        }

        static async public void ReinstateModRole(TwitchAPI _TwitchAPI, string TwitchChannelId, string userIdToMod, string username, int banLength)
        {
            Thread.Sleep(banLength * 1000); //wait for user's timeout to finish (seconds)

            try
            {
                await _TwitchAPI.Helix.Moderation.AddChannelModeratorAsync(TwitchChannelId, userIdToMod);

                List<string> testSearchMods = new List<string>();
                testSearchMods.Add(userIdToMod);
                GetModeratorsResponse modsResult = await _TwitchAPI.Helix.Moderation.GetModeratorsAsync(TwitchChannelId, testSearchMods);

                if (modsResult.Data.Length > 0)
                    WPFUtility.WriteToLog($"Mod Role reinstated for: {modsResult.Data[0].UserName}");
                else
                    throw new Exception("Unable to restore mod role for: " + username);
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog($"ReinstateModRole Error: {except.Message}");
            }
        }

        //old way bot handled sending messages was through _TwitchClient and Twitch's IRC channel system
        //e.g.
        /*
            _TwitchClient.SendReply(e.ChatMessage.Channel,
                e.ChatMessage.Id,
                "The current chat commands are: help, about, discord, twitter, lurk, joke, fact, roll, roulette, rouletteleaderboard, and 1st");
        */
        //where e is the object containing the current message
        static async public void SendChatMessage(TwitchAPI _TwitchAPI, string senderId, string broadcasterId, string message, string? parentResponseMessageId = null, bool sendMessageAsChatBot = false)
        {
            SendChatMessageRequest chatMessageRequest = new SendChatMessageRequest();
            chatMessageRequest.SenderId = senderId;
            chatMessageRequest.BroadcasterId = broadcasterId;
            chatMessageRequest.Message = message;

            if (parentResponseMessageId != null)
                chatMessageRequest.ReplyParentMessageId = parentResponseMessageId;

            if (sendMessageAsChatBot)
                chatMessageRequest.ForSourceOnly = true;

            SendChatMessageResponse sendMessageResponse = await _TwitchAPI.Helix.Chat.SendChatMessage(chatMessageRequest);

            foreach (var respInfo in sendMessageResponse.Data)
            {
                if (respInfo.IsSent)
                {
                    System.Console.WriteLine($"TwitchUtility.SendChatMessage \tMessId:{respInfo.MessageId}\tIsSent:{respInfo.IsSent}");
                }
                else
                {
                    WPFUtility.WriteToLog($"TwitchUtility.SendChatMessage MESSAGE NOT SENT \ttDropCode:{respInfo.DropReason.Code}\tDropMessage{respInfo.DropReason.Message}");
                }
            }
        }

        //Used as a troubleshooting step if there is an issue with autohandling redemption status of points rewards (specifically only ones made through this bot)
        static async public void RecreateCustomPointsReward(CreateCustomRewardsRequest createReward)
        {
            GetCustomRewardsResponse customRewards = new GetCustomRewardsResponse();

            //get list of rewards that were specifically created through this bot (as the next step can only work if this is true)
            try
            {
                customRewards = await GlobalObjects._TwitchAPI.Helix.ChannelPoints.GetCustomRewardAsync(
                GlobalObjects.TwitchBroadcasterUserId,
                onlyManageableRewards: true);
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog($"Error retreiving list of custom rewards: {except.Message}");
            }

            //iterate through list of rewards and if a reward already exists with the title of the new reward, the older reward is removed from Twitch
            foreach (CustomReward reward in customRewards.Data)
            {
                //used to ensure whatever reward previously had this name is removed
                if (reward.Title == createReward.Title)
                {
                    try
                    {
                        await GlobalObjects._TwitchAPI.Helix.ChannelPoints.DeleteCustomRewardAsync(GlobalObjects.TwitchBroadcasterUserId, reward.Id);
                    }
                    catch (Exception except)
                    {
                        WPFUtility.WriteToLog($"Error removing custom reward: {except.Message}");
                    }
                    break;
                }
            }

            bool rewardCreated = true;

            //create new reward with the information in the object from the method parameter
            try
            {
                CreateCustomRewardsResponse customRewardResponse = await GlobalObjects._TwitchAPI.Helix.ChannelPoints.CreateCustomRewardsAsync(
                GlobalObjects.TwitchBroadcasterUserId,
                createReward
                );
            }
            catch(Exception except)
            {
                rewardCreated = false;
                WPFUtility.WriteToLog($"Error creating custom reward \"{createReward.Title}\": {except.Message}");
            }

            if(rewardCreated)
                WPFUtility.WriteToLog($"Successfully created new points reward \"{createReward.Title}\". Currently can't autoset images to rewards made this way so this has to be done manually on Twitch.");
        }
    }
}
