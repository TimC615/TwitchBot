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

namespace TwitchBot
{
    internal class TwitchChatCommands
    {
        TwitchClient _TwitchClient;
        TwitchAPI _TwitchAPI;

        string TwitchChannelName;
        string TwitchChannelId;

        SpeechSynthesis _SpeechSynth;
        HttpClient NinjaAPIConnection;

        private static readonly int TIMEOUTROULETTELENGTH = 30;      //timeout length, in seconds
        private static readonly int TIMEOUTROULETTETOPPOSITIONSTODISPLAY = 3;   //used to determine max number of results to show for leaderboard display

        private static readonly string FIRSTREDEEMSJSONFILENAME = @"firstredeemsleaderboard.json";
        private readonly string ROULETTEJSONFILENAME = @"rouletteleaderboard.json";

        Dictionary<string, string> CommandsStaticResponses = new Dictionary<string, string>
        {
            { "about", "Hello! I'm TheCakeIsAPie__ and I'm a Canadian variety streamer. We play a bunch of stuff over here in this small corner of the internet. Come pop a seat and have fun watching the shenanigans!"},
            { "discord", "Join the discord server at: https://discord.gg/uzHqnxKKkC"},
            { "twitter", "Follow me on Twitter at: https://twitter.com/TheCakeIsAPi"},
            { "lurk", "Have fun lurking!"}
        };

        public TwitchChatCommands(TwitchClient _TwitchClient, TwitchAPI _twitchAPI, 
            string twitchChannelName, string twitchChannelId, 
            SpeechSynthesis _SpeechSynth, HttpClient NinjaAPIConnection)
        {
            this._TwitchClient = _TwitchClient;
            this._TwitchAPI = _twitchAPI;

            this.TwitchChannelName = twitchChannelName;
            this.TwitchChannelId = twitchChannelId;

            this._SpeechSynth = _SpeechSynth;
            this.NinjaAPIConnection = NinjaAPIConnection;
        }

        public void BaseCommandMethod(TwitchLib.Client.Models.ChatCommand e)
        {
            string commandText = e.CommandText.ToLower();

            //2 ways to deal with commands: if/switch statements OR dictionary lookups

            //responses are added to dictionary in lowercase
            if (CommandsStaticResponses.TryGetValue(commandText, out string? value))
            {
                _TwitchClient.SendReply(TwitchChannelName,
                    e.ChatMessage.Id,
                    value);
            }

            //more complex comands
            else
            {
                //return list of current bot commands (added different command to avoid also showing commands for other Twitch bots)
                if(commandText.Equals("commands") || commandText.Equals("botmenu"))
                {
                    _TwitchClient.SendReply(TwitchChannelName,
                            e.ChatMessage.Id,
                            "The current chat commands are: help, about, discord, twitter, lurk, joke, fact, roll, roulette, rouletteleaderboard, and 1st");
                }


                //Tells user how to use commands
                if (commandText.Equals("help"))
                    HelpCommands(e);

                //roll a random dX sided die
                if (commandText.Contains("roll", StringComparison.OrdinalIgnoreCase))
                {
                    if(e.ArgumentsAsList.Count >= 1)
                    {
                        RollCommand(e);
                    }
                    else
                        _TwitchClient.SendReply(TwitchChannelName,
                            e.ChatMessage.Id,
                            "Make sure you add in the number of dice to roll and the number of sides on a die (e.g. \"!roll 2d6\")");
                }

                //Grab random fact from https://api-ninjas.com/
                if (commandText.Equals("fact"))
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
                if (commandText.Equals("joke"))
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
                if (commandText.Equals("roulette"))
                {
                    RouletteCommand(e);
                }

                //Displays a list of the chatters with the most timeout roulette spins without a timeout
                if (commandText.Equals("rouletteleaderboard"))
                {
                    RouletteLeaderboardCommand(e);
                }

                if (commandText.Equals("first") || commandText.Equals("1st"))
                {
                    FirstCommand(e);
                }

                //Tell user what skyrim spawn commands are avaialble (only when Twitch Plays is active)
                if (commandText.Equals("skyrim") && MainWindow.twitchPlaysEnable)
                {
                    string skyrimCommands = "You can mess with skyrim by saying any of the following: forward, back, stop, left, right, jump, " +
                        "cheese, soup, wine, potions, rabbits, skeevers, bears, lydia, spiders, dragons, cheesemageddon, and soupmageddon";
                    _TwitchClient.SendReply(TwitchChannelName,
                        e.ChatMessage.Id,
                        skyrimCommands);
                }
            }
        }

