using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchBot.Utility_Code;
using TwitchBot;
using TwitchLib.Api.Helix.Models.Moderation.BanUser;
using TwitchLib.Client.Events;
using TwitchLib.Client;
using System.Net.Http;
using System.IO;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Channels.SendChatMessage;

namespace TwitchBot
{
    public class TwitchChatCommands
    {
        TwitchAPI _TwitchAPI;

        SpeechSynthesis _SpeechSynth;
        HttpClient NinjaAPIConnection;

        private static readonly int TIMEOUTROULETTELENGTH = 30;      //timeout length, in seconds
        private static readonly int TIMEOUTROULETTETOPPOSITIONSTODISPLAY = 3;   //used to determine max number of results to show for leaderboard display
        private static readonly int MAXTIMEOUTTIMEALLOWED = 1209600;    //maximum time allowed to time someone out through the Twitch API

        private static readonly string FIRSTREDEEMSJSONFILENAME = @"firstredeemsleaderboard.json";
        private readonly string ROULETTEJSONFILENAME = @"rouletteleaderboard.json";

        Dictionary<string, string> CommandsStaticResponses = new Dictionary<string, string>
        {
            { "about", "Hello! I'm Cake and I'm a Canadian variety streamer. We play a bunch of stuff over here in this small corner of the internet. Come pop a seat and have fun watching the shenanigans!"},
            { "discord", "Join the discord server at: https://discord.gg/uzHqnxKKkC"},
            { "twitter", "Follow me on Twitter at: https://twitter.com/TheCakeIsAPi"},
            { "lurk", "Have fun lurking!"}
        };

        public TwitchChatCommands(TwitchAPI _twitchAPI, SpeechSynthesis _SpeechSynth, HttpClient NinjaAPIConnection)
        {
            this._TwitchAPI = _twitchAPI;

            this._SpeechSynth = _SpeechSynth;
            this.NinjaAPIConnection = NinjaAPIConnection;
        }

        async public void BaseCommandMethod(TwitchLib.EventSub.Core.SubscriptionTypes.Channel.ChannelChatMessage e)
        {
            //contains an array of the user's whole message. when compared to _TwitchClient implementation, array[0] is the command itself and all following array indexes are arguments
            string[] messageInputs = e.Message.Text.ToLower().Split(' ');
            string cleanedCommandName = messageInputs[0].Remove(0, 1);  //removes the leading '!' from the first element of the array


            //2 ways to deal with commands: if/switch statements OR dictionary lookups

            //responses are added to dictionary in lowercase
            if (CommandsStaticResponses.TryGetValue(cleanedCommandName, out string? value))
            {
                TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, value, e.MessageId, true);
            }
            //more complex comands
            else
            {
                //return list of current bot commands (added different command to avoid also showing commands for other Twitch bots)
                if(cleanedCommandName.Equals("commands") || cleanedCommandName.Equals("botmenu"))
                {
                    string helpMessage = "The current chat commands are: help, about, discord, twitter, lurk, joke, fact, roll, roulette, rouletteleaderboard, and 1st";
                    TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, helpMessage, e.MessageId, true);
                }

                //Tells user how to use commands
                if (cleanedCommandName.Equals("help"))
                    HelpCommands(messageInputs, e.MessageId);

                //roll a random dX sided die
                //command text only looks for the first word after the ! so it automatically ignores additional chars after command
                if (cleanedCommandName.Equals("roll"))
                {
                    if(messageInputs.Length >= 2)
                    {
                        RollCommand(messageInputs, e.MessageId);
                    }
                    else
                    {
                        string inputErrorMessage = "Make sure you add in the number of dice to roll and the number of sides on a die (e.g. \"!roll 2d6\")";
                        TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, inputErrorMessage, e.MessageId, true);
                    }
                }

                //Grab random fact from https://api-ninjas.com/
                if (cleanedCommandName.Equals("fact"))
                {
                    if(NinjaAPIConnection == null)
                    {
                        NinjaAPIConnection = new HttpClient();
                        NinjaAPIConnection.BaseAddress = new Uri("https://api.api-ninjas.com/v1/");
                        //requires an API key to be added to requests but will be handled during actual GET requests
                    }

                    new Thread(APINinjaGetFact).Start();
                }

