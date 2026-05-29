using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchBot.Utility_Code
{
    class OBSUtility
    {
        public static void CloseTTSFace(SceneItemDetails sceneItem, string sceneName)
        {
            try
            {
                SpeechSynthesis _SpeechSynth = SpeechSynthesis.GetInstance();

                //ensure TTS is running before starting to check states
                Thread.Sleep(100);

                //check every 100ms if TTS is actively speaking. if finished speaking, disable TTS face
                while (_SpeechSynth.asyncSynth.State.ToString() == "Speaking")
                {
                    Thread.Sleep(100);
                }

                if (GlobalObjects._OBS.IsConnected && sceneItem != null)
                {
                    GlobalObjects._OBS.SetSceneItemEnabled(sceneName, sceneItem.ItemId, false);
                }
            }
            catch (Exception e)
            {
                WPFUtility.WriteToLog("CloseTTSFace Error: " + e.Message);
            }
        }

        //Resets the pngtuber image in OBS back to the bottom right hand side corner
        //Returns true if succeeded and false if an error occurred
        public static bool ResetPNGTuber()
        {
            string currSceneName = GlobalObjects._OBS.GetCurrentProgramScene();
            try
            {
                List<SceneItemDetails> sceneItemList = GlobalObjects._OBS.GetSceneItemList(currSceneName);

                SceneItemDetails pngtuberSceneItem = sceneItemList.FirstOrDefault(sceneItem => sceneItem.SourceName == GlobalObjects.ObsPngTuberName);

                if (pngtuberSceneItem == null)
                    throw new Exception("Unable to find talking head either due to not being present in current scene or due to incorrectly stored name in settings");

                //if (currSceneName == "Starting Soon" || currSceneName == "BRB" || currSceneName == "Just Chatting" || currSceneName == "Ending")
                //    throw new Exception("Moving pngTuber in OBS scene \"{currSceneName}\" is currently not allowed");


                SceneItemTransformInfo transformInfo = new SceneItemTransformInfo();
                transformInfo.Rotation = 0.00;

                //need to manually set scaling factors to 1. otherwise it extremely annoyingly defaults to 0
                transformInfo.ScaleX = 1;
                transformInfo.ScaleY = 1;

                //use "OBS_BOUNDS_NONE" to set the bounds type in obs to automatic (allows for easy flipping of image among other things)
                transformInfo.BoundsType = SceneItemBoundsType.OBS_BOUNDS_NONE;

                //still need to set bounds width and height values even if using "OBS_BOUNDS_NONE"
                transformInfo.BoundsWidth = 394.00;
                transformInfo.BoundsHeight = 591.00;

                switch (currSceneName)
                {
                    case ("Just Chatting"):
                        transformInfo.Width = 591.00;
                        transformInfo.Height = 887.00;

                        transformInfo.X = 920.00;
                        transformInfo.Y = 193.00;
                        break;

                    case ("Ending"):
                        transformInfo.Width = 542.00;
                        transformInfo.Height = 813.00;

                        transformInfo.X = 1285.00;
                        transformInfo.Y = 267.00;
                        break;

                    default:
                        transformInfo.Width = 400.00;
                        transformInfo.Height = 600.00;

                        transformInfo.X = 1755.50;
                        transformInfo.Y = 784.00;
                        break;
                }

                GlobalObjects._OBS.SetSceneItemTransform(currSceneName, pngtuberSceneItem.ItemId, transformInfo);

                return true;
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog($"ResetPngTuber Error: {except.Message}");
                return false;
            }
        }

        //Method to move pngtuber image in OBS to a random location and rotation along the edge of screen
        //Returns true if everything succeeded and false if an error occurred
        public static bool MovePNGTuber()
        {
            string currSceneName = GlobalObjects._OBS.GetCurrentProgramScene();
            try
            {
                List<SceneItemDetails> sceneItemList = GlobalObjects._OBS.GetSceneItemList(currSceneName);

                SceneItemDetails pngtuberSceneItem = sceneItemList.FirstOrDefault(sceneItem => sceneItem.SourceName == GlobalObjects.ObsPngTuberName);

                if (pngtuberSceneItem == null)
                    throw new Exception("Unable to find talking head either due to not being present in current scene or due to incorrectly stored name in settings");

                if (currSceneName == "Starting Soon" || currSceneName == "BRB" || currSceneName == "Just Chatting" || currSceneName == "Ending")
                    throw new Exception("Moving pngTuber in OBS scene \"{currSceneName}\" is currently not allowed");


                Random random = new Random();

                //in the order left, right, top, bottom
                int border = random.Next(0, 4);
                double rawRotationAngle = Math.Round(random.NextDouble() * (45.00 - -45.00) + -45.00, 2);

                SceneItemTransformInfo transformInfo = new SceneItemTransformInfo();


                //---SET NON-CHANGING TRANSFORMATION VARIABLES---
                transformInfo.Width = 400;
                transformInfo.Height = 600;

                //need to manually set scaling factors to 1. otherwise it extremely annoyingly defaults to 0
                transformInfo.ScaleX = -1;
                transformInfo.ScaleY = 1;

                //use "OBS_BOUNDS_NONE" to set the bounds type in obs to automatic (allows for easy flipping of image among other things)
                transformInfo.BoundsType = SceneItemBoundsType.OBS_BOUNDS_NONE;

                //still need to set bounds width and height values even if using "OBS_BOUNDS_NONE"
                transformInfo.BoundsWidth = 394.00;
                transformInfo.BoundsHeight = 591.00;


                ObsVideoSettings videoSettings = GlobalObjects._OBS.GetVideoSettings();



                //---SET LOCATION ON RANDOMLY CHOSEN CANVAS EDGE---

                double randObsCanvasLocation = 0;

                //pngtuber image will be on the left or right edge of the obs canvas
                if (border == 0 || border == 1)
                {
                    double max = videoSettings.BaseHeight;
                    double min = 0.00;
                    randObsCanvasLocation = Math.Round(random.NextDouble() * (max - min) + min, 2);

                    transformInfo.Y = randObsCanvasLocation;
                }
                //pngtuber image will be on the top or bottom edge of the obs canvas
                else
                {
                    double max = videoSettings.BaseWidth;
                    double min = 0.00;
                    randObsCanvasLocation = Math.Round(random.NextDouble() * (max - min) + min, 2);

                    transformInfo.X = randObsCanvasLocation;
                }


                //---SET ROTATION AND FLIP IMAGE TO FACE CENTER---

                switch (border)
                {
                    //left edge
                    case 0:
                        transformInfo.X = Math.Round(random.NextDouble() * (300 - 0) + 0, 2);
                        transformInfo.Rotation = rawRotationAngle + 90.00;

                        if (transformInfo.Y < videoSettings.OutputHeight / 2)
                            transformInfo.ScaleX = -1;
                        else
                            transformInfo.ScaleX = 1;

                        break;

                    //right edge
                    case 1:
                        transformInfo.X = Math.Round(random.NextDouble() * (videoSettings.BaseWidth - (videoSettings.BaseWidth - 300)) + (videoSettings.BaseWidth - 300), 2);
                        transformInfo.Rotation = rawRotationAngle + 270.00;

                        if (transformInfo.Y < videoSettings.OutputHeight / 2)
                            transformInfo.ScaleX = 1;
                        else
                            transformInfo.ScaleX = -1;
                        break;

                    //top edge
                    case 2:
                        transformInfo.Y = Math.Round(random.NextDouble() * (300 - 0) + 0, 2);
                        transformInfo.Rotation = rawRotationAngle + 180.00;
                        
                        if (transformInfo.X < videoSettings.OutputWidth / 2)
                            transformInfo.ScaleX = 1;
                        else
                            transformInfo.ScaleX = -1;
                        break;

                    //bottom edge
                    case 3:
                        transformInfo.Y = Math.Round(random.NextDouble() * (videoSettings.BaseHeight - (videoSettings.BaseHeight - 300)) + (videoSettings.BaseHeight - 300), 2);
                        transformInfo.Rotation = rawRotationAngle;

                        if (transformInfo.X < videoSettings.OutputWidth / 2)
                            transformInfo.ScaleX = -1;
                        else
                            transformInfo.ScaleX = 1;
                        break;
                }


                //send final commands to obs
                GlobalObjects._OBS.SetSceneItemTransform(currSceneName, pngtuberSceneItem.ItemId, transformInfo);

                return true;
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog($"ResetPngTuber Error: {except.Message}");
                return false;
            }
        }
        
    }
}
