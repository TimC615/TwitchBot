using Newtonsoft.Json;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Core.Extensions.System;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateRedemptionStatus;
using TwitchLib.Api.Helix.Models.Channels.GetAdSchedule;
using TwitchLib.Api.Helix.Models.Channels.GetChannelInformation;
using TwitchLib.Api.Helix.Models.Channels.SendChatMessage;
using TwitchLib.Api.Helix.Models.Channels.StartCommercial;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;
using TwitchLib.Api.Helix.Models.Polls.CreatePoll;
using TwitchLib.Api.Helix.Models.Predictions.CreatePrediction;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.Models;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.PubSub.Events;

namespace TwitchBot.Utility_Code
{
    public class ResetReward
    {
        string rewardId;
        string rewardTitle;
        int rewardCost;

        public ResetReward(string rewardId, string rewardTitle, int rewardCost)
        {
            this.rewardId = rewardId;
            this.rewardTitle = rewardTitle;
            this.rewardCost = rewardCost;
        }

        public string getId()
        {
            return this.rewardId;
        }

        //title probably isn't needed in the grand scheme of things but in case I want to filter by title in the future
        public string getTitle()
        {
            return rewardTitle;
        }

        public int getCost()
        {
            return rewardCost;
        }
    }



    class TwitchUtility
    {
        private static System.Timers.Timer adBreakWarningTimer;
        private static int ADBREAKTIMERINTERVALMINUTES = 1; //set how many minutes between a check of whether the current time relative to the next scheduled ad is below the set threshold 
        //private static double PREADBREAKWARNINGMINUTES = 5;   //ideally have this number <10 as twitch is currently set to give me an ad warning at 10 minutes (gives time to sleep without warning being sent)


        static List<string> pngtuberRewardTitles = new List<string>
        {
             "Move PNG-Me", "Reset PNG-Me"
        };

        static List<string> ttsRewardTitles = new List<string>
        {
            "TTS (Normal Speech Rate)", "TTS (Random Speech Rate)"
        };