                //Grab random dad joke from https://api-ninjas.com/
                if (cleanedCommandName.Equals("joke"))
                {
                    if (NinjaAPIConnection == null)
                    {
                        NinjaAPIConnection = new HttpClient();
                        NinjaAPIConnection.BaseAddress = new Uri("https://api.api-ninjas.com/v1/");
                        //requires an API key to be added to requests but will be handled during actual GET requests
                    }

                    new Thread(APINinjaGetDadJoke).Start();
                }

                //Random chance for user to time themself out. If user has roles, automatically re-apply once timeout is done
                if (cleanedCommandName.Equals("roulette"))
                {
                    
                    //Spin the wheel x times (default is once)
                    if (messageInputs.Length == 1)
                        RouletteCommand(e);
                    else
                    {
                        try
                        {
                            int totalSpins = Int32.Parse(messageInputs[1]);

                            if (totalSpins < 1)
                                RouletteCommand(e, 1);
                            else
                                RouletteCommand(e, totalSpins);
                        }
                        catch(Exception)
                        {
                            WPFUtility.WriteToLog($"Exception parsing number for custom roulette spins. Defaulting to 1.");
                            RouletteCommand(e);
                        }
                    }
                }

                //Displays a list of the chatters with the most timeout roulette spins without a timeout
                if (cleanedCommandName.Equals("rouletteleaderboard"))
                {
                    RouletteLeaderboardCommand(e.MessageId);
                }


                if (cleanedCommandName.Equals("first") || cleanedCommandName.Equals("1st"))
                {
                    FirstCommand(e.ChatterUserId, e.ChatterUserName, e.MessageId);
                }

