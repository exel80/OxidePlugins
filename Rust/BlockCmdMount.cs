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
        #region Block Commands

        // Block / commands
        private object OnUserCommand(IPlayer player, string command, string[] args)
        {
            BasePlayer _player = player.Object as BasePlayer;

            //Puts((!_player.isMounted ? false : true).ToString());

            // Bypass admins
            if (_player.IsAdmin) return null;

            // isMounted?
            if (_player.isMounted)
            {
                SendReply(_player, "You can NOT use command while sitting!");
                return false;
            }
            return null;
        }

        //private object OnServerCommand(ConsoleSystem.Arg arg)
        //{
        //    var connection = arg.Connection;
        //    Puts((!connection.player.GetComponent<BasePlayer>().isMounted ? false : true).ToString());

        //    if (connection == null || string.IsNullOrEmpty(arg.cmd?.FullName)) return null;
        //    if (!connection.player.GetComponent<BasePlayer>().isMounted ? true : false) return null;

        //    return true;
        //}

        // Block ! commands (Only handy for vanilla server commands)
        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            // Bypass admins
            if (arg.Connection.authLevel < 0) return null;

            // Get BasePlayer
            BasePlayer player = (BasePlayer)arg.Connection.player;

            // Check is player mounted
            if (!player.isMounted) return null;

            // Get message
            string message = arg.GetString(0, "text");

            // Check first char, if its / or !
            if (message[0] == '!')
            {
                SendReply(player, "You can NOT use command while sitting!");
                return false;
            }

            return null;
        }
        #endregion
    }
}
