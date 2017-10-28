using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using System.Text;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("EasyVote", "Exel80", "2.0.0", ResourceId = 2102)]
    [Description("Simple and smooth voting start by activating one scirpt.")]
    class EasyVote : RustPlugin
    {
        #region Initializing
        // Permissions
        private const bool DEBUG = true;
        private const string permUse = "EasyVote.Use";
        private const string permAdmin = "EasyVote.Admin";

        // Spam protect list
        Dictionary<ulong, bool> claimCooldown = new Dictionary<ulong, bool>();

        // Global bools
        private bool NoReward = true;

        // List all vote sites.
        List<string> availableAPISites = new List<string>();
        StringBuilder _voteList = new StringBuilder();
        private List<int> numberMax = new List<int>();

        void Init()
        {
            // Load configs
            LoadConfigValues();
            LoadDefaultMessages();

            _storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("EasyVote");

            // Check available vote sites
            checkVoteSites();

            // Build StringBuilders
            voteList();

            BuildNumberMax();

            // Regitering permissions
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permAdmin, this);
        }
        #endregion

        #region Localization
        string _lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ClaimError"] = "Something went wrong! Player <color=red>{0} got a error</color> from <color=yellow>{1}</color>. Please try again later!",
                ["ClaimReward"] = "You just received your vote reward(s). Enjoy!",
                ["ClaimPleaseWait"] = "Checking vote sites. Please wait...",
                ["VoteList"] = "You have voted <color=yellow>{1}</color> time! Leave your vote in these sites:\n{0}",
                ["EarnReward"] = "When you are voted. Type <color=yellow>/claim</color> to earn your reward(s)!",
                ["RewardListFirstTime"] = "<color=cyan>Reward when player vote first time.</color>",
                ["RewardListEverytime"] = "<color=cyan>Reward what player will receive everytime when vote.</color>",
                ["RewardList"] = "<color=cyan>Reward when player has voted</color> <color=orange>{0}</color> <color=cyan>time(s).</color>",
                ["Received"] = "You have received {0}x {1}",
                ["ThankYou"] = "Thank you for voting! You have voted {0} time(s)",
                ["NoRewards"] = "You do not have any new rewards available" +
                "\n Please type <color=yellow>/vote</color> and go to the website to vote and receive your reward",
                ["RemeberClaim"] = "You haven't yet claimed your reward from voting server! Use <color=cyan>/claim</color> to claim your reward!" +
                "\n You have to claim your reward in <color=yellow>24h</color>! Otherwise it will be gone!",
                ["GlobalChatAnnouncments"] = "<color=yellow>{0}</color><color=cyan> has voted </color><color=yellow>{1}</color><color=cyan> time(s) and just received their rewards. Find out where to vote by typing</color><color=yellow> /vote</color>\n<color=cyan>To see a list of avaliable rewards type</color><color=yellow> /reward list</color>",
                ["money"] = "{0} has been desposited into your account",
                ["rp"] = "You have gained {0} reward points",
                ["tempaddgroup"] = "You have been temporality added to {0} group (Expire in {1})",
                ["tempgrantperm"] = "You have temporality granted {0} permission (Expire in {1})",
                ["zlvl-wc"] = "You have gained {0} woodcrafting level(s)",
                ["zlvl-m"] = "You have gained {0} mining level(s)",
                ["zlvl-s"] = "You have gained {0} skinning level(s)",
                ["zlvl-c"] = "You have gained {0} crafting level(s)",
                ["zlvl-*"] = "You have gained {0} in all level(s)",
                ["oxidegrantperm"] = "You have granted {0} permission",
                ["oxiderevokeperm"] = "You have revoked {0} permission",
                ["oxidegrantgroup"] = "You have been added to {0} group",
                ["oxiderevokegroup"] = "You have been removed from {0} group"
            }, this);
        }
        #endregion

        #region Commands
        [ChatCommand("vote")]
        void cmdVote(BasePlayer player, string command, string[] args)
        {
            // Check how many time player has voted.
            int voted = 0;
            if (_storedData.Players.ContainsKey(player.UserIDString))
                voted = _storedData.Players[player.UserIDString].voted;

            Chat(player, _lang("VoteList", player.UserIDString, _voteList.ToString(), voted));
        }

        [ChatCommand("claim")]
        void cmdClaim(BasePlayer player, string command, string[] args)
        {
            // Check if player exist in cooldown list or not
            if (!claimCooldown.ContainsKey(player.userID))
                claimCooldown.Add(player.userID, false);
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
                        if (vp.Key == site)
                        {
                            string[] idKeySplit = vp.Value.Split(':');
                            foreach (KeyValuePair<string, string> SitesApi in _config.VoteSitesAPI[site])
                            {
                                if (SitesApi.Key == PluginSettings.apiClaim)
                                {
                                    // Formating api claim => {0} = Key & {1} Id
                                    // Example: "http://rust-servers.net/api/?action=custom&object=plugin&element=reward&key= {0} &steamid= {1} ",
                                    string _format = String.Format(SitesApi.Value, idKeySplit[1], player.userID);

                                    // Send GET request to voteAPI site.
                                    webrequest.Enqueue(_format, null, (code, response) => ClaimReward(code, response, player, site), this, RequestMethod.GET, null, timeout);

                                    _Debug($"GET: {_format} =>\n Site: {site} Server: {kvp.Key} Id: {idKeySplit[0]}");
                                }
                            }
                        }
                    }
                }
            }

            // Wait 5.55 sec before execute this command.
            // Because need make sure that plugin webrequest all api sites.
            timer.Once(5.55f, () =>
            {
                if (!claimCooldown[player.userID])
                {
                    Chat(player, $"{_lang("NoRewards", player.UserIDString)}");
                }

                // Remove player from cooldown list
                claimCooldown.Remove(player.userID);
            });
        }

        [ChatCommand("reward")]
        void cmdReward(BasePlayer player, string command, string[] args)
        {
            if (args?.Length > 1)
                return;

            if (args[0] == "list")
                rewardList(player);

            //TODO: Maybe change if to switch? And add usage in lang?
        }
        #endregion

        #region Reward Handler
        private void RewardHandler(BasePlayer player)
        {
            var info = new PlayerData(player);

            // Check that player is in "database".
            if (!_storedData.Players.ContainsKey(info.id))
            {
                _storedData.Players.Add(info.id, info);
                Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData);
            }

            // Add +1 vote to player.
            _storedData.Players[info.id].voted++;
            Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData);

            // Get how many time player has voted.
            int voted = _storedData.Players[info.id].voted;

            // Take closest number from rewardNumbers
            int? closest = (int?)numberMax.Aggregate((x, y) => Math.Abs(x - voted) < Math.Abs(y - voted)
                    ? (x > voted ? y : x)
                    : (y > voted ? x : y));

            if (closest > voted)
            {
                _Debug($"Closest ({closest}) number was bigger then voted number ({voted})");
                _Debug($"Closest ({closest}) is now 0!");
                closest = 0;
            }

            _Debug($"Reward Number: {closest} Voted: {voted}");

            // and here the magic happens.
            foreach (KeyValuePair<string, List<string>> kvp in _config.Rewards)
            {
                if (closest != 0)
                {
                    // Loop for all rewards.
                    if (kvp.Key.ToLower() == "first")
                    {
                        _Debug("Founded 'first' in config!");

                        //TODO: Check that player vote first time & gave first time rewwrd.
                    }

                    if (kvp.Key == "@" | kvp.Key == "*")
                    {
                        _Debug("Founded 'repeat reward' in config!");

                        //TODO: Gave this reward to the player everytime.
                    }

                    if (kvp.Key.ToString() == $"vote{closest}")
                    {
                        Chat(player, $"{_lang("ThankYou", player.UserIDString, voted)}");
                        foreach (string reward in kvp.Value)
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
                                    Chat(player, $"{_lang(commmand, player.UserIDString, value)}");
                                else
                                {
                                    string[] _value = value.Split('-');
                                    Chat(player, $"{_lang(commmand, player.UserIDString, _value[0], _value[1])}");
                                }

                                _Debug($"Ran command {String.Format(commmand, value)}");
                                continue;
                            }
                            else
                            {
                                try
                                {
                                    Item itemToReceive = ItemManager.CreateByName(commmand, Convert.ToInt32(value));
                                    _Debug($"Received item {itemToReceive.info.displayName.translated} {value}");
                                    //If the item does not end up in the inventory
                                    //Drop it on the ground for them
                                    if (!player.inventory.GiveItem(itemToReceive, player.inventory.containerMain))
                                        itemToReceive.Drop(player.GetDropPosition(), player.GetDropVelocity());

                                    Chat(player, $"{_lang("Received", player.UserIDString, value, itemToReceive.info.displayName.translated)}");
                                }
                                catch (Exception e) { PrintWarning($"{e}"); }
                            }
                        }
                    }
                }
            }
            if (_config.Settings[PluginSettings.GlobalChatAnnouncments]?.ToLower() == "true")
                PrintToChat($"{_lang("GlobalClaimAnnouncment", player.UserIDString, player.displayName, voted)}");
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
            public string id;
            public int voted;
            public DateTime lastTime_Voted;

            public PlayerData(BasePlayer player)
            {
                id = player.UserIDString;
                voted = 0;
            }
        }
        StoredData _storedData;
        #endregion

        #region Webrequests
        void ClaimReward(int code, string response, BasePlayer player, string url)
        {
            _Debug($"Code: {code}, Response: {response}");

            if (code != 200)
            {
                PrintWarning("Error: {0} - Couldn't get an answer for {1} ({2})", code, player.displayName, url);
                Chat(player, $"{_lang("ClaimError", player.UserIDString, code, url)}");
                return;
            }

            switch (response)
            {
                case "1":
                    {
                        RewardHandler(player);
                        if (claimCooldown.ContainsKey(player.userID))
                            claimCooldown[player.userID] = true;
                    }
                    break;
            }
        }

        void CheckStatus(int code, string response, BasePlayer player)
        {
            _Debug($"Code: {code}, Response: {response}");

            if (response?.ToString() == "1" && code == 200)
                Chat(player, _lang("RemeberClaim", player.UserIDString));
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
                    { PluginSettings.Prefix, "<color=cyan>[EasyVote]</color>" },
                    { PluginSettings.AllowManualCheckReward, "true" },
                    { PluginSettings.AllowAutoRewardCheck, "true" },
                    { PluginSettings.RewardCheckTime, "60" },
                    { PluginSettings.RewardIsCumulative, "false" },
                    { PluginSettings.GlobalChatAnnouncments, "true" },
                    { PluginSettings.LocalChatAnnouncments, "true" }
                },
                Discord = new Dictionary<string, string>
                {
                    { PluginSettings.DiscordEnabled, "false" },
                    { PluginSettings.Title, "Vote" },
                    { PluginSettings.WebhookURL, "" }
                },
                Servers = new Dictionary<string, Dictionary<string, string>>
                {
                    { "ServerName1", new Dictionary<string, string>() { { "Beancan", "ID:KEY" }, { "RustServers", "ID:KEY" } } },
                    { "ServerName2", new Dictionary<string, string>() { { "Beancan", "ID:KEY" } } }
                },
                VoteSitesAPI = new Dictionary<string, Dictionary<string, string>>
                {
                    { "RustServers",
                       new Dictionary<string, string>() {
                           { PluginSettings.apiClaim, "http://rust-servers.net/api/?action=custom&object=plugin&element=reward&key={0}&steamid={1}" },
                           { PluginSettings.apiStatus, "http://rust-servers.net/api/?object=votes&element=claim&key={0}&steamid={1}" },
                           { PluginSettings.apiLink, "http://rust-servers.net/server/{0}" }
                       }
                    },
                    { "Beancan",
                       new Dictionary<string, string>() {
                           { PluginSettings.apiClaim, "http://beancan.io/vote/put/{0}/{1}" },
                           { PluginSettings.apiStatus, "http://beancan.io/vote/get/{0}/{1}" },
                           { PluginSettings.apiLink, "http://beancan.io/server/{0}" }
                       }
                    },
                },
                Rewards = new Dictionary<string, List<string>>
                {
                    { "first", new List<string>() { "oxidegrantgroup: voter" } },
                    { "@", new List<string>() { "supply.signal: 1" } },
                    { "vote3", new List<string>() { "money: 100" } },
                    { "vote6", new List<string>() { "money: 500" } },
                    { "vote10", new List<string>() { "money: 1000", "rp: 50" } }
                },
                Commands = new Dictionary<string, string>
                {
                    ["money"] = "eco.c deposit {playerid} {value}",
                    ["rp"] = "sr add {playerid} {value}",
                    ["oxidegrantperm"] = "oxide.grant user {playerid} {value}",
                    ["oxiderevokeperm"] = "oxide.revoke user {playerid} {value}",
                    ["oxidegrantgroup"] = "oxide.usergroup add {playerid} {value}",
                    ["oxiderevokegroup"] = "oxide.usergroup remove {playerid} {value}",
                    ["tempaddgroup"] = "addgroup {playerid} {value} {value2}",
                    ["tempgrantperm"] = "grantperm {playerid} {value} {value2}",
                    ["zlvl-c"] = "zlvl {playername} C +{value}",
                    ["zlvl-wc"] = "zlvl {playername} WC +{value}",
                    ["zlvl-m"] = "zlvl {playername} M +{value}",
                    ["zlvl-s"] = "zlvl {playername} S +{value}",
                    ["zlvl-*"] = "zlvl {playername} * +{value}",
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
            public const string Prefix = "Prefix";
            public const string AllowManualCheckReward = "Allow player claim reward manually with command (true / false)";
            public const string AllowAutoRewardCheck = "Timer : Check online players if someone has voted (true / false)";
            public const string RewardCheckTime = "Timer : How often timer check online players (seconds)";
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
            _config = Config.ReadObject<PluginConfig>();
            var defaultConfig = DefaultConfig();
            Merge(_config.Settings, defaultConfig.Settings);
            Merge(_config.Discord, defaultConfig.Discord);
            Merge(_config.Servers, defaultConfig.Servers, true);
            Merge(_config.VoteSitesAPI, defaultConfig.VoteSitesAPI, true);
            Merge(_config.Rewards, defaultConfig.Rewards, true);
            Merge(_config.Commands, defaultConfig.Commands, true);

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
                configChanged = true;
            }
        }
        #endregion

        #region Helper 
        public void Chat(BasePlayer player, string str) => SendReply(player, $"{_config.Settings["Prefix"]} " + str);
        public void _Debug(string msg)
        {
            if (DEBUG)
                Puts($"[Debug] {msg}");
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
                    rewardList.Append(_lang("RewardListFirstTime", null)).AppendLine();

                    var valueList = String.Join(Environment.NewLine, kvp.Value.ToArray());
                    rewardList.Append(valueList).AppendLine();
                    lineCounter++;
                }

                if (kvp.Key == "*" | kvp.Key == "@")
                {
                    rewardList.Append(_lang("RewardListEverytime", null)).AppendLine();

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
                    rewardList.Append(_lang("RewardList", null, voteNumber)).AppendLine();

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

                    rewardList.Append(_lang("RewardList", null, voteNumber)).AppendLine();
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
                if (kvp.Key == "*" | kvp.Key == "@" | kvp.Key.ToLower() == "first")
                    return;

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

        private void voteList()
        {
            List<string> temp = new List<string>();

            foreach (var site in availableAPISites.ToList())
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> kvp in _config.Servers)
                {
                    foreach (KeyValuePair<string, string> vp in kvp.Value)
                    {
                        if (vp.Key == site)
                        {
                            string[] idKeySplit = vp.Value.Split(':');
                            foreach (KeyValuePair<string, string> SitesApi in _config.VoteSitesAPI[site])
                            {
                                if (SitesApi.Key == PluginSettings.apiLink)
                                {
                                    _Debug($"Added {String.Format(SitesApi.Value, idKeySplit[0])} to the stringbuilder list.");
                                    temp.Add($"<color=silver>{String.Format(SitesApi.Value, idKeySplit[0])}</color>");
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
                        return;
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
    }
}
