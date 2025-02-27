using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Api.Auth;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.PubSub.Events;

namespace TwitchBot.Utility_Code
{
    class TwitchUtility
    {
        async static Task CheckAccessToken()
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
    }
}
