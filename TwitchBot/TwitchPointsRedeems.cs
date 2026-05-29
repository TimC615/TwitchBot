using Newtonsoft.Json;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchBot.Utility_Code;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.Api.Helix.Models.Channels.GetChannelInformation;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.PubSub.Events;

namespace TwitchBot
{
    internal class TwitchPointsRedeems
    {

        //
        //----------------------PubSub Event Hookups----------------------
        //
        /*
        private void PubSub_OnPubSubServiceConnected(object sender, EventArgs e)
        {
            PubSub.SendTopics(CachedOwnerOfChannelAccessToken);
        }
        */

        /*
        private void PubSub_OnListenResponse(object sender, OnListenResponseArgs e)
        {
            if (!e.Successful)
            {
                Log($"Failed to listen! Response: {e.Topic} + {e.Response.Error}");
                throw new Exception($"Failed to listen! Response: {e.Topic} + {e.Response.Error}");
            }
            else
            {
                Log("PubSub Listen: " + e.Topic);
                Log("PubSub Connected");
            }
        }
        */

        /*
        private void PubSub_OnLog(object sender, TwitchLib.PubSub.Events.OnLogArgs e)
        {
            Log("PubSub Log: " + e.Data);
        }
        */

        /*
        private void PubSub_OnServiceError(object sender, OnPubSubServiceErrorArgs e)
        {
            Log($"PubSub Service Error: {e.Exception.Message} {e.Exception}");
        }
        */

        //
        //----------------------End of PubSub Event Hookups----------------------
        //

        public static void OnChannelPointsRewardRedeemed(ChannelPointsCustomRewardRedemptionArgs e)
        {
            //Log("PubSub: " + e.RewardRedeemed.Redemption.Reward.Title);
            var pointsRedemption = e.Payload.Event;
            string redeemTitle = pointsRedemption.Reward.Title.ToLower();
            WPFUtility.WriteToLog("Points Reward: " + redeemTitle);

            switch (redeemTitle)
            {
                case "toggle cake face":
                    ToggleCakeFace(pointsRedemption);
                    break;

                case "tts (random speech rate)":
                    Random random = new Random();
                    int randRate = random.Next(1, 21) - 10;

                    TtsRedeem(pointsRedemption, randRate);
                    break;

                case "tts (normal speech rate)":
                    TtsRedeem(pointsRedemption);
                    break;

                case "1st":
                    FirstRedeem(pointsRedemption);
                    break;

                case "move png-me":
                    MovePngMe(pointsRedemption);
                    break;

                case "reset png-me":
                    ResetPngMe(pointsRedemption);
                    break;
            }
        }

        private static void ToggleCakeFace(ChannelPointsCustomRewardRedemption redemption)
        {
            UpdateCustomRewardRedemptionStatusRequest rewardRedemptionStatus = new UpdateCustomRewardRedemptionStatusRequest();

            //if (obsConnected)
            if (GlobalObjects._OBS != null && GlobalObjects._OBS.IsConnected)
            {
                //check if webcam and reactive image scene items exist
                string currSceneName = GlobalObjects._OBS.GetCurrentProgramScene();

                List<OBSWebsocketDotNet.Types.SceneItemDetails> currScenesList = GlobalObjects._OBS.GetSceneItemList(currSceneName);
                List<string> currSceneItemIDs = new List<string>();

                //probably a far more elegant way to do a .contains for specific parameter values but this makes sense and i can do it
                foreach (OBSWebsocketDotNet.Types.SceneItemDetails sceneItem in currScenesList)
                    currSceneItemIDs.Add(sceneItem.SourceName);

                if (currSceneItemIDs.Contains(Properties.Settings.Default.OBSWebcamSceneID) && currSceneItemIDs.Contains(Properties.Settings.Default.OBSReactiveImageSceneID))
                {
                    try
                    {
                        //read in visibility of webcam
                        //if false, tell obs to set to true
                        //else, set to false
                        int webcamItemID = GlobalObjects._OBS.GetSceneItemId(currSceneName, Properties.Settings.Default.OBSWebcamSceneID, 0);
                        bool webcamEnabled = GlobalObjects._OBS.GetSceneItemEnabled(currSceneName, webcamItemID);

                        int reactiveImageID = GlobalObjects._OBS.GetSceneItemId(currSceneName, "Reactive Images - Myself", 0);
                        bool reactiveImageEnabled = GlobalObjects._OBS.GetSceneItemEnabled(currSceneName, reactiveImageID);

                        if (!webcamEnabled)
                        {
                            GlobalObjects._OBS.SetSceneItemEnabled(currSceneName, webcamItemID, true);
                            GlobalObjects._OBS.SetSceneItemEnabled(currSceneName, reactiveImageID, false);
                            WPFUtility.WriteToLog("Toggle Webcam: Webcam enabled");
                        }
                        else
                        {
                            GlobalObjects._OBS.SetSceneItemEnabled(currSceneName, webcamItemID, false);
                            GlobalObjects._OBS.SetSceneItemEnabled(currSceneName, reactiveImageID, true);
                            WPFUtility.WriteToLog("Toggle Webcam: Webcam disabled");
                        }

                        rewardRedemptionStatus.Status = TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.FULFILLED;
                    }
                    catch (Exception except)
                    {
                        WPFUtility.WriteToLog("Toggle webcam: " + except.Message);
                        rewardRedemptionStatus.Status = TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.CANCELED;
                    }
                }
                else
                    WPFUtility.WriteToLog("Toggle webcam: The OBS webcam and/or reactive image names saved in settings don't match up to items in the current OBS scene. Please check to make sure you entered the right names.");
                rewardRedemptionStatus.Status = TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.CANCELED;
            }
            else
            {
                WPFUtility.WriteToLog("Toggle webcam: Couldn't run due to OBS not running or not being able to connect to the OBS websocket");
                rewardRedemptionStatus.Status = TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.CANCELED;
            }

            TwitchUtility.UpdateCustomPointRedemptionStatus(
                redemption.BroadcasterUserId,
                redemption.Reward.Id,
                new List<string> { redemption.Id },
                rewardRedemptionStatus);
        }