                //Tell user what skyrim spawn commands are avaialble (only when Twitch Plays is active)
                if (cleanedCommandName.Equals("skyrim") && GlobalObjects.twitchPlaysActive)
                {
                    string skyrimCommands = "You can mess with skyrim by saying any of the following: forward, back, stop, left, right, jump, " +
                        "cheese, soup, wine, potions, rabbits, skeevers, bears, lydia, spiders, dragons, cheesemageddon, and soupmageddon";

                    TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, skyrimCommands, e.MessageId, true);
                }
            }
        }

        void HelpCommands(string[] messageInput, string parentMessageId)
        {
            //checks if user is asking for help on a specific command or the help command itself
            if (messageInput.Length == 1)
            {
                string defaultHelpMessage = "Type \"!help <command>\" to see how you can use it (E.g. !help roll)";
                TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, defaultHelpMessage, parentMessageId, true);
            }
            else
            {
                try
                {
                    string helpSpecifier = messageInput[1];

                    string? helpMessage = null;
                    switch (helpSpecifier)
                    {
                        case "about":
                        case "discord":
                        case "twitter":
                        case "lurk":
                        case "fact":
                        case "joke":
                        case "1st":
                        case "first":
                            helpMessage = "Enter \"!" + helpSpecifier + "\" and I'll do all the rest";
                            break;
                        case "roll":
                            helpMessage = "Enter \"!roll d<number of sides>\" to roll a single die or \"!roll <number of dice>d<number of sides>\" to roll multiple dice! (e.g. !roll d6) or !roll 3d20";
                            break;
                        case "roulette":
                            helpMessage = "Enter \"!roulette\" for a chance to time yourself out for " + TIMEOUTROULETTELENGTH + " seconds or add a number afterwards to roll multiple times in a row!";
                            break;
                        case "rouletteleaderboard":
                            helpMessage = "Enter \"!rouletteleaderboard\" to see who has the highest active streaks on the timeout roulette wheel!";
                            break;
                        default:
                            break;
                    }

                    //returns a message only if user is asking for help with an actual command
                    if (!String.IsNullOrEmpty(helpMessage))
                    {
                        TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, helpMessage, parentMessageId, true);
                    }
                }
                catch (Exception except)
                {
                    WPFUtility.WriteToLog("Help command: " + except.Message);
                }
            }
        }

        void RouletteCommand(TwitchLib.EventSub.Core.SubscriptionTypes.Channel.ChannelChatMessage e, int totalSpins = 1)
        {
            try
            {
                //Log($"Roulette triggered by {e.ChatMessage.DisplayName}");

                if (e.IsBroadcaster || e.IsStaff)
                {
                    string rouletteInputError = $"Sorry {e.ChatterUserName}, you're not able to be timed out so you can't spin the roulette";
                    TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, rouletteInputError, e.MessageId, true);

                    throw new Exception("User tried to timeout as restricted role");
                }

                Random random = new Random();
                for (int spin = 1; spin <= totalSpins; spin++)
                {
                    if (random.Next(1, 11) == 1)
                    {
                        int totalTimeoutLength;

                        //checks if mathematical timeout for user is higher than max allowed timeout by twitch's api
                        if (totalSpins * TIMEOUTROULETTELENGTH > MAXTIMEOUTTIMEALLOWED)
                            totalTimeoutLength = MAXTIMEOUTTIMEALLOWED;
                        //checks if total timeout length is negative, if so then timeout for 1 spin's worth
                        else if (totalSpins * TIMEOUTROULETTELENGTH < 1)
                            totalTimeoutLength = TIMEOUTROULETTELENGTH;
                        //default situation calculating a user's total timeout time
                        else
                            totalTimeoutLength = totalSpins * TIMEOUTROULETTELENGTH;

                        RouletteTimeout(e.ChatterUserLogin, e.ChatterUserId, e.ChatterUserName, e.MessageId, totalTimeoutLength, e.IsModerator);

                        //stops looping through parent for loop since user has failed at least 1 of the x spins they asked for
                        return;
                    }
                }

                //only reaches here if user succeeds all roulette spins
                //check if user is in leaderboard already
                if (MainWindow.rouletteLeaderboard.ContainsKey(e.ChatterUserLogin))
                {
                    MainWindow.rouletteLeaderboard[e.ChatterUserLogin] += totalSpins;
                }
                else
                    MainWindow.rouletteLeaderboard.Add(e.ChatterUserLogin, 1);

                int rouletteLeaderboardCount = MainWindow.rouletteLeaderboard[e.ChatterUserLogin];


                //helps to avoid spamming the chat if roulette command is too popular
                if (Properties.Settings.Default.DisplayRouletteSuccessMessage)
                {
                    string rouletteSurvivalMessage = $"{e.ChatterUserName} has survived the timeout roulette {rouletteLeaderboardCount} time(s)";
                    TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, rouletteSurvivalMessage, e.MessageId, true);
                }

                SaveRouletteLeaderboardToJson();
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog("Roulette Exception: " + except.Message);
            }
        }

        async void RouletteTimeout(string senderUsername, string senderUserId, string senderDisplayName, string parentMessageId, int timeoutLength, bool isModerator)
        {
            MainWindow.rouletteLeaderboard = GetRouletteLeaderboardFromJson();

            await TwitchUtility.CheckAccessToken();

            int leaderboardSpins = 0;
            if (MainWindow.rouletteLeaderboard.ContainsKey(senderUsername))
            {
                leaderboardSpins = MainWindow.rouletteLeaderboard[senderUsername];
                MainWindow.rouletteLeaderboard.Remove(senderUsername);
            }

            string timeoutRouletteMessage = "";
            if(leaderboardSpins == 0)
                timeoutRouletteMessage = $"{senderDisplayName} won the roulette and timed themselves out for {timeoutLength} seconds after surviving {leaderboardSpins} spins!";
            else
                timeoutRouletteMessage = $"{senderDisplayName} won the roulette and timed themselves out for {timeoutLength} seconds on their first spin!";

            TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, timeoutRouletteMessage, parentMessageId, true);

            //ban info for current user
            BanUserRequest request = new BanUserRequest();
            request.UserId = senderUserId;
            request.Reason = "Congrats! You won the timeout roulette!";
            request.Duration = timeoutLength;

            //add specific channel and acting moderator info to current user ban info
            BanUserResponse result = _TwitchAPI.Helix.Moderation.BanUserAsync(
                GlobalObjects.TwitchBroadcasterUserId,
                GlobalObjects.TwitchBroadcasterUserId,
                request
                ).Result;

            //Log("Roulette result: " + result.Data);

            SaveRouletteLeaderboardToJson();

            if (isModerator)
            {
                new Thread(delegate ()
                {
                    TwitchUtility.ReinstateModRole(_TwitchAPI, GlobalObjects.TwitchBroadcasterUserId, senderUserId, senderUsername, TIMEOUTROULETTELENGTH);
                }).Start();
            }
        }

        void RouletteLeaderboardCommand(string parentMessageId)
        {
            //var topGroups = MainWindow.rouletteLeaderboard.OrderByDescending(x => x.Value).GroupBy(x => x.Value).Take(3);

            List<RouletteLeaderboardPosition> topLeaderboardSpots = GetTopRouletteLeaderboardPositions(MainWindow.rouletteLeaderboard);


            if (topLeaderboardSpots.Count == 0)
            {
                string emptyLeaderboardMessage = "There are currently no people listed on the timeout roulette leaderboard. Make sure to have at least 1 spin without being timed out to show up here!";
                TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, emptyLeaderboardMessage, parentMessageId, true);
            }
            else
            {
                string leaderboardOutput = $"The most active spins without a timeout are: ";
                foreach (var topSpot in topLeaderboardSpots)
                {
                    if (topSpot.spinCount == 1)
                        leaderboardOutput += $" {topSpot.username} with {topSpot.spinCount} spin,";
                    else
                        leaderboardOutput += $" {topSpot.username} with {topSpot.spinCount} spins,";
                }

                leaderboardOutput = leaderboardOutput.Remove(leaderboardOutput.LastIndexOf(","), 1);

                TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, leaderboardOutput, parentMessageId, true);
            }
        }

        public List<RouletteLeaderboardPosition> GetTopRouletteLeaderboardPositions(Dictionary<string, int> leaderboard)
        {
            List<RouletteLeaderboardPosition> topPositions = new List<RouletteLeaderboardPosition>();

            var topGroups = MainWindow.rouletteLeaderboard.OrderByDescending(x => x.Value).GroupBy(x => x.Value).Take(TIMEOUTROULETTETOPPOSITIONSTODISPLAY);

            foreach (var topGroup in topGroups)
            {
                foreach (var topPair in topGroup)
                {
                    if (topPositions.Count != TIMEOUTROULETTETOPPOSITIONSTODISPLAY && topPair.Value != 0)
                    {
                        RouletteLeaderboardPosition currPosition = new RouletteLeaderboardPosition(topPair.Key, topPair.Value);
                        topPositions.Add(currPosition);
                    }
                    else
                        return topPositions;
                }
            }

            return topPositions;
        }

        void RollCommand(string[] messageInput, string parentMessageId)
        {
            try
            {
                //retrieve and split roll command into 2 segments: *number of dice* and *size of die*
                string rollInput = messageInput[1];
                string[] rollParams;

                if (!rollInput.Contains('d'))
                {
                    rollParams = ["1", rollInput];
                }
                else
                    rollParams = rollInput.Split("d");


                //convert param0 to a long
                int diceToRoll;
                try
                {
                    //take care of all reasonable ways a user might want to roll just 1 die
                    if (rollParams[0] == null || rollParams[0] == "" || rollParams[0] == "1")
                    {
                        diceToRoll = 1;
                    }
                    else
                    {
                        diceToRoll = Int32.Parse(rollParams[0]);
                    }
                }
                catch (Exception ex)
                {
                    WPFUtility.WriteToLog("Roll command error converting param 0: " + ex.Message);

                    string rollInputErrorMessage = "The number of dice to roll needs to be a whole number greater than or equal to 1";
                    TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, rollInputErrorMessage, parentMessageId, true);

                    diceToRoll = -1;
                }

                //try to convert param[1] to long if param[0] was a valid number
                if (diceToRoll != -1)
                {
                    try
                    {
                        long sizeOfDie = Int64.Parse(rollParams[1]);
                        if (sizeOfDie <= 1)
                        {
                            string rollInputErrorMessage = "The size of the die to roll needs to be a whole number greater than 1";
                            TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, rollInputErrorMessage, parentMessageId, true);
                        }
                        //roll dice
                        else
                        {
                            Random random = new Random();
                            long total = 0;

                            for (int x = 1; x <= diceToRoll; x++)
                            {
                                total += random.NextInt64(1, sizeOfDie + 1);
                            }

                            string rollResultMessage = "";
                            if (total == 1)
                                rollResultMessage = "You rolled: a nat 1! Good job!";
                            else
                                rollResultMessage = $"You rolled: {total}";

                            TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, rollResultMessage, parentMessageId, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        WPFUtility.WriteToLog("Roll command error converting param 1: " + ex.Message);
                    }
                }
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog("Roll command: " + except.Message);
            }
        }

        void FirstCommand(string userId, string userDisplayName, string parentMessageId)
        {
            Dictionary<string, int> firstRedeemLeaderboard = null;
            try
            {
                string firstRedeemJsonInput = File.ReadAllText(FIRSTREDEEMSJSONFILENAME);

                var deserializedLeaderboard = JsonConvert.DeserializeObject<Dictionary<string, int>>(firstRedeemJsonInput);
                if (deserializedLeaderboard == null)
                    WPFUtility.WriteToLog($"FirstRedeem leaderboard was empty when trying to read user scores");
                else
                    firstRedeemLeaderboard = deserializedLeaderboard;
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog($"Read from first redeem leaderboard JSON error: {except.Message}");
                return;
            }

            if (firstRedeemLeaderboard != null)
            {
                string firstResultMessage = "";

                if (firstRedeemLeaderboard.ContainsKey(userId))
                    firstResultMessage = $"{userDisplayName} has been first {firstRedeemLeaderboard[userId]} time(s)!";
                else
                    firstResultMessage = $"{userDisplayName} hasn't been first before.";

                TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, firstResultMessage, parentMessageId, true);
            }
        }

        public Dictionary<string, int> GetRouletteLeaderboardFromJson()
        {
            try
            {
                string rouletteJsonInput = File.ReadAllText(ROULETTEJSONFILENAME);

                var deserializedLeaderboard = JsonConvert.DeserializeObject<Dictionary<string, int>>(rouletteJsonInput);
                if (deserializedLeaderboard == null)
                    return new Dictionary<string, int>();
                else
                    return deserializedLeaderboard;
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog($"Read from roulette leaderboard JSON error: {except.Message}");
                return new Dictionary<string, int>();
            }
        }

        public void SaveRouletteLeaderboardToJson()
        {
            if (MainWindow.rouletteLeaderboard == null)
            {
                WPFUtility.WriteToLog($"rouletteLeaderboard was null when trying to save to file");
                return;
            }

            //var rouletteJson = JsonConvert.SerializeObject(rouletteLeaderboard.ToArray());    //for list
            var rouletteJson = JsonConvert.SerializeObject(MainWindow.rouletteLeaderboard);

            System.IO.File.WriteAllText(ROULETTEJSONFILENAME, rouletteJson);
        }

        //Ping API Ninja for a fact and output to twitch chat
        async private void APINinjaGetFact()
        {
            try
            {
                //no need to add a limit since default is already 1
                string apiUrl = "facts?X-Api-Key=" + Properties.Settings.Default.APINinjaKey;
                var response = await NinjaAPIConnection.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    //API get request
                    string stringResponse = await response.Content.ReadAsStringAsync();

                    WPFUtility.WriteToLog($"Fact test: {stringResponse}");

                    //convert API call to array of objects. we only ever call 1 result from API so length will always be 1
                    APINinjaFacts[] result = JsonConvert.DeserializeObject<APINinjaFacts[]>(stringResponse);

                    TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, result[0].fact, sendMessageAsChatBot: true);
                    //TwitchPlays.SpeechSynthSync(result[0].fact);
                    _SpeechSynth.SpeechSynth(result[0].fact);
                }
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog("Fact command: " + except.Message);
            }
        }

        //Ping API Ninja for a dad joke and output to twitch chat
        async private void APINinjaGetDadJoke()
        {
            try
            {
                //no need to add a limit since default is already 1
                string apiUrl = "dadjokes?X-Api-Key=" + Properties.Settings.Default.APINinjaKey;
                var response = await NinjaAPIConnection.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    //API get request
                    string stringResponse = await response.Content.ReadAsStringAsync();

                    //convert API call to array of objects. we only ever call 1 result from API so length will always be 1
                    APINinjaDadJokes[] result = JsonConvert.DeserializeObject<APINinjaDadJokes[]>(stringResponse);

                    TwitchUtility.SendChatMessage(GlobalObjects._TwitchAPIBotAccount, GlobalObjects.TwitchMessageBotUserId, GlobalObjects.TwitchBroadcasterUserId, result[0].joke, sendMessageAsChatBot: true);
                    //TwitchPlays.SpeechSynthSync(result[0].joke);
                    _SpeechSynth.SpeechSynth(result[0].joke);
                }
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog("Dad Joke command: " + except.Message);
            }
        }
    }
}
