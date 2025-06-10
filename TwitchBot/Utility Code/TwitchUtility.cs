using Newtonsoft.Json;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.PubSub.Events;

namespace TwitchBot.Utility_Code
{
    class TwitchUtility
    {
        private static readonly string FIRSTREDEEMSJSONFILENAME = @"firstredeemsleaderboard.json";


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

        async static public void TtsRedeem(ChannelPointsCustomRewardRedemption e, int speechRate)
        {
            SpeechSynthesis _SpeechSynth = SpeechSynthesis.GetInstance();

            if (GlobalObjects._OBS.IsConnected)
            {
                string ttsSceneName = GlobalObjects._OBS.GetCurrentProgramScene();

                List<SceneItemDetails> sceneItemList = GlobalObjects._OBS.GetSceneItemList(ttsSceneName);

                SceneItemDetails ttsSceneItem = sceneItemList.FirstOrDefault(sceneItem => sceneItem.SourceName == GlobalObjects.ObsTtsTalkingHeadName);

                if (ttsSceneName != null && ttsSceneItem != null)
                {
                    try
                    {
                        await CheckAccessToken();

                        //Log("TTS Talking Head Source Found");

                        //get id and login of user, send request to Twitch API to get profile image url, and set obs browser source to url
                        List<string> idSearch = new List<string>();
                        //idSearch.Add(e.RewardRedeemed.Redemption.User.Id);
                        idSearch.Add(e.UserId);
                        List<string> userSearch = new List<string>();
                        //userSearch.Add(e.RewardRedeemed.Redemption.User.Login);
                        userSearch.Add(e.UserLogin);

                        var users = GlobalObjects._TwitchAPI.Helix.Users.GetUsersAsync(idSearch, userSearch);
                        string profileImageUrl = users.Result.Users[0].ProfileImageUrl;

                        InputSettings testInSet = GlobalObjects._OBS.GetInputSettings("TTS Talking Head");

                        testInSet.Settings["url"] = profileImageUrl;

                        GlobalObjects._OBS.SetInputSettings(testInSet);

                        GlobalObjects._OBS.SetSceneItemEnabled(ttsSceneName, ttsSceneItem.ItemId, true);


                        //_SpeechSynth.SpeechSynthAsync(e.RewardRedeemed.Redemption.UserInput, speechRate);
                        _SpeechSynth.SpeechSynthAsync(e.UserInput, speechRate);

                        OBSUtility.CloseTTSFace(ttsSceneItem, ttsSceneName);
                    }
                    catch (Exception except)
                    {
                        WPFUtility.WriteToLog($"TtsRedeem Error: {except.Message}");

                        _SpeechSynth.SpeechSynthAsync(e.UserInput, speechRate);
                    }
                }
                else
                {
                    WPFUtility.WriteToLog("TTS Talking Head Source not found in current OBS scene");

                    _SpeechSynth.SpeechSynthAsync(e.UserInput, speechRate);
                }
            }
            else
            {
                //trigger TTS without calling obs-related methods
                if (speechRate == -100)
                    _SpeechSynth.SpeechSynthAsync(e.UserInput);
                else
                    _SpeechSynth.SpeechSynthAsync(e.UserInput, speechRate);
            }


        }

        async static public void OnCommercial_NewThread(ChannelAdBreakBeginArgs e)
        {
            int commercialBreakLength = e.Notification.Payload.Event.DurationSeconds;
            int threadSleepLength = commercialBreakLength * 1000;

            WPFUtility.WriteToLog("EventSub: Ads started for " + commercialBreakLength + " seconds");

            await MainWindow.AppWindow.CheckAccessToken();

            if (commercialBreakLength >= 60)
            {
                double commercialBreakLengthMin = (double)commercialBreakLength / 60;
                GlobalObjects._TwitchClient.SendMessage(GlobalObjects.TwitchChannelName, "Ads have started and will last for " + commercialBreakLengthMin
                    + " minutes. Feel free to stretch a bit, hydrate, or just chill out in chat!");
            }
            else
            {
                GlobalObjects._TwitchClient.SendMessage(GlobalObjects.TwitchChannelName, "Ads have started and will last for " + commercialBreakLength
                    + " seconds. Feel free to stretch a bit, hydrate, or just chill out in chat!");
            }

            Thread.Sleep(threadSleepLength);
            WPFUtility.WriteToLog("PubSub_OnCommercial: Ads have finished");
            //GlobalObjects._TwitchClient.SendMessage(GlobalObjects.TwitchChannelName, "Ad break is now done!");
        }

        static public void FirstRedeem(ChannelPointsCustomRewardRedemption e)
        {
            Dictionary<string, int> firstRedeemLeaderboard;
            try
            {
                string firstRedeemJsonInput = File.ReadAllText(FIRSTREDEEMSJSONFILENAME);

                var deserializedLeaderboard = JsonConvert.DeserializeObject<Dictionary<string, int>>(firstRedeemJsonInput);
                if (deserializedLeaderboard == null)
                    firstRedeemLeaderboard =  new Dictionary<string, int>();
                else
                    firstRedeemLeaderboard =  deserializedLeaderboard;
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog($"Read from first redeem leaderboard JSON error: {except.Message}");
                return;
            }

            //update leaderboard 
            if (firstRedeemLeaderboard.ContainsKey(e.UserName))
                firstRedeemLeaderboard[e.UserName]++;
            else
                firstRedeemLeaderboard.Add(e.UserName, 1);


            //check in case no update occurs
            if (firstRedeemLeaderboard == null)
            {
                WPFUtility.WriteToLog($"firstRedeemLeaderboard was null when trying to save to file");
                return;
            }

            //save to .json file
            try
            {
                var firstRedeemJson = JsonConvert.SerializeObject(firstRedeemLeaderboard);

                System.IO.File.WriteAllText(FIRSTREDEEMSJSONFILENAME, firstRedeemJson);
            }
            catch(Exception except)
            {
                WPFUtility.WriteToLog($"rouletteLeaderboard error while saving to file - {except.Message}");
                return;
            }
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
    }
}