        void HelpCommands(TwitchLib.Client.Models.ChatCommand e)
        {
                List<string> test = e.ArgumentsAsList;

                if (e.ArgumentsAsList.Count == 0)
                    _TwitchClient.SendReply(TwitchChannelName,
                        e.ChatMessage.Id,
                        "Type \"!help <command>\" to see how you can use it (E.g. !help roll)");
                else
                {
                    try
                    {
                        string helpSpecifier = e.ArgumentsAsList[0].ToLower();

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
                                _TwitchClient.SendReply(TwitchChannelName,
                                    e.ChatMessage.Id,
                                    "Enter \"!" + helpSpecifier + "\" and I'll do all the rest");
                                break;
                            case "roll":
                                _TwitchClient.SendReply(TwitchChannelName,
                                    e.ChatMessage.Id,
                                    "Enter \"!roll d<number of sides>\" to roll a single die or \"!roll <number of dice>d<number of sides>\" to roll multiple dice! (e.g. !roll d6) or !roll 3d20");
                                break;
                            case "roulette":
                                _TwitchClient.SendReply(TwitchChannelName,
                                    e.ChatMessage.Id,
                                    "Enter \"!roulette\" for a chance to time yourself out for " + TIMEOUTROULETTELENGTH + " seconds");
                                break;
                            case "rouletteleaderboard":
                                _TwitchClient.SendReply(TwitchChannelName,
                                    e.ChatMessage.Id,
                                    "Enter \"!rouletteleaderboard\" to see who has the highest active streaks on the timeout roulette wheel!");
                                break;
                            default:
                                break;
                        }
                    }
                    catch (Exception except)
                    {
                        WPFUtility.WriteToLog("Help command: " + except.Message);
                    }
                }
        }

        async void RouletteCommand(TwitchLib.Client.Models.ChatCommand e)
        {
            try
            {
                //Log($"Roulette triggered by {e.ChatMessage.DisplayName}");

                Random random = new Random();

                if (e.ChatMessage.IsMe || e.ChatMessage.IsBroadcaster || e.ChatMessage.IsStaff)
                {
                    _TwitchClient.SendReply(TwitchChannelName,
                        e.ChatMessage.Id,
                        $"Sorry {e.ChatMessage.Username}, you're not able to be timed out so you can't spin the roulette");
                    throw new Exception("User tried to timeout as restricted role");
                }

                //1 in 10 chance 
                if (random.Next(1, 11) == 1)
                {
                    MainWindow.rouletteLeaderboard = GetRouletteLeaderboardFromJson();

                    await TwitchUtility.CheckAccessToken();

                    bool isMod = false;
                    if (e.ChatMessage.IsModerator)
                        isMod = true;



                    int leaderboardSpins = 0;
                    if (MainWindow.rouletteLeaderboard.ContainsKey(e.ChatMessage.Username))
                    {
                        leaderboardSpins = MainWindow.rouletteLeaderboard[e.ChatMessage.Username];
                        MainWindow.rouletteLeaderboard.Remove(e.ChatMessage.Username);
                    }

                    string timeoutRouletteMessage = $"Congrats {e.ChatMessage.DisplayName}! You won the roulette and timed yourself out after surviving {leaderboardSpins} spins!";
                    _TwitchClient.SendReply(TwitchChannelName,
                        e.ChatMessage.Id,
                        timeoutRouletteMessage);

                    //ban info for current user
                    BanUserRequest request = new BanUserRequest();
                    request.UserId = e.ChatMessage.UserId;
                    request.Reason = "Congrats! You won the timeout roulette!";
                    request.Duration = TIMEOUTROULETTELENGTH;

                    //add specific channel and acting moderator info to current user ban info
                    BanUserResponse result = _TwitchAPI.Helix.Moderation.BanUserAsync(
                        TwitchChannelId,
                        TwitchChannelId,
                        request
                        ).Result;

                    //Log("Roulette result: " + result.Data);

                    SaveRouletteLeaderboardToJson();

                    if (isMod)
                    {
                        new Thread(delegate ()
                        {
                            TwitchUtility.ReinstateModRole(_TwitchAPI, TwitchChannelId, e.ChatMessage.UserId, e.ChatMessage.Username, TIMEOUTROULETTELENGTH);
                        }).Start();
                    }
                }
                else
                {
                    //check if user is in leaderboard already
                    if (MainWindow.rouletteLeaderboard.ContainsKey(e.ChatMessage.Username))
                    {
                        MainWindow.rouletteLeaderboard[e.ChatMessage.Username]++;
                    }
                    else
                        MainWindow.rouletteLeaderboard.Add(e.ChatMessage.Username, 1);

                    int rouletteLeaderboardCount = MainWindow.rouletteLeaderboard[e.ChatMessage.Username];


                    _TwitchClient.SendReply(TwitchChannelName,
                        e.ChatMessage.Id,
                        $"{e.ChatMessage.DisplayName} has survived the timeout roulette {rouletteLeaderboardCount} time(s)");

                    SaveRouletteLeaderboardToJson();
                }
            }
            catch (Exception except)
            {
                WPFUtility.WriteToLog("Roulette Exception: " + except.Message);
            }
        }

