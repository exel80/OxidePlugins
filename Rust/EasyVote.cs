using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using System.Text;
using Oxide.Core.Libraries;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{

    //Fixes by dfxphoenix
    //Added RustServers.gg API by SheepRW
    //Removed rust-servers.info API Hyper
    //Colour change, added rust-servers.info API, removed Beancan API by MikeHawke
    //Removed Resource ID by MikeHawke
    //Added BestServers.com by MikeHawke
    [Info("EasyVote", "Exel80", "2.0.45")]
    [Description("Simple and smooth voting start by activating one script.")]
    class EasyVote : RustPlugin
    {
        [PluginReference] private Plugin DiscordMessages;

        #region Initializing
        // Permissions
        private const bool DEBUG = false;
        private const string permUse = "EasyVote.Use";

        // Vote status arrays
        // 0 = Havent voted yet OR already claimed.
        // 1 = Voted and waiting claiming.
        // 2 = Already claimed reward (Far us i know, RustServers is only who use this response number)
        protected string[] voteStatus = { "No reward(s)", "Claim reward(s)", "Claim reward(s) / Already claimed?" };
        protected string[] voteStatusColor = { "#ff0000", "#44ff00", "#ffff00" };

        // Spam protect list
        Dictionary<ulong, StringBuilder> claimCooldown = new Dictionary<ulong, StringBuilder>();
        Dictionary<ulong, bool> checkCooldown = new Dictionary<ulong, bool>();

        // List received reward(s) one big list.
        StringBuilder rewardsString = new StringBuilder();

        // List all vote sites.
        List<string> availableAPISites = new List<string>();
        StringBuilder _voteList = new StringBuilder();
        StringBuilder helpYou = new StringBuilder();
        private List<int> numberMax = new List<int>();

        void Loaded()
        {
            // Load configs
            LoadConfigValues();
            LoadMessages();

            // Regitering permissions
            permission.RegisterPermission(permUse, this);

            // Load storedata
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("EasyVote");

            // Check rewards and add them one big list
            BuildNumberMax();

            // Build helptext
            HelpText();

            // Check available vote sites
            checkVoteSites();

            // Build StringBuilders
            voteList();
        }
        #endregion

        #region Localization
        string _lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You do not have permission to use this command!",
                ["ClaimStatus"] = "<color=#00fff7>[{0}]</color> Checked {1}, Status: {2}",
                ["ClaimError"] = "Something went wrong! Player <color=#ff0000>{0} got an error</color> from <color=#fffb00>{1}</color>. Please try again later!",
                ["ClaimReward"] = "You just received your vote reward(s). Enjoy!",
                ["ClaimPleaseWait"] = "Checking the voting websites. Please wait...",
                ["VoteList"] = "You have voted <color=#fffb00>{1}</color> time(s)!\n Leave another vote on these websites:\n{0}",
                ["EarnReward"] = "When you have voted, type <color=#fffb00>/claim</color> to claim your reward(s)!",
                ["RewardListFirstTime"] = "<color=#00fff7>Reward for voting for the first time.</color>",
                ["RewardListEverytime"] = "<color=#00fff7>Reward, which player will receive everytime they vote.</color>",
                ["RewardList"] = "<color=#00fff7>Reward for voting</color> <color=#FFA500>{0}</color> <color=#00fff7>time(s).</color>",
                ["Received"] = "You have received {0}x {1}",
                ["ThankYou"] = "Thank you for voting! You have voted <color=#fffb00>{0}</color> time(s) Here is your reward for..\n{1}",
                ["NoRewards"] = "You do not have any new rewards available\n Please type <color=#fffb00>/vote</color> and go to one of the websites to vote and receive your reward",
                ["RemeberClaim"] = "You haven't claimed your reward from voting for the server yet! Use <color=#fffb00>/claim</color> to claim your reward!\n You have to claim your reward within <color=#fffb00>24h</color>! Otherwise it will be gone!",
                ["GlobalChatAnnouncments"] = "<color=#fffb00>{0}</color><color=#00fff7> has voted </color><color=#fffb00>{1}</color><color=#00fff7> time(s) and just received their rewards. Find out where you can vote by typing</color><color=#fffb00> /vote</color>\n<color=#00fff7>To see a list of avaliable rewards type</color><color=#fffb00> /rewardlist</color>",
                ["money"] = "<color=#fffb00>{0}$</color> has been desposited into your account",
                ["rp"] = "You have gained <color=#fffb00>{0}</color> reward points",
                ["tempaddgroup"] = "You have been temporality added to <color=#fffb00>{0}</color> group (Expire in {1})",
                ["tempgrantperm"] = "You have temporality granted <color=#fffb00>{0}</color> permission (Expire in {1})",
                ["zlvl-wc"] = "You have gained <color=#fffb00>{0}</color> woodcrafting level(s)",
                ["zlvl-m"] = "You have gained <color=#fffb00>{0}</color> mining level(s)",
                ["zlvl-s"] = "You have gained <color=#fffb00>{0}</color> skinning level(s)",
                ["zlvl-c"] = "You have gained <color=#fffb00>{0}</color> crafting level(s)",
                ["zlvl-*"] = "You have gained <color=#fffb00>{0}</color> in all level(s)",
                ["oxidegrantperm"] = "You have been granted <color=#fffb00>{0}</color> permission",
                ["oxiderevokeperm"] = "Your permission <color=#fffb00>{0}</color> has been revoked",
                ["oxidegrantgroup"] = "You have been added to <color=#fffb00>{0}</color> group",
                ["oxiderevokegroup"] = "You have been removed from <color=#fffb00>{0}</color> group"
            }, this);
        }
        #endregion

        #region Hooks
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            // If (for some reason) player is stuck in claimCooldown list.
            if (claimCooldown.ContainsKey(player.userID))
                claimCooldown.Remove(player.userID);
        }

        private void SendHelpText(BasePlayer player)
        {
            if (hasPermission(player, permUse))
                player.ChatMessage(helpYou.ToString());
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!hasPermission(player, permUse))
                return;

            // Check if player exist in cooldown list or not
            if (!checkCooldown.ContainsKey(player.userID))
                checkCooldown.Add(player.userID, false);
            else if (checkCooldown.ContainsKey(player.userID))
                return;

            var timeout = 5500f; // Timeout (in milliseconds)

            foreach (var site in availableAPISites.ToList())
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> kvp in _config.Servers)
                {
                    foreach (KeyValuePair<string, string> vp in kvp.Value)
                    {
                        if (vp.Key != site)
                            continue;

                        string[] idKeySplit = vp.Value.Split(':');
                        foreach (KeyValuePair<string, string> SitesApi in _config.VoteSitesAPI[site])
                        {
                            if (SitesApi.Key == PluginSettings.apiStatus)
                            {
                                // Formating api claim =>
                                // {0} = Key
                                // {1} PlayerID
                                string _format = String.Format(SitesApi.Value, idKeySplit[1], player.userID, idKeySplit[0]);

                                // Send GET request to voteAPI site.
                                webrequest.Enqueue(_format, null, (code, response) => CheckStatus(code, response, player), this, RequestMethod.GET, null, timeout);

                                _Debug($"GET: {_format} =>\n Site: {site} Server: {kvp.Key} Id: {idKeySplit[0]}");
                            }
                        }
                    }
                }
            }
            // Wait 3.69 sec before execute this command.
            // Because need make sure that plugin webrequest all api sites.
            timer.Once(3.69f, () =>
            {
                if (checkCooldown[player.userID])
                {
                    Chat(player, $"{_lang("RemeberClaim", player.UserIDString)}");
                }

                // Remove player from cooldown list
                checkCooldown.Remove(player.userID);
            });
        }
        #endregion

        #region Commands
        [ChatCommand("vote")]
        void cmdVote(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, permUse))
            {
                Chat(player, _lang("NoPermission", player.UserIDString));
                return;
            }

            // Check how many time player has voted.
            int voted = 0;
            if (_storedData.Players.ContainsKey(player.UserIDString))
                voted = _storedData.Players[player.UserIDString].voted;

            Chat(player, _lang("VoteList", player.UserIDString, _voteList.ToString(), voted));
            Chat(player, _lang("EarnReward", player.UserIDString));
        }

        [ChatCommand("claim")]
        void cmdClaim(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, permUse))
            {
                Chat(player, _lang("NoPermission", player.UserIDString));
                return;
            }

            // Check if player exist in cooldown list or not
            if (!claimCooldown.ContainsKey(player.userID))
                claimCooldown.Add(player.userID, new StringBuilder());
            else if (claimCooldown.ContainsKey(player.userID))
                return;

            var timeout = 5500f; // Timeout (in milliseconds)
            Chat(player, _lang("ClaimPleaseWait", player.UserIDString));

            foreach (var site in availableAPISites.ToList())
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> kvp in _config.Servers)
                {
                    foreach (KeyValuePair<string, string> vp in kvp.Value)
                    {
                        // Make sure that key is site
                        if (vp.Key != site)
                            continue;

                        // Null check for ID & KEY
                        if (!vp.Value.Contains(":"))
                        {
                            _Debug($"{kvp.Key} {vp.Key} does NOT contains ID or Key !!!");
                            continue;
                        }
                        else if (vp.Value.Split(':')[0] == "ID")
                        {
                            _Debug($"{kvp.Key} {vp.Key} does NOT contains ID !!!");
                            continue;
                        }
                        else if (vp.Value.Split(':')[1] == "KEY")
                        {
                            _Debug($"{kvp.Key} {vp.Key} does NOT contains KEY !!!");
                            continue;
                        }

                        // Split ID & Key
                        string[] idKeySplit = vp.Value.Split(':');

                        // Loop API pages
                        foreach (KeyValuePair<string, string> SitesApi in _config.VoteSitesAPI[site])
                        {
                            // Got apiClaim url
                            if (SitesApi.Key == PluginSettings.apiClaim)
                            {
                                // Formating api claim =>
                                // {0} APIKey
                                // {1} SteamID
                                // Example: "http://rust-servers.net/api/?action=custom&object=plugin&element=reward&key= {0} &steamid= {1} ",
                                string _format = String.Format(SitesApi.Value, idKeySplit[1], player.userID, idKeySplit[0]);

                                // Send GET request to voteAPI site.
                                webrequest.Enqueue(_format, null, (code, response) => ClaimReward(code, response, player, site, kvp.Key), this, RequestMethod.GET, null, timeout);

                                _Debug($"Player: {player.displayName} - Check claim URL: {_format}\nSite: {site} Server: {kvp.Key} VoteAPI-ID: {idKeySplit[0]} VoteAPI-KEY: {idKeySplit[1]}");
                            }
                        }
                    }
                }
            }

            // Wait 5.55 sec before remove player from cooldown list.
            timer.Once(5.55f, () =>
            {
                try
                {
                    // Print builded stringbuilder
                    Chat(player, claimCooldown[player.userID].ToString(), false);

                    // Remove player from cooldown list
                    claimCooldown.Remove(player.userID);
                }
                catch (Exception ex) { _Debug($"Error happen when try print \\claim status to \"{player.displayName}\"\n{ex.ToString()}"); PrintError("[ClaimStatus] Error printed to oxide/logs/EasyVote"); }
            });
        }

        [ChatCommand("rewardlist")]
        void cmdReward(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, permUse))
            {
                Chat(player, _lang("NoPermission", player.UserIDString));
                return;
            }

            rewardList(player);
        }
        #endregion

        #region Reward Handler
        private void RewardHandler(BasePlayer player, string serverName = null)
        {
            // Check that player is in "database".
            var playerData = new PlayerData();
            if (!_storedData.Players.ContainsKey(player.UserIDString))
            {
                _storedData.Players.Add(player.UserIDString, playerData);
                _storedData.Players[player.UserIDString].lastTime_Voted = DateTime.UtcNow;
                Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData);
            }

            // Add +1 vote to player.
            _storedData.Players[player.UserIDString].voted++;
            _storedData.Players[player.UserIDString].lastTime_Voted = DateTime.UtcNow;
            Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData);

            // Get how many time player has voted.
            int voted = _storedData.Players[player.UserIDString].voted;

            // Take closest number from rewardNumbers
            int? closest = null;
            if (numberMax.Count != 0)
            {
                try
                {
                    closest = (int?)numberMax.Aggregate((x, y) => Math.Abs(x - voted) < Math.Abs(y - voted)
                            ? (x > voted ? y : x)
                            : (y > voted ? x : y));
                }
                catch (InvalidOperationException error) { _Debug($"Player {player.displayName} tried to claim a reward but this happened ...\n{error.ToString()}"); PrintError("[ClaimReward] Error printed to oxide/logs/EasyVote"); return; }

                if (closest > voted)
                {
                    _Debug($"Closest ({closest}) number was bigger then voted number ({voted}). Changed closest from ({closest}) to 0");
                    closest = 0;
                }

                _Debug($"Reward Number: {closest} Voted: {voted}");
            }

            // and here the magic happens. Loop for all rewards.
            foreach (KeyValuePair<string, List<string>> kvp in _config.Rewards)
            {
                // If first time voted
                if (kvp.Key.ToLower() == "first")
                {
                    // Make sure that this is player first time voting
                    if (voted > 1)
                        continue;

                    GaveRewards(player, kvp.Value);
                    continue;
                }

                // Gave this reward everytime
                if (kvp.Key == "@")
                {
                    GaveRewards(player, kvp.Value);
                    continue;
                }

                // Cumlative reward
                if (_config.Settings[PluginSettings.RewardIsCumulative].ToLower() == "true")
                {
                    if (kvp.Key.ToString().Contains("vote"))
                    {
                        // Tryparse vote number
                        int voteNumber;
                        if (!int.TryParse(kvp.Key.Replace("vote", ""), out voteNumber))
                            continue;

                        // All reward has now claimed
                        if (voteNumber > closest)
                            continue;

                        _Debug($" -> About to gave {kvp.Key} rewards");
                        GaveRewards(player, kvp.Value);
                    }
                    continue;
                }

                // Got closest vote
                if (closest != null)
                {
                    if (kvp.Key.ToString() == $"vote{closest}")
                    {
                        GaveRewards(player, kvp.Value);
                    }
                }

            }

            if (_config.Settings[PluginSettings.GlobalChatAnnouncments]?.ToLower() == "true")
            {
                foreach (var p in BasePlayer.activePlayerList)
                {
                    Chat(p, $"{_lang("GlobalChatAnnouncments", p.UserIDString, p.displayName, voted)}");
                }
            }

            // Send message to discord text channel.
            if (_config.Discord[PluginSettings.DiscordEnabled].ToLower() == "true")
            {
                List<Fields> fields = new List<Fields>();
                string json;

                fields.Add(new Fields("Voter", $"[{player.displayName}](https://steamcommunity.com/profiles/{player.userID})", true));
                fields.Add(new Fields("Voted", voted.ToString(), true));
                fields.Add(new Fields("Server", (serverName != null ? serverName : "[ UNKNOWN ]"), true));
                fields.Add(new Fields("Reward(s)", CleanHTML(rewardsString.ToString()), false));

                json = JsonConvert.SerializeObject(fields);
                DiscordMessages?.Call("API_SendFancyMessage", _config.Discord[PluginSettings.WebhookURL], _config.Discord[PluginSettings.Title], 3329330, json, bool.Parse(_config.Discord[PluginSettings.Alert]) ? "@here" : null);
            }

            // Make sure that player has voted etc.
            if (rewardsString.Length > 1)
            {
                // Hookmethod: void onUserReceiveReward(BasePlayer player, int voted)
                Interface.CallHook("onUserReceiveReward", player, voted);

                // Send ThankYou to player
                if (_config.Settings[PluginSettings.LocalChatAnnouncments].ToLower() == "true")
                    Chat(player, $"{_lang("ThankYou", player.UserIDString, voted, rewardsString.ToString())}");

                // Clear rewardString
                rewardsString.Clear();
            }
        }

        /// <summary>
        /// Gave all rewards from List<string>
        /// </summary>
        /// <param name="player"></param>
        /// <param name="rewardValue"></param>
        private void GaveRewards(BasePlayer player, List<string> rewardValue)
        {
            foreach (string reward in rewardValue)
            {
                // Split reward to variable and value.
                string[] valueSplit = reward.Split(':');
                string commmand = valueSplit[0];
                string value = valueSplit[1].Replace(" ", "");

                // Checking variables and run console command.
                // If variable not found, then try give item.
                if (_config.Commands.ContainsKey(commmand))
                {
                    _Debug($"{getCmdLine(player, commmand, value)}");
                    rust.RunServerCommand(getCmdLine(player, commmand, value));

                    if (!value.Contains("-"))
                        rewardsString.AppendLine($"- {_lang(commmand, player.UserIDString, value)}");
                    else
                    {
                        string[] _value = value.Split('-');
                        rewardsString.AppendLine($"- {_lang(commmand, player.UserIDString, _value[0], _value[1])}");
                    }

                    _Debug($"Ran command {String.Format(commmand, value)}");
                    continue;
                }
                else
                {
                    try
                    {
                        Item itemToReceive = ItemManager.CreateByName(commmand, Convert.ToInt32(value));
                        _Debug($"Received item {itemToReceive.info.displayName.translated} x{value}");
                        //If the item does not end up in the inventory
                        //Drop it on the ground for them
                        if (!player.inventory.GiveItem(itemToReceive, player.inventory.containerMain))
                            itemToReceive.Drop(player.GetDropPosition(), player.GetDropVelocity());

                        rewardsString.AppendLine($"- {_lang("Received", player.UserIDString, value, itemToReceive.info.displayName.translated)}");
                    }
                    catch (Exception e) { PrintWarning($"{e}"); }
                }
            }
        }

        private string getCmdLine(BasePlayer player, string str, string value)
        {
            var output = String.Empty;
            string playerid = player.UserIDString;
            string playername = player.displayName;

            // Checking if value contains => -
            if (!value.Contains('-'))
                output = _config.Commands[str].ToString()
                    .Replace("{playerid}", playerid)
                    .Replace("{playername}", '"' + playername + '"')
                    .Replace("{value}", value);
            else
            {
                string[] splitValue = value.Split('-');
                output = _config.Commands[str].ToString()
                    .Replace("{playerid}", playerid)
                    .Replace("{playername}", '"' + playername + '"')
                    .Replace("{value}", splitValue[0])
                    .Replace("{value2}", splitValue[1]);
            }
            return $"{output}";
        }
        #endregion

        #region Storing
        class StoredData
        {
            public Dictionary<string, PlayerData> Players = new Dictionary<string, PlayerData>();
            public StoredData() { }
        }
        class PlayerData
        {
            public int voted;
            public DateTime lastTime_Voted;

            public PlayerData()
            {
                voted = 0;
                lastTime_Voted = DateTime.UtcNow;
            }
        }
        StoredData _storedData;
        #endregion

        #region Webrequests
        void ClaimReward(int code, string response, BasePlayer player, string url, string serverName = null)
        {
            _Debug($"URL: {url} - Code: {code}, Response: {response}");

            // Change response to number
            int responseNum = 0;
            if (!int.TryParse(response, out responseNum))
                _Debug($"Cant understand vote site {url} response \"{response}\"");

            // If vote site is down
            if (code != 200)
            {
                PrintError("Error: {0} - Couldn't get an answer for {1} ({2})", code, player.displayName, url);
                Chat(player, $"{_lang("ClaimError", player.UserIDString, code, url)}");
                return;
            }

            // Add response to StringBuilder
            if (claimCooldown.ContainsKey(player.userID))
            {
                claimCooldown[player.userID].AppendLine(_lang("ClaimStatus", player.UserIDString,
                    (!string.IsNullOrEmpty(serverName) ? serverName : string.Empty), url, $"<color={voteStatusColor[responseNum]}>{voteStatus[responseNum]}</color>"));
            }

            // If response is 1 = Voted & not yet claimed
            if (responseNum == 1)
                RewardHandler(player, serverName);
        }

        void CheckStatus(int code, string response, BasePlayer player)
        {
            _Debug($"Player: {player.displayName} - Code: {code}, Response: {response}");

            if (response?.ToString() == "1" && code == 200)
            {
                if (!checkCooldown.ContainsKey(player.userID))
                {
                    checkCooldown.Add(player.userID, true);
                }
                checkCooldown[player.userID] = true;
            }
        }
        #endregion

        #region Configuration

        #region Configuration Defaults
        PluginConfig DefaultConfig()
        {
            var defaultConfig = new PluginConfig
            {
                Settings = new Dictionary<string, string>
                {
                    { PluginSettings.Prefix, "<color=#00fff7>[EasyVote]</color>" },
                    { PluginSettings.RewardIsCumulative, "false" },
                    { PluginSettings.LogEnabled, "true" },
                    { PluginSettings.GlobalChatAnnouncments, "true" },
                    { PluginSettings.LocalChatAnnouncments, "true" }
                },
                Discord = new Dictionary<string, string>
                {
                    { PluginSettings.DiscordEnabled, "false" },
                    { PluginSettings.Alert, "false" },
                    { PluginSettings.Title, "Vote" },
                    { PluginSettings.WebhookURL, "" }
                },
                Servers = new Dictionary<string, Dictionary<string, string>>
                {
                    { "ServerName1", new Dictionary<string, string>() {{ "Rust-Servers.net", "ID:KEY" } } },
                    { "ServerName2", new Dictionary<string, string>() { { "Rustservers.gg", "ID:KEY" } } },
                    { "ServerName3", new Dictionary<string, string>() { { "BestServers.com", "ID:KEY" } } }
                },
                VoteSitesAPI = new Dictionary<string, Dictionary<string, string>>
                {
                    { "Rust-Servers.net",
                       new Dictionary<string, string>() {
                           { PluginSettings.apiClaim, "http://rust-servers.net/api/?action=custom&object=plugin&element=reward&key={0}&steamid={1}" },
                           { PluginSettings.apiStatus, "http://rust-servers.net/api/?object=votes&element=claim&key={0}&steamid={1}" },
                           { PluginSettings.apiLink, "http://rust-servers.net/server/{0}" }
                       }
                    },
                    { "Rustservers.gg",
                       new Dictionary<string, string>() {
                           { PluginSettings.apiClaim, "https://rustservers.gg/vote-api.php?action=claim&key={0}&server={2}&steamid={1}" },
                           { PluginSettings.apiStatus, "https://rustservers.gg/vote-api.php?action=status&key={0}&server={2}&steamid={1}" },
                           { PluginSettings.apiLink, "https://rustservers.gg/server/{0}" }
                       }
                    },
                    { "BestServers.com",
                       new Dictionary<string, string>() {
                           { PluginSettings.apiClaim, "https://bestservers.com/api/vote.php?action=claim&key={0}&steamid={1}" },
                           { PluginSettings.apiStatus, "https://bestservers.com/api/vote.php?action=status&key={0}&steamid={1}" },
                           { PluginSettings.apiLink, "https://bestservers.com/server/{0}" }
                       }
                    },
                },
                Rewards = new Dictionary<string, List<string>>
                {
                    { "first", new List<string>() { "oxidegrantperm: kits.starterkit" } },
                    { "@", new List<string>() { "supply.signal: 1", "zlvl-*: 1" } },
                    { "vote3", new List<string>() { "oxidegrantgroup: member" } },
                    { "vote6", new List<string>() { "money: 500", "tempaddgroup: vip-1d1h1m" } },
                    { "vote10", new List<string>() { "money: 1000", "rp: 50", "tempgrantperm: fauxadmin.allowed-5m" } }
                },
                Commands = new Dictionary<string, string>
                {
                    ["money"] = "deposit {playerid} {value}",
                    ["rp"] = "sr add {playerid} {value}",
                    ["oxidegrantperm"] = "oxide.grant user {playerid} {value}",
                    ["oxiderevokeperm"] = "oxide.revoke user {playerid} {value}",
                    ["oxidegrantgroup"] = "oxide.usergroup add {playerid} {value}",
                    ["oxiderevokegroup"] = "oxide.usergroup remove {playerid} {value}",
                    ["tempaddgroup"] = "addgroup {playerid} {value} {value2}",
                    ["tempgrantperm"] = "grantperm {playerid} {value} {value2}",
                    ["zlvl-c"] = "zl.lvl {playerid} C +{value}",
                    ["zlvl-wc"] = "zl.lvl {playerid} WC +{value}",
                    ["zlvl-m"] = "zl.lvl {playerid} M +{value}",
                    ["zlvl-s"] = "zl.lvl {playerid} S +{value}",
                    ["zlvl-*"] = "zl.lvl {playerid} * +{value}",
                }
            };
            return defaultConfig;
        }
        #endregion

        private bool configChanged;
        private PluginConfig _config;

        protected override void LoadDefaultConfig() => Config.WriteObject(DefaultConfig(), true);

        class PluginSettings
        {
            public const string apiClaim = "API Claim Reward (GET URL)";
            public const string apiStatus = "API Vote status (GET URL)";
            public const string apiLink = "Vote link (URL)";
            public const string Title = "Title";
            public const string WebhookURL = "Discord webhook (URL)";
            public const string DiscordEnabled = "DiscordMessage Enabled (true / false)";
            public const string Alert = "Enable @here alert (true / false)";
            public const string Prefix = "Prefix";
            public const string LogEnabled = "Enable logging => oxide/logs/EasyVote (true / false)";
            public const string RewardIsCumulative = "Vote rewards cumulative (true / false)";
            public const string GlobalChatAnnouncments = "Globally announcment in chat when player voted (true / false)";
            public const string LocalChatAnnouncments = "Send thank you message to player who voted (true / false)";
        }
        class PluginConfig
        {
            public Dictionary<string, string> Settings { get; set; }
            public Dictionary<string, string> Discord { get; set; }
            public Dictionary<string, Dictionary<string, string>> Servers { get; set; }
            public Dictionary<string, Dictionary<string, string>> VoteSitesAPI { get; set; }
            public Dictionary<string, List<string>> Rewards { get; set; }
            public Dictionary<string, string> Commands { get; set; }
        }
        void LoadConfigValues()
        {
            // Load config file
            _config = Config.ReadObject<PluginConfig>();
            var defaultConfig = DefaultConfig();

            try
            {
                // Try merge config
                Merge(_config.Settings, defaultConfig.Settings);
                Merge(_config.Discord, defaultConfig.Discord);
                Merge(_config.Servers, defaultConfig.Servers, true);
                Merge(_config.VoteSitesAPI, defaultConfig.VoteSitesAPI, true);
                Merge(_config.Rewards, defaultConfig.Rewards, true);
                Merge(_config.Commands, defaultConfig.Commands, true);
            }
            catch
            {
                // Print warning
                PrintWarning($"Could not read oxide/config/{Name}.json, creating new config file");

                // Load default config
                LoadDefaultConfig();
                _config = Config.ReadObject<PluginConfig>();

                // Merge config again
                Merge(_config.Settings, defaultConfig.Settings);
                Merge(_config.Discord, defaultConfig.Discord);
                Merge(_config.Servers, defaultConfig.Servers, true);
                Merge(_config.VoteSitesAPI, defaultConfig.VoteSitesAPI, true);
                Merge(_config.Rewards, defaultConfig.Rewards, true);
                Merge(_config.Commands, defaultConfig.Commands, true);
            }

            // If config changed, run this
            if (!configChanged) return;
            PrintWarning("Configuration file(s) updated!");
            Config.WriteObject(_config);
        }
        void Merge<T1, T2>(IDictionary<T1, T2> current, IDictionary<T1, T2> defaultDict, bool bypass = false)
        {
            foreach (var pair in defaultDict)
            {
                if (bypass) continue;
                if (current.ContainsKey(pair.Key)) continue;
                current[pair.Key] = pair.Value;
                configChanged = true;
            }
            var oldPairs = defaultDict.Keys.Except(current.Keys).ToList();
            foreach (var oldPair in oldPairs)
            {
                if (bypass) continue;
                current.Remove(oldPair);
                configChanged = true;
            }
        }
        #endregion

        #region Helper 
        public void Chat(BasePlayer player, string str, bool prefix = true) => SendReply(player, (prefix != false ? $"{_config.Settings["Prefix"]} " : string.Empty) + str);
        public void _Debug(string msg)
        {
            if (Convert.ToBoolean(_config.Settings[PluginSettings.LogEnabled]))
                LogToFile("EasyVote", $"[{DateTime.UtcNow.ToString()}] {msg}", this);

            if (DEBUG)
                Puts($"[Debug] {msg}");
        }

        private void HelpText()
        {
            helpYou.Append("<color=#00fff7>EasyVote Commands ::</color>").AppendLine();
            helpYou.Append("<color=#fffb00>/vote</color> - Show the voting website(s)").AppendLine();
            helpYou.Append("<color=#fffb00>/claim</color> - Claim vote reward(s)").AppendLine();
            helpYou.Append("<color=#fffb00>/rewardlist</color> - Display all reward(s) what you can get from voting.");
        }

        private string CleanHTML(string input)
        {
            return Regex.Replace(input, @"<(.|\n)*?>", string.Empty);
        }

        private bool hasPermission(BasePlayer player, string perm)
        {
            if (player.IsAdmin) return true;
            if (permission.UserHasPermission(player.UserIDString, perm)) return true;
            return false;
        }

        public class Fields
        {
            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }

        private void rewardList(BasePlayer player)
        {
            StringBuilder rewardList = new StringBuilder();

            int lineCounter = 0; // Count lines
            int lineSplit = 2; // Value when split reward list.

            foreach (KeyValuePair<string, List<string>> kvp in _config.Rewards)
            {
                if (kvp.Key.ToLower() == "first")
                {
                    rewardList.Append(_lang("RewardListFirstTime", player.UserIDString)).AppendLine();

                    var valueList = String.Join(Environment.NewLine, kvp.Value.ToArray());
                    rewardList.Append(valueList).AppendLine();
                    lineCounter++;
                }

                if (kvp.Key == "@")
                {
                    rewardList.Append(_lang("RewardListEverytime", player.UserIDString)).AppendLine();

                    var valueList = String.Join(Environment.NewLine, kvp.Value.ToArray());
                    rewardList.Append(valueList).AppendLine();
                    lineCounter++;
                }

                // If lineCounter is less then lineSplit.
                if (lineCounter <= lineSplit)
                {
                    int voteNumber;
                    if (!int.TryParse(kvp.Key.Replace("vote", ""), out voteNumber))
                    {
                        if (!(kvp.Key.ToLower() != "first" || kvp.Key.ToLower() != "@"))
                            PrintWarning($"[RewardHandler] Invalid vote config format \"{kvp.Key}\"");

                        continue;
                    }
                    rewardList.Append(_lang("RewardList", player.UserIDString, voteNumber)).AppendLine();

                    var valueList = String.Join(Environment.NewLine, kvp.Value.ToArray());
                    rewardList.Append(valueList).AppendLine();
                    lineCounter++;
                }
                // If higher, then send rewardList to player and empty it.
                else
                {
                    SendReply(player, rewardList.ToString());
                    rewardList.Clear();
                    lineCounter = 0;

                    int voteNumber;
                    if (!int.TryParse(kvp.Key.Replace("vote", ""), out voteNumber))
                    {
                        if (!(kvp.Key.ToLower() != "first" || kvp.Key.ToLower() != "@"))
                            PrintWarning($"[RewardHandler] Invalid vote config format \"{kvp.Key}\"");

                        continue;
                    }

                    rewardList.Append(_lang("RewardList", player.UserIDString, voteNumber)).AppendLine();

                    var valueList = String.Join(Environment.NewLine, kvp.Value.ToArray());
                    rewardList.Append(valueList).AppendLine();
                }
            }

            // This section is for making sure all rewards will be displayed.
            SendReply(player, rewardList.ToString());
        }

        private void BuildNumberMax()
        {
            foreach (KeyValuePair<string, List<string>> kvp in _config.Rewards)
            {
                // Ignore @ and first
                if (kvp.Key == "@")
                    continue;
                if (kvp.Key.ToLower() == "first")
                    continue;

                // If key contains "vote"
                if (kvp.Key.ToLower().Contains("vote"))
                {
                    int rewardNumber;

                    // Remove alphabetic and leave only number.
                    if (!int.TryParse(kvp.Key.Replace("vote", ""), out rewardNumber))
                    {
                        Puts($"Invalid vote config format \"{kvp.Key}\"");
                        continue;
                    }
                    numberMax.Add(rewardNumber);
                }
            }
        }

        private void voteList()
        {
            List<string> temp = new List<string>();

            foreach (var site in availableAPISites.ToList())
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> kvp in _config.Servers)
                {
                    foreach (KeyValuePair<string, string> vp in kvp.Value)
                    {
                        // Null checking
                        if (!vp.Value.Contains(":"))
                        {
                            _Debug($"{kvp.Key} {vp.Key} does NOT contains ID or Key !!!");
                            continue;
                        }
                        else if (vp.Value.Split(':')[0] == "ID")
                        {
                            _Debug($"{kvp.Key} {vp.Key} does NOT contains ID !!!");
                            continue;
                        }
                        else if (vp.Value.Split(':')[1] == "KEY")
                        {
                            _Debug($"{kvp.Key} {vp.Key} does NOT contains KEY !!!");
                            continue;
                        }

                        if (vp.Key == site)
                        {
                            string[] idKeySplit = vp.Value.Split(':');
                            foreach (KeyValuePair<string, string> SitesApi in _config.VoteSitesAPI[site])
                            {
                                if (SitesApi.Key == PluginSettings.apiLink)
                                {
                                    _Debug($"Added {String.Format(SitesApi.Value, idKeySplit[0])} to the stringbuilder list.");
                                    temp.Add($"<color=#C0C0C0>{String.Format(SitesApi.Value, idKeySplit[0])}</color>");
                                }
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < temp.Count; i++)
            {
                _voteList.Append(temp[i]);
                if (i != (temp.Count - 1))
                    _voteList.AppendLine();
            }
        }

        private void checkVoteSites()
        {
            // Double check that VoteSitesAPI isnt null
            if (_config.VoteSitesAPI.Count == 0)
            {
                PrintWarning("VoteSitesAPI is null in oxide/config/EasyVote.json !!!");
                return;
            }

            // Add key names to List<String> availableSites
            foreach (KeyValuePair<string, Dictionary<string, string>> kvp in _config.VoteSitesAPI)
            {
                bool pass = true;
                foreach (KeyValuePair<string, string> vp in kvp.Value)
                {
                    if (string.IsNullOrEmpty(vp.Value))
                    {
                        pass = false;
                        PrintWarning($"In '{kvp.Key}' value '{vp.Key}' is null (oxide/config/EasyVote.json). Disabled: {kvp.Key}");
                        continue;
                    }
                }

                if (pass)
                {
                    _Debug($"Added {kvp.Key} to the \"availableSites\" list");
                    availableAPISites.Add(kvp.Key);
                }
            }
        }
        #endregion

        #region API
        // Output : string() UserID;
        // If there multiple player has same vote value, include them all in one string.
        // Multiple player output format: userid,userid,userid .. etc
        private string getHighestvoter()
        {
            // Helppers
            string output = string.Empty;
            int tempValue = 0;
            Dictionary<string, int> tempList = new Dictionary<string, int>();

            // Receive EasyVote StoreData and save it to tempList.
            foreach (KeyValuePair<string, PlayerData> item in _storedData.Players)
                tempList.Add(item.Key, item.Value.voted);

            // Loop tempList
            foreach (var item in tempList.OrderByDescending(key => key.Value))
            {
                // Run once
                if (tempValue == 0)
                {
                    output = item.Key;
                    tempValue = item.Value;
                    continue;
                }

                if (tempValue != 0)
                {
                    // If tempValue match.
                    if (item.Value == tempValue)
                    {
                        output += $",{item.Key}";
                    }
                    continue;
                }
            }
            return output;
        }
        
        // Output : int() Vote;
        private int getPlayerVotes(string steamID)
        {
            int voted = 0;
            if (_storedData.Players.ContainsKey(steamID))
                voted = _storedData.Players[steamID].voted;

            return voted;
        }

        // Output : string() UserID;
        private string getLastvoter()
        {
            string output = string.Empty;
            Dictionary<string, DateTime> tempList = new Dictionary<string, DateTime>();

            foreach (KeyValuePair<string, PlayerData> item in _storedData.Players)
                tempList.Add(item.Key, item.Value.lastTime_Voted);

            foreach (var item in tempList.OrderBy(x => x.Value).Take(1))
                output = item.Key;

            return output;
        }

        // Output : Only console message.
        private void resetPlayerVotedData(string steamID, bool displayMessage = true)
        {
            // Null checks
            if (string.IsNullOrEmpty(steamID))
                return;

            if (!_storedData.Players.ContainsKey(steamID))
                return;

            // Reset voted data
            int old = _storedData.Players[steamID].voted;
            _storedData.Players[steamID].voted = 0;
            Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData);

            // Print console message
            if (displayMessage)
                Puts($"Player '{steamID}' vote(s) data has been reseted from {old} to 0.");
        }

        // Output : Only console message.
        private void resetData(bool backup = true)
        {
            string currentTime = DateTime.UtcNow.ToString("dd-MM-yyyy");

            // Backup
            if (backup)
                Interface.GetMod().DataFileSystem.WriteObject($"EasyVote-{currentTime}.bac", _storedData);

            // Set new storedata
            _storedData = new StoredData();

            // Write wiped data
            Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData);

            Puts($"Storedata reseted, backup made in oxide/data/EasyVote-{currentTime}.bac");
        }
        #endregion
    }
}