        public async static Task CheckAccessToken(int secondsRemainingCheck = 10)
        {
            //Log("Checking AccessToken...");

            var tokenResponse = GlobalObjects._TwitchAPI.Auth.ValidateAccessTokenAsync();
            ValidateAccessTokenResponse tokenResult = await tokenResponse;

            //tokenResult is null if current Access Token is invalid
            //added ExpiresIn case to allow for the code needing an access token to fully execute
            if (tokenResult == null || tokenResult.ExpiresIn <= secondsRemainingCheck)
            {
                WPFUtility.WriteToLog("CheckAccessToken: Bad token, refreshing...");

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

        public static async void TogglePngTuberManipulationRedeems(bool isEnabled)
        {
            GetCustomRewardsResponse manageableRewards = await GlobalObjects._TwitchAPI.Helix.ChannelPoints.GetCustomRewardAsync(GlobalObjects.TwitchBroadcasterUserId, onlyManageableRewards: true);
            
            foreach(CustomReward reward in manageableRewards.Data)
            {

                if (pngtuberRewardTitles.Contains(reward.Title))
                {
                    UpdateCustomRewardRequest updateRewardRequest = new UpdateCustomRewardRequest();

                    if (isEnabled)
                        updateRewardRequest.IsPaused = false;
                    else
                        updateRewardRequest.IsPaused = true;

                    await GlobalObjects._TwitchAPI.Helix.ChannelPoints.UpdateCustomRewardAsync(GlobalObjects.TwitchBroadcasterUserId, reward.Id, updateRewardRequest);
                }
            }
        }

        //toggles twitch points redeems that require obs to work
        public static async void ToggleOtherOBSbasedRedeems(bool isEnabled)
        {
            GetCustomRewardsResponse manageableRewards = await GlobalObjects._TwitchAPI.Helix.ChannelPoints.GetCustomRewardAsync(GlobalObjects.TwitchBroadcasterUserId, onlyManageableRewards: true);

            foreach (CustomReward reward in manageableRewards.Data)
            {

                //if more obs-based redeems are added in future, add lists of the reward titles here
                if (ttsRewardTitles.Contains(reward.Title))
                {
                    UpdateCustomRewardRequest updateRewardRequest = new UpdateCustomRewardRequest();

                    if (isEnabled)
                        updateRewardRequest.IsPaused = false;
                    else
                        updateRewardRequest.IsPaused = true;

                    await GlobalObjects._TwitchAPI.Helix.ChannelPoints.UpdateCustomRewardAsync(GlobalObjects.TwitchBroadcasterUserId, reward.Id, updateRewardRequest);
                }
            }
        }

        async static public void OnCommercial_NewThread(ChannelAdBreakBeginArgs e)
        {
            //check if timer is initialized and is still running when commercial triggers
            //stop timer while this is running. no need to check time to ad break when already in one
            if (adBreakWarningTimer != null && adBreakWarningTimer.Enabled)
            {
                adBreakWarningTimer.Stop();
            }

            int commercialBreakLength = e.Payload.Event.DurationSeconds;
            int threadSleepLength = commercialBreakLength * 1000;

            WPFUtility.WriteToLog("EventSub OnCommercial: Ads started for " + commercialBreakLength + " seconds");

            //checks api token and ensures it will be valid for total ad length and have enough buffer to send "ad done" message
            await CheckAccessToken(threadSleepLength + 5);

            string commercialBreakMessage = "";
            if (commercialBreakLength >= 60)
            {
                double commercialBreakLengthMin = (double)commercialBreakLength / 60;
                commercialBreakMessage = $"A {commercialBreakLengthMin} minute ad break will begin shortly... Feel free to stretch a bit, hydrate, or just chill out in chat!";
            }
            else
                commercialBreakMessage = $"A {commercialBreakLength} second ad break will begin shortly... Feel free to stretch a bit, hydrate, or just chill out in chat!";

            TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, commercialBreakMessage, sendMessageAsChatBot: true);

            Thread.Sleep(threadSleepLength);
            WPFUtility.WriteToLog("EventSub OnCommercial: Ads have finished");
            //TwitchUtility.SendChatMessage(
            //    GlobalObjects._TwitchAPIBotAccount, 
            //    GlobalObjects.TwitchMessageBotUserId, 
            //    GlobalObjects.TwitchBroadcasterUserId, 
            //    "Ad break is now done!", 
            //    sendMessageAsChatBot: true);

            try
            {
                //start a timer to check for an ad break every x minutes
                //use a larger value to have less timer checks (meaning less API calls to twitch)
                adBreakWarningTimer = new System.Timers.Timer(5 * 60000);
                adBreakWarningTimer.AutoReset = true;  //makes it so timer continually triggers and restarts (used so bot can axtively check how far it is from the next ad break)
                adBreakWarningTimer.Elapsed += AdBreakWarning;
                adBreakWarningTimer.Start();
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog($"Error initializing ad warning timer: {except.Message}");
            }
        }

        public static async void InitializeAdBreakWarningTimer()
        {
            WPFUtility.WriteToLog($"Initializing Ad Break Warning timer...");

            try
            {
                //start a timer to check for an ad break every x minutes
                //use a larger value to have less timer checks (meaning less API calls to twitch)
                adBreakWarningTimer = new System.Timers.Timer(ADBREAKTIMERINTERVALMINUTES * 60);

                adBreakWarningTimer.AutoReset = true;  //makes it so timer continually triggers and restarts (used so bot can axtively check how far it is from the next ad break)
                adBreakWarningTimer.Elapsed += AdBreakWarning;
                adBreakWarningTimer.Start();
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog($"Error initializing ad warning timer: {except.Message}");
            }
        }

        //handles sending a warning message to chat about ads in x minutes
        static async void AdBreakWarning(Object? source, System.Timers.ElapsedEventArgs e)
        {
            Trace.WriteLine(DateTime.Now.ToString() + $"\tAdBreakWarning timer elapsed. Checking if time to next ad break is under <{Properties.Settings.Default.WarningMinutesBeforeTwitchAdBreak} minutes");

            //get current utc time before polling twitch for next ad in schedule to help ensure difference between the 2 is as accurate as possible
            DateTimeOffset currUtcDateTime = DateTimeOffset.UtcNow;

            GetAdScheduleResponse adScheduleResp;
            try
            {
                //have to check for when the next ad is scheduled to see if the ad break has been slept by 5 minutes since last check
                adScheduleResp = await GlobalObjects._TwitchAPI.Helix.Channels.GetAdScheduleAsync(GlobalObjects.TwitchBroadcasterUserId);

                //assuming that the GetAdScheduleResp.Data array will only ever be 1 element long (containing the most recent information)
                //otherwise would have to work through which element is the most recent information

                //checks to see if response object retreived null/empty data or retreived default values in the case of an offline channel
                if (adScheduleResp == null)
                    throw new Exception("GetAdScheduleResponse was null");
                if (adScheduleResp.Data == null)
                    throw new Exception("GetAdSchedule.Data was null");
                if (adScheduleResp.Data.Length == 0)
                    throw new Exception("GetAdSchedule.Data.Length was 0");
                if (string.IsNullOrEmpty(adScheduleResp.Data[0].NextAdAt))
                    throw new Exception("GetAdSchedule.Data[0].NextAdAt was null or empty. (Check if stream you are asking the schedule for is offline)");
            }
            catch(Exception except)
            {
                WPFUtility.WriteToLog($"AdBreakWarning() Error retreiving ad schedule: {except.Message}");

                return;
            }


            long nextAdBreakSeconds;

            //from this point on, retreived "NextAdAt" value is a set value
            //check if value retreived can be immediately parsed into a DateTimeOffset object
            if (DateTimeOffset.TryParse(adScheduleResp.Data[0].NextAdAt, out DateTimeOffset nextAdDateTime))
            {
                //An exception of type 'System.FormatException' occurred in System.Private.CoreLib.dll but was not handled in user code
                //String '1792633301' was not recognized as a valid DateTime.

                TimeSpan timeToNextAdBreak = nextAdDateTime - currUtcDateTime;

                //forcing round down as opposed to normal rounding as if the next ad break time is very close to the set warning threshold then being slightly under that is better
                //as opposed to being very slightly above and forcing another round of checks
                nextAdBreakSeconds = (long)Math.Floor(timeToNextAdBreak.TotalSeconds);
            }
            //value retreived from NextAdAt cannot be parsed into a DateTime object. resorting to test if value retreived is milliseconds since unix epoch instead
            else
            {
                //check if retreived value is millisenconds since epoch instead of a DateTime-parsable date string
                if (long.TryParse(adScheduleResp.Data[0].NextAdAt, out long parsedNextDateAtSeconds))
                {
                    nextAdBreakSeconds = parsedNextDateAtSeconds - currUtcDateTime.ToUnixTimeSeconds();
                }
                //unknown value retreived for nextAdAt. stop timer
                else
                {
                    WPFUtility.WriteToLog($"AdBreakWarning: Error parsing time of next ad in schedule");

                    return;
                }
            }

            WPFUtility.WriteToLog($"TESTING-- " +
                $"nextAdBreakSeconds: {nextAdBreakSeconds}\t" +
                $"nextDateSeconds:{adScheduleResp.Data[0].NextAdAt.ToString()}\t" +
                $"currTime(toUnixSeconds):{currUtcDateTime.ToUnixTimeSeconds()}");
            WPFUtility.WriteToLog($"TESTING 2-- nextAdSeconds: {nextAdBreakSeconds}\tnextAdBreakMinutes: {nextAdBreakSeconds / 60.0}\tnextAdBreakHours: {nextAdBreakSeconds / 3600.0}");

            try
            {
                if(nextAdBreakSeconds <= 0)
                {
                    throw new Exception("Time to next ad break was calculated to be <=0");
                }


                //check if scheduled time until next ad break is x minutes in the future
                if (nextAdBreakSeconds <= Properties.Settings.Default.WarningMinutesBeforeTwitchAdBreak * 60)
                {
                    WPFUtility.WriteToLog($"AdBreakWarning trigger threshold reached. Stopping timer until next ad break.");

                    adBreakWarningTimer.Stop();

                    //get a rounded number to display specifically how many minutes are left until ads (useful if the timer is slow and doesn't trigger right at the warning threshold)
                    //added d to 60 to avoid an ambiguous rounding call between decimal and double. 'd' implies 60 is a decimal
                    int roundedMinutesToAdBreak = (int)Math.Round(nextAdBreakSeconds / 60d);

                    string adWarningMessage = $"Heads up, there is a scheduled ad break in roughly {roundedMinutesToAdBreak} minutes";

                    TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, adWarningMessage, sendMessageAsChatBot: true);
                }
            }
            catch(Exception except)
            {
                WPFUtility.WriteToLog($"AdBreakWarning act upon time delta Error: {except.Message}");
            }
        }