        void RouletteLeaderboardCommand(TwitchLib.Client.Models.ChatCommand e)
        {
            //var topGroups = MainWindow.rouletteLeaderboard.OrderByDescending(x => x.Value).GroupBy(x => x.Value).Take(3);

            List<RouletteLeaderboardPosition> topLeaderboardSpots = GetTopRouletteLeaderboardPositions(MainWindow.rouletteLeaderboard);


            if (topLeaderboardSpots.Count == 0)
            {
                _TwitchClient.SendReply(TwitchChannelName,
                e.ChatMessage.Id,
                "There are currently no people listed on the timeout roulette leaderboard. Make sure to have at least 1 spin without being timed out to show up here!");
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

                _TwitchClient.SendReply(TwitchChannelName,
                    e.ChatMessage.Id,
                    leaderboardOutput);
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


        void RollCommand(TwitchLib.Client.Models.ChatCommand e)
        {
            try
            {
                //retrieve and split roll command into 2 segments: *number of dice* and *size of die*
                List<string> rollCommand = e.ArgumentsAsList;

                string rollInput = rollCommand[0].ToLower();
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
                    _TwitchClient.SendReply(TwitchChannelName,
                        e.ChatMessage.Id,
                        "The number of dice to roll needs to be a whole number greater than or equal to 1");
                    diceToRoll = -1;
                }

                //try to convert param[1] to long if param[0] was a valid number
                if (diceToRoll != -1)
                {
                    try
                    {
                        long sizeOfDie = Int64.Parse(rollParams[1]);
                        if (sizeOfDie <= 1)
                            _TwitchClient.SendReply(TwitchChannelName,
                                e.ChatMessage.Id,
                                "The size of the die to roll needs to be a whole number greater than 1");
                        //roll dice
                        else
                        {
                            Random random = new Random();
                            long total = 0;

                            for (int x = 1; x <= diceToRoll; x++)
                            {
                                total += random.NextInt64(1, sizeOfDie + 1);
                            }

                            if (total == 1)
                                _TwitchClient.SendReply(TwitchChannelName, e.ChatMessage.Id, "You rolled: a nat 1! Good job!");
                            else
                                _TwitchClient.SendReply(TwitchChannelName, e.ChatMessage.Id, "You rolled: " + total);
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

        void FirstCommand(TwitchLib.Client.Models.ChatCommand e)
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
                string username = e.ChatMessage.DisplayName;

                if (firstRedeemLeaderboard.ContainsKey(username))
                    _TwitchClient.SendReply(TwitchChannelName,
                        e.ChatMessage.Id,
                        $"{username} has been first {firstRedeemLeaderboard[username]} time(s)!");
                else
                    _TwitchClient.SendReply(TwitchChannelName,
                        e.ChatMessage.Id,
                        $"{username} hasn't been first before.");
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

                    //convert API call to array of objects. we only ever call 1 result from API so length will always be 1
                    APINinjaFacts[] result = JsonConvert.DeserializeObject<APINinjaFacts[]>(stringResponse);

                    _TwitchClient.SendMessage(TwitchChannelName, result[0].fact);
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

                    _TwitchClient.SendMessage(TwitchChannelName, result[0].joke);
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