        private static void FirstRedeem(ChannelPointsCustomRewardRedemption redemption)
        {
            //Dictionary<string, int> firstRedeemLeaderboard = TwitchChatCommands.GetFirstLeaderboardFromJson();
            Dictionary<string, int>? firstRedeemLeaderboard = TwitchChatCommands.GetLeaderboardFromJson(TwitchChatCommands.FIRSTREDEEMSJSONFILENAME);

            if (firstRedeemLeaderboard == null)
            {
                WPFUtility.WriteToLog($"First Redeem: Leaderboard set to null. Ending method to avoid saving incorrect data over actual file.");
                return;
            }

            //update leaderboard 
            if (firstRedeemLeaderboard.ContainsKey(redemption.UserId))
                firstRedeemLeaderboard[redemption.UserId]++;
            else
                firstRedeemLeaderboard.Add(redemption.UserId, 1);


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

                System.IO.File.WriteAllText(TwitchChatCommands.FIRSTREDEEMSJSONFILENAME, firstRedeemJson);
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog($"rouletteLeaderboard error while saving to file - {except.Message}");
                return;
            }
        }

        private static void MovePngMe(ChannelPointsCustomRewardRedemption redemption)
        {
            UpdateCustomRewardRedemptionStatusRequest rewardRedemptionStatus = new UpdateCustomRewardRedemptionStatusRequest();

            if(GlobalObjects._OBS != null && GlobalObjects._OBS.IsConnected)
            {
                bool movePNGSuccess = OBSUtility.MovePNGTuber();

                //use this for automatically redeeming someone's points if action doesn't complete successfully
                if (movePNGSuccess)
                    rewardRedemptionStatus.Status = TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.FULFILLED;
                else
                    rewardRedemptionStatus.Status = TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.CANCELED;
            }
            else
            {
                WPFUtility.WriteToLog($"Error trying to connect to OBS for Move PNG redeem");
                rewardRedemptionStatus.Status = TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.CANCELED;
            }

            TwitchUtility.UpdateCustomPointRedemptionStatus(
                redemption.BroadcasterUserId,
                redemption.Reward.Id,
                new List<string> { redemption.Id },
                rewardRedemptionStatus);
        }

        private static void ResetPngMe(ChannelPointsCustomRewardRedemption redemption)
        {
            UpdateCustomRewardRedemptionStatusRequest rewardRedemptionStatus = new UpdateCustomRewardRedemptionStatusRequest();

            if (GlobalObjects._OBS != null && GlobalObjects._OBS.IsConnected)
            {
                bool resetPNGSuccess = OBSUtility.ResetPNGTuber();

                //use this for automatically redeeming someone's points if action doesn't complete successfully
                if (resetPNGSuccess)
                    rewardRedemptionStatus.Status = TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.FULFILLED;
                else
                    rewardRedemptionStatus.Status = TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.CANCELED;
            }
            else
            {
                WPFUtility.WriteToLog($"Error trying to connect to OBS for Move PNG redeem");
                rewardRedemptionStatus.Status = TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.CANCELED;
            }

            TwitchUtility.UpdateCustomPointRedemptionStatus(
                redemption.BroadcasterUserId,
                redemption.Reward.Id,
                new List<string> { redemption.Id },
                rewardRedemptionStatus);
        }

        async static private void TtsRedeem(ChannelPointsCustomRewardRedemption e, int speechRate = -100)
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
                        await TwitchUtility.CheckAccessToken();

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
                _SpeechSynth.SpeechSynthAsync(e.UserInput, speechRate);
            }
        }
    }
}