        public static void StopAdWarningTimer()
        {
            Trace.WriteLine(DateTime.Now.ToString() + ($"Stopping ad warning timer..."));

            if(adBreakWarningTimer != null)
            {
                adBreakWarningTimer.Stop();
                adBreakWarningTimer.Dispose();
            }
        }


        static async public void ReinstateModRole(TwitchAPI _TwitchAPI, string TwitchChannelId, string userIdToMod, string username, int banLength)
        {
            Thread.Sleep(banLength * 1000); //wait for user's timeout to finish (converting from seconds to miliseconds)

            await TwitchUtility.CheckAccessToken();

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

            try
            {
                SendChatMessageResponse sendMessageResponse = await _TwitchAPI.Helix.Chat.SendChatMessage(chatMessageRequest);

                foreach (var respInfo in sendMessageResponse.Data)
                {
                    if (respInfo.IsSent)
                    {
                        System.Console.WriteLine($"TwitchUtility.SendChatMessage \tMessId:{respInfo.MessageId}\tIsSent:{respInfo.IsSent}");
                    }
                    else
                    {
                        WPFUtility.WriteToLog($"TwitchUtility.SendChatMessage MESSAGE NOT SENT \ttDropCode:{respInfo.DropReason.Code}\tDropMessage{respInfo.DropReason.Message}\nBe sure to check if the bot has mod status or not in the channel this issue occurred in. Could fix the issue.");
                    }
                }
            }
            catch(Exception except)
            {
                WPFUtility.WriteToLog($"TwitchUtility.SendChatMessage ERROR \t{except.Message}");
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

        static async public void ResetPointsRedeemCosts(List<ResetReward> rewardsToReset)
        {
            int threadSleepLength = 30 * 1000;
            Thread.Sleep(threadSleepLength);

            await TwitchUtility.CheckAccessToken();

            WPFUtility.WriteToLog($"Returning bot-owned rewards to original prices...");

            try
            {
                foreach (ResetReward reward in rewardsToReset)
                {
                    UpdateCustomRewardRequest rewardUpdate = new UpdateCustomRewardRequest();
                    rewardUpdate.Cost = reward.getCost();

                    await GlobalObjects._TwitchAPI.Helix.ChannelPoints.UpdateCustomRewardAsync(GlobalObjects.TwitchBroadcasterUserId, reward.getId(), rewardUpdate);
                }
            }
            catch(Exception except)
            {
                WPFUtility.WriteToLog($"ResetPointsRedeemCosts error: {except.Message}");
            }
        }

        static int test()
        {
            return 5;
        }

        //have to format this method as a task as otherwise I am unable to return a bool if method is also async
        static async public Task<bool> CreatePrediction(string predictionQuestion, List<string> choices, string broadcasterUserId)
        {
            try
            {
                if (string.IsNullOrEmpty(predictionQuestion))
                    throw new Exception("Nothing provided for prediction title");


                CreatePredictionRequest request = new CreatePredictionRequest();
                request.Title = predictionQuestion;
                request.PredictionWindowSeconds = 120;
                request.BroadcasterId = broadcasterUserId;


                //change between a list of strings for the choices to a list of "Outcome" type
                int totalChoices = choices.Count;
                List<Outcome> outcomes = new List<Outcome>();

                for (int x = 0; x < choices.Count; x++)
                {
                    WPFUtility.WriteToLog($"Choice: {choices[x]}");

                    Outcome newOutcome = new Outcome();
                    newOutcome.Title = choices[x];
                    outcomes.Add(newOutcome);
                }

                
                if (outcomes.Count < 2)
                    throw new Exception("Too few choices to create a prediction");
                if (outcomes.Count > 5)
                    throw new Exception("Too many choices to create a prediction");


                request.Outcomes = outcomes.ToArray();

                await GlobalObjects._TwitchAPI.Helix.Predictions.CreatePredictionAsync(request);
            }
            catch(Exception except)
            {
                WPFUtility.WriteToLog($"Error creating prediction: ");
                return false;
            }

            return true;
        }

        static async public void RaidResponse(TwitchLib.EventSub.Core.SubscriptionTypes.Channel.ChannelRaid e)
        {
            WPFUtility.WriteToLog($"User {e.FromBroadcasterUserName} just raided into user {e.ToBroadcasterUserName} with {e.Viewers} viewers");

            //have to poll Twitch API for channel info as there is no other way to be able to get the last category (game name) the user was in (assuming the user is now offline)
            GetChannelInformationResponse channelInfoResp = await GlobalObjects._TwitchAPI.Helix.Channels.GetChannelInformationAsync(new List<string> { $"{e.FromBroadcasterUserId}" });

            string raidMessage = $"Oh hey! {e.FromBroadcasterUserName} just raided in with {e.Viewers} viewers! They were last playing {channelInfoResp.Data[0].GameName}.";
            TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, raidMessage, sendMessageAsChatBot: true);

            await GlobalObjects._TwitchAPI.Helix.Chat.SendShoutoutAsync(GlobalObjects.TwitchBroadcasterUserId, e.FromBroadcasterUserId, GlobalObjects.TwitchBroadcasterUserId);
        }
    }
}
