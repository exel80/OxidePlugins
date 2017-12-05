using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Oxide.Plugins
{
    [Info("BlockCommandsMounted", "Exel80", "1.0.0")]
    [Description("Block commands while mounted")]
    class BlockCmdMount : RustPlugin
    {
        //FIX: OnServerCommand block all chating?

        #region Init
        // Mounted player(s) list
        List<string> MountedPlayers = new List<string>();
        #endregion

        #region Block Commands

        // Block / commands
        //private object OnUserCommand(IPlayer player, string command, string[] args)
        //{
        //    if(MountedPlayers.Contains(player.Id))
        //        Puts("True");

        //    //if (arg.connection.authlevel < 0) return null;
        //    if (MountedPlayers.Contains(player.Id)) return false;
        //    return null;
        //}

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            var connection = arg.Connection;
            if (connection == null || string.IsNullOrEmpty(arg.cmd?.FullName)) return null;
            if (!MountedPlayers.Contains(connection.userid.ToString())) return null;

            return true;
        }

        // Block ! commands (Only handy for vanilla server plugins)
        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            // Bypass admins
            //if (arg.Connection.authLevel < 0) return null;

            // Get BasePlayer
            BasePlayer player = (BasePlayer)arg.Connection.player;

            // Check is player mounted
            if (!MountedPlayers.Contains(player.UserIDString))
                return null;

            // Get message
            string message = arg.GetString(0, "text");
            Puts(message[0].ToString());

            // Check first char, if its / or !
            if (message[0] == '!')
            {
                SendReply(player, "You can NOT use command while you sit!");
                return true;
            }

            return null;
        }
        #endregion

        #region Hooks
        // Remove player from MountedPlayers list
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (MountedPlayers.Contains(player.UserIDString))
                MountedPlayers.Remove(player.UserIDString);
        }

        // Check if player mounted
        void OnEntityMounted(object mounted, BasePlayer player) => MountedPlayers.Add(player.UserIDString);

        // Check if player dismounted
        void OnEntityDismounted(object mounted, BasePlayer player) => MountedPlayers.Remove(player.UserIDString);
        #endregion
    }
}
