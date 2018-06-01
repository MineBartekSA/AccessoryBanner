using System;
using Terraria;
using TShockAPI;
using System.Linq;
using TShockAPI.DB;
using TerrariaApi.Server;
using System.Collections.Generic;

namespace AccesoryBanner
{
    [ApiVersion(2, 1)]
    public class AccessoryBanner : TerrariaPlugin
    {
        public override String Name => "AccessoryBanner";
        public override String Author => "MineBartekSA";
        public override Version Version => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        public override String Description => "Bans accessories for players in PVP!";

        IList<int> pvpBannedItems = new List<int>();
        IList<TSPlayer> pvPlayers = new List<TSPlayer>();
        IList<TSPlayer> freezeNoFreeze = new List<TSPlayer>();

        public AccessoryBanner(Main game) : base(game)
        {
            // LOL
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameUpdate.Register(this, Update);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            TShockAPI.Hooks.PlayerHooks.PlayerCommand += OnPlayerCommand;
            GetDataHandlers.TogglePvp += TPVP;

            SQLInit();

            Commands.ChatCommands.Add(new Command("accessorybanner.admin", OnCommand, "accessorybanner", "ab", "accban"));
            Commands.ChatCommands.Add(new Command("accessorybanner.nofreeze", OnFreezeNoFreeze, "accessorybannerfreeze", "abf", "abfreeze", "accbanfreeze"));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, Update);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                TShockAPI.Hooks.PlayerHooks.PlayerCommand -= OnPlayerCommand;
                GetDataHandlers.TogglePvp -= TPVP;
            }
        }

        void OnCommand(CommandArgs args)
        {
            if (args.Parameters.Count == 0)
            {
                TSPlayer p = args.Player;
                p.SendSuccessMessage("AccessoryBanner v" + Version.ToString());
                if (freezeNoFreeze.Contains(args.Player))
                    args.Player.SendInfoMessage("You have allowed AccessoryBanner to have an effect on you!");
                else if (args.Player.HasPermission("accessorybanner.nofreeze"))
                    args.Player.SendInfoMessage("You haven't allowed AccessoryBanner to have an effect on you!");
                p.SendInfoMessage("Usage: accessorybanner <item> [<item> ...] - Bans or unbans accessories for those who are in pvp");
                p.SendInfoMessage("To list bannded accessories use: accessorybanner list");
            }
            else
            {
                if (args.Parameters[0].ToLower() == "list")
                {
                    if (pvpBannedItems.Count != 0)
                    {
                        args.Player.SendSuccessMessage("Items banned for players in PVP mode:");
                        pvpBannedItems.ForEach((i) => args.Player.SendInfoMessage("- " + GetItemHoverNameFromID(i)));
                    }
                    else
                        args.Player.SendSuccessMessage("No banned accessories for players in PVP");
                }
                else
                {
                    IList<Item> toAdd = FindItems(args.Parameters, args.Player);
                    if(toAdd != null)
                    {
                        toAdd.ForEach((i) =>
                        {
                            if(pvpBannedItems.Contains(i.netID))
                            {
                                pvpBannedItems.Remove(i.netID);
                                RemoveFormDB(i.netID);
                                args.Player.SendSuccessMessage("Unbanned " + i.HoverName + " for players in PVP!");
                            }
                            else
                            {
                                pvpBannedItems.Add(i.netID);
                                AddToDB(i.netID);
                                args.Player.SendSuccessMessage("Banned " + i.HoverName + " for players in PVP!");
                            }
                        });
                    }
                }
            }
        }

        void OnFreezeNoFreeze(CommandArgs args)
        {
            if(freezeNoFreeze.Contains(args.Player))
            {
                freezeNoFreeze.Remove(args.Player);
                args.Player.SendSuccessMessage("AccessoryBanner now have no effect on you!");
            }
            else
            {
                freezeNoFreeze.Add(args.Player);
                args.Player.SendSuccessMessage("AccessoryBanner now have an effect on you!");
            }
        }

        void OnLeave(LeaveEventArgs args)
        {
            if (TShock.Players[args.Who] == null)
                return;
            if (pvPlayers.Contains(TShock.Players[args.Who]))
                pvPlayers.Remove(TShock.Players[args.Who]);
            if (freezeNoFreeze.Contains(TShock.Players[args.Who]))
                freezeNoFreeze.Remove(TShock.Players[args.Who]);
        }

        void TPVP(object sender, GetDataHandlers.TogglePvpEventArgs args)
        {
            if (TShock.Players[args.PlayerId] == null)
                return;
            if(args.Pvp && !pvPlayers.Contains(TShock.Players[args.PlayerId]))
            {
                pvPlayers.Add(TShock.Players[args.PlayerId]);
                args.Handled = true;
            }
            else if(!args.Pvp && pvPlayers.Contains(TShock.Players[args.PlayerId]))
            {
                pvPlayers.Remove(TShock.Players[args.PlayerId]);
                args.Handled = true;
            }
        }

        void OnPlayerCommand(TShockAPI.Hooks.PlayerCommandEventArgs args)
        {
            if (args.Player == null || !args.Player.IsLoggedIn)
                return;

            if(CheckIfPVPCommandExists() && args.CommandName == "pvp")
            {
                if (!args.Player.TPlayer.hostile && !pvPlayers.Contains(args.Player))
                    pvPlayers.Add(args.Player);
                else if (args.Player.TPlayer.hostile && pvPlayers.Contains(args.Player))
                    pvPlayers.Remove(args.Player);
            }
        }

        void Update(EventArgs args)
        {
            if(pvPlayers.Count != 0 && pvpBannedItems.Count != 0)
            {
                foreach (TSPlayer p in pvPlayers)
                {
                    if(!p.HasPermission("accessorybanner.nofreeze") || freezeNoFreeze.Contains(p))
                    {
                        p.Accessories.ForEach((i) =>
                        {
                            if (pvpBannedItems.Contains(i.netID))
                            {
                                p.SendErrorMessage("Accessory " + i.HoverName + " is banned in PVP mode!");
                                FreezePlayer(p);
                            }
                        });
                    }
                }
            }
        }

        void FreezePlayer(TSPlayer player)
        {
            player.SetBuff(47, 60, true); //frozen debuff (Can't move)
            player.SetBuff(156, 60, true); //stoned debuff (Can't move)
            player.SetBuff(149, 60, true); //webbed debuff (Can't move)
        }

        IList<Item> FindItems(IList<String> items, TSPlayer p)
        {
            IList<Item> i = new List<Item>();
            bool fail = false;
            items.ForEach((iStr) =>
            {
                if (fail)
                    return;
                IList<Item> found = TShock.Utils.GetItemByIdOrName(iStr);
                if (found.Count == 0)
                {
                    p.SendErrorMessage("Invalid item name!");
                    fail = true;
                    return;
                }
                else if (found.Count > 1)
                {
                    string itemss = "";
                    int count = 0;
                    found.ForEach((s) => { if (s.accessory) { itemss += s.Name + "(" + s.netID + "), "; count++; } });
                    itemss.Remove(itemss.Length - 2);
                    if (itemss != "")
                    {
                        if(count == 1)
                        {
                            p.SendErrorMessage("Could not find '" + iStr + "' but found:");
                            p.SendInfoMessage(itemss);
                            p.SendErrorMessage("Try with this");
                        }
                        else
                        {
                            p.SendErrorMessage("For '" + iStr + "' found more than one accessory!");
                            p.SendInfoMessage("Found: " + itemss);
                            p.SendErrorMessage("Plese choose one!");
                        }
                    }
                    else
                        p.SendErrorMessage("No accessories found!");
                    fail = true;
                    return;
                }
                else
                {
                    if (!found[0].accessory)
                    {
                        p.SendErrorMessage("No Accessories found for '" + iStr + "'!");
                        fail = true;
                        return;
                    }
                    else
                        i.Add(found[0]);
                }  
            });
            if (!fail)
                return i;
            else
                return null;
        }

        string GetItemHoverNameFromID(int netID)
        {
            return TShock.Utils.GetItemById(netID).HoverName;
        }

        bool CheckIfPVPCommandExists()
        {
            IEnumerable<Command> pvp = Commands.ChatCommands.Where((c) => c.Name == "pvp");
            return pvp.Count() != 0;
        }

        void SQLInit()
        {
            bool table = false;

            try
            {
                TShock.DB.Query("CREATE TABLE ABans(id INT32)");
            }
            catch(Exception exe)
            {
                if (exe.HResult != -2147467259)
                    throw new Exception("SQL ERROR: " + exe.HResult);
                table = true;
            }

            if(table)
            {
                QueryResult res = TShock.DB.QueryReader("SELECT * FROM ABans");
                while(res.Read())
                    pvpBannedItems.Add(res.Get<int>("id")); 
            }
        }

        void AddToDB(int netID)
        {
            if (TShock.DB.Query("INSERT INTO ABans(id) VALUES ('" + netID + "')") != 1)
                TShock.Log.Error("SQL Error while inserting to ABans");
        }

        void RemoveFormDB(int netID)
        {
            if (TShock.DB.Query("DELETE FROM ABans WHERE id='" + netID + "'") != 1)
                TShock.Log.Error("SQL Error while removing form ABans");
        }
    }
}
