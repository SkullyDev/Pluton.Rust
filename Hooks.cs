namespace Pluton.Rust
{
	using System;
	using System.Linq;
	using System.Globalization;
	using System.Collections.Generic;
	using Network;
	using UnityEngine;

	using Events;
	using Core;
	using Core.PluginLoaders;
	using Objects;
	using Logger = Core.Logger;
	using Steamworks;

	public class Hooks : Core.Hooks
	{
		internal static List<string> hookNames = new List<string>() {
			"On_BeingHammered",
			"On_BuildingComplete",
			"On_BuildingPartDemolished",
			"On_BuildingPartDestroyed",
			"On_BuildingPartGradeChange",
			"On_Chat",
			"On_ClientAuth",
			"On_ClientConsole",
			"On_CombatEntityHurt",
			"On_Command",
			"On_CommandPermission",
			"On_ConsumeFuel",
			"On_CorpseHurt",
			"On_DoorCode",
			"On_DoorUse",
			"On_EventTriggered",
			"On_ItemAdded",
			"On_ItemLoseCondition",
			"On_ItemPickup",
			"On_ItemRemoved",
			"On_ItemRepaired",
			"On_ItemUsed",
			"On_LandmineArmed",
			"On_LandmineExploded",
			"On_LandmineTriggered",
			"On_LootingEntity",
			"On_LootingItem",
			"On_LootingPlayer",
			"On_NetworkableKill",
			"On_NPCHurt",
			"On_NPCKilled",
			"On_Placement",
			"On_PlayerAssisted",
			"On_PlayerClothingChanged",
			"On_PlayerConnected",
			"On_PlayerDied",
			"On_PlayerDisconnected",
			"On_PlayerGathering",
			"On_PlayerHurt",
			"On_PlayerLoaded",
			"On_PlayerSleep",
			"On_PlayerStartCrafting",
			"On_PlayerSyringeOther",
			"On_PlayerSyringeSelf",
			"On_PlayerHealthChange",
			"On_PlayerTakeRadiation",
			"On_PlayerWakeUp",
			"On_PlayerWounded",
			"On_QuarryMining",
			"On_Respawn",
			"On_RocketShooting",
			"On_Shooting",
			"On_ServerConsole",
			"On_ServerInit",
			"On_ServerSaved",
			"On_ServerShutdown",
			"On_WeaponThrow"
		};

		new public void Initialize()
		{
			SetInstance<Hooks>();
			HookNames.AddRange(hookNames);
			CreateOrUpdateSubjects();
			Loaded = true;
		}
        
        #region General Hooks
        
        public static void On_BeingHammered(HitInfo info, BasePlayer ownerPlayer) => OnNext("On_BeingHammered", new HammerEvent(info, ownerPlayer));

        public static void On_CombatEntityHurt(BaseCombatEntity combatEnt, HitInfo info)
        {
            try
            {
                Assert.Test(combatEnt.isServer, "This should be called serverside only");

                if (combatEnt.IsDead())
                    return;

                using (TimeWarning.New("Hurt", 50))
                {
                    BaseNPC npc = combatEnt.GetComponent<BaseNPC>();
                    BaseCorpse corpse = combatEnt.GetComponent<BaseCorpse>();
                    BasePlayer player = combatEnt.GetComponent<BasePlayer>();

                    combatEnt.ScaleDamage(info);
                    
                    if (player != null)
                    {
                        Player p = Server.GetPlayer(player);

                        if (p.Teleporting)
                        {
                            for (int i = 0; i < info.damageTypes.types.Length; i++)
                            {
                                info.damageTypes.types[i] = 0f;
                            }
                        }

                        HurtEvent he = new PlayerHurtEvent(p, info);
                        OnNext("On_PlayerHurt", he);
                    }
                    else if (npc != null)
                    {
                        HurtEvent he = new NPCHurtEvent(new NPC(npc), info);
                        OnNext("On_NPCHurt", he);
                    }
                    else if (corpse != null)
                    {
                        HurtEvent he = new CorpseHurtEvent(corpse, info);
                        OnNext("On_CorpseHurt", he);
                    }
                    else
                    {
                        HurtEvent he = new CombatEntityHurtEvent(combatEnt, info);
                        OnNext("On_CombatEntityHurt", he);
                    }

                    if (info.PointStart != Vector3.zero)
                    {
                        DirectionProperties[] directionProperties = (DirectionProperties[]) combatEnt.GetFieldValue("propDirection");
                        for (int i = 0; i < directionProperties.Length; i++)
                        {
                            if (!(directionProperties[i].extraProtection == null))
                            {
                                if (directionProperties[i].IsWeakspot(combatEnt.transform, info))
                                {
                                    directionProperties[i].extraProtection.Scale(info.damageTypes);
                                }
                            }
                        }
                    }

                    // the DebugHurt() method
                    if (ConVar.Vis.attack)
                    {
                        if (info.PointStart != info.PointEnd)
                        {
                            ConsoleNetwork.BroadcastToAllClients("ddraw.arrow", new object[] {
                                60, Color.cyan, info.PointStart, info.PointEnd, 0.1
                            });

                            ConsoleNetwork.BroadcastToAllClients("ddraw.sphere", new object[] {
                                60, Color.cyan, info.HitPositionWorld, 0.05
                            });
                        }

                        string text = String.Empty;

                        for (int i = 0; i < info.damageTypes.types.Length; i++)
                        {
                            float num = info.damageTypes.types[i];
                            if (num != 0)
                            {
                                string text2 = text;
                                text = String.Concat(new string[] {
                                    text2, " ", ((global::Rust.DamageType)i).ToString().PadRight(10), num.ToString("0.00"), "\r\n"
                                });
                            }
                        }

                        string text3 = String.Concat(new object[] {
                            "<color=lightblue>Damage:</color>".PadRight(10),
                            info.damageTypes.Total().ToString("0.00"),
                            "\r\n<color=lightblue>Health:</color>".PadRight(10),
                            combatEnt.health.ToString("0.00"), " / ",
                            (combatEnt.health - info.damageTypes.Total() > 0) ? "<color=green>" : "<color=red>",
                            (combatEnt.health - info.damageTypes.Total()).ToString("0.00"), "</color>",
                            "\r\n<color=lightblue>Hit Ent:</color>".PadRight(10), combatEnt,
                            "\r\n<color=lightblue>Attacker:</color>".PadRight(10), info.Initiator,
                            "\r\n<color=lightblue>Weapon:</color>".PadRight(10), info.Weapon,
                            "\r\n<color=lightblue>Damages:</color>\r\n", text
                        });

                        ConsoleNetwork.BroadcastToAllClients("ddraw.text", new object[] {
                            60, Color.white, info.HitPositionWorld, text3
                        });
                    }

                    combatEnt.health -= info.damageTypes.Total();
                    combatEnt.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

                    if (ConVar.Global.developer > 1)
                    {
                        Debug.Log(string.Concat(new object[]
                        {
                            "[Combat]".PadRight(10),
                                      combatEnt.gameObject.name,
                                      " hurt ",
                                      info.damageTypes.GetMajorityDamageType(),
                                      "/",
                                      info.damageTypes.Total(),
                                      " - ",
                                      combatEnt.health.ToString("0"),
                                      " health left"
                        }));
                    }

                    combatEnt.lastDamage = info.damageTypes.GetMajorityDamageType();
                    combatEnt.lastAttacker = info.Initiator;

                    BaseCombatEntity baseCombatEntity = combatEnt.lastAttacker as BaseCombatEntity;

                    if (baseCombatEntity != null)
                        baseCombatEntity.MarkHostileTime();

                    combatEnt.SetFieldValue("_lastAttackedTime", Time.time);

                    if (combatEnt.health <= 0f)
                    {
                        combatEnt.Die(info);

                        BuildingBlock bb = combatEnt.GetComponent<BuildingBlock>();

                        if (bb != null)
                        {
                            OnNext("On_BuildingPartDestroyed", new BuildingPartDestroyedEvent(bb, info));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[Hooks] Error in CombatEntityHurt hook.");
                Logger.LogException(ex);
            }
        }

        public static void On_ConsumeFuel(BaseOven bo, Item fuel, ItemModBurnable burn) => OnNext("On_ConsumeFuel", new ConsumeFuelEvent(bo, fuel, burn));

        public static void On_EventTriggered(TriggeredEventPrefab tep)
        {
            EventTriggeredEvent ete = new EventTriggeredEvent(tep);

            OnNext("On_EventTriggered", ete);

            if (ete.Stop)
                return;

            Debug.Log("[event] " + ete.Prefab);

            BaseEntity baseEntity = GameManager.server.CreateEntity(ete.Prefab);

            if (baseEntity)
                baseEntity.Spawn();
        }

        public static void On_NetworkableKill(BaseNetworkable bn) => OnNext("On_NetworkableKill", bn);

        public static void On_QuarryMining(MiningQuarry miningQuarry) => OnNext("On_QuarryMining", miningQuarry);

        #endregion

        #region Chat Hooks

        // ConVar.Chat.say()
        public static void On_Chat(ConsoleSystem.Arg arg)
        {
            if (arg.ArgsStr.StartsWith("\"/") && !arg.ArgsStr.StartsWith("\"/ "))
            {
                On_Command(arg);
                return;
            }

            if (!ConVar.Chat.enabled)
            {
                arg.ReplyWith("Chat is disabled.");
            }
            else
            {
                if (arg.ArgsStr == "\"\"")
                {
                    return;
                }

                BasePlayer basePlayer = arg.Player();

                if (!basePlayer || basePlayer.HasPlayerFlag(BasePlayer.PlayerFlags.ChatMute))
                {
                    return;
                }

                string str = arg.GetString(0, "text").Trim();

                if (str.Length > 128)
                    str = str.Substring(0, 128);

                if (str.Length <= 0)
                    return;

                if (basePlayer.NextChatTime < Single.Epsilon)
                {
                    basePlayer.NextChatTime = Time.realtimeSinceStartup - 30f;
                }

                if (basePlayer.NextChatTime > Time.realtimeSinceStartup)
                {
                    basePlayer.NextChatTime += 2f;
                    float num = basePlayer.NextChatTime - Time.realtimeSinceStartup;

                    ConsoleNetwork.SendClientCommand(basePlayer.net.connection, "chat.add", new object[] {
                        0,
                        "You're chatting too fast - try again in " + (num + 0.5f).ToString("0") + " seconds"
                    });

                    if (num > 120f)
                    {
                        basePlayer.Kick("Chatting too fast");
                    }
                    return;
                }

                var pChat = new ChatEvent(Server.GetPlayer(basePlayer), arg);

                OnNext("On_Chat", pChat);

                if (!pChat.AllowFormatting && pChat.FinalText.Contains("<"))
                {
                    if (pChat.FinalText.Contains("<size", CompareOptions.IgnoreCase))
                        return;

                    if (pChat.FinalText.Contains("<color", CompareOptions.IgnoreCase))
                        return;

                    if (pChat.FinalText.Contains("<material", CompareOptions.IgnoreCase))
                        return;

                    if (pChat.FinalText.Contains("<quad", CompareOptions.IgnoreCase))
                        return;

                    if (pChat.FinalText.Contains("<b>", CompareOptions.IgnoreCase))
                        return;

                    if (pChat.FinalText.Contains("<i>", CompareOptions.IgnoreCase))
                        return;
                }

                if (ConVar.Chat.serverlog)
                {
                    ServerConsole.PrintColoured(new object[] {
                        ConsoleColor.DarkYellow,
                        basePlayer.displayName + ": ",
                        ConsoleColor.DarkGreen,
                        str
                    });

                    ConVar.Server.Log("Log.Chat.txt", string.Format("{0}: {1}\n", basePlayer.userID, basePlayer.displayName, str));

                    Debug.Log(String.Format("[CHAT] {0}: {1}", basePlayer.displayName, str));
                }

                string colour = "#5af";

                if (basePlayer.IsAdmin())
                {
                    colour = "#af5";
                }

                if (DeveloperList.IsDeveloper(basePlayer))
                {
                    colour = "#fa5";
                }

                basePlayer.NextChatTime = Time.realtimeSinceStartup + 1.5f;

                var chatEntry = new ConVar.Chat.ChatEntry
                {
                    Message = pChat.FinalText,
                    UserId = basePlayer.userID,
                    Username = basePlayer.displayName,
                    Color = colour,
                    Time = Facepunch.Math.Epoch.Current
                };

                (typeof(ConVar.Chat).GetStaticFieldValue("History") as List<ConVar.Chat.ChatEntry>).Add(chatEntry);

                Facepunch.RCon.Broadcast(Facepunch.RCon.LogType.Chat, chatEntry);

                if (pChat.FinalText != "")
                {
                    Logger.ChatLog(pChat.BroadcastName, pChat.OriginalText);
                    arg.ReplyWith(pChat.Reply);

                    if (ConVar.Server.globalchat)
                    {
                        ConsoleNetwork.BroadcastToAllClients("chat.add2", basePlayer.userID, pChat.FinalText, pChat.BroadcastName, colour, 1);
                    }
                    else
                    {
                        float num = 2500;

                        foreach (BasePlayer current in BasePlayer.activePlayerList)
                        {
                            float sqrMagnitude = (current.transform.position - basePlayer.transform.position).sqrMagnitude;

                            if (sqrMagnitude <= num)
                            {
                                ConsoleNetwork.SendClientCommand(current.net.connection, "chat.add2", basePlayer.userID, pChat.FinalText, pChat.BroadcastName, colour, Mathf.Clamp01(num - sqrMagnitude + 0.2f));
                            }
                        }
                    }
                }
            }
        }
        
        public static void On_Command(ConsoleSystem.Arg arg)
        {
            Player player = Server.GetPlayer(arg.Player());
            string[] args = arg.ArgsStr.Substring(2, arg.ArgsStr.Length - 3).Replace("\\", "").Split(new string[] { " " }, StringSplitOptions.None);

            var cmd = new CommandEvent(player, args);

            // TODO: do this part in a different function to be documented
            if (cmd.Cmd == "")
                return;

            foreach (KeyValuePair<string, BasePlugin> pl in PluginLoader.GetInstance().Plugins)
            {
                ChatCommand[] commands = ((ChatCommands) pl.Value.GetGlobalObject("Commands")).getChatCommands(cmd.Cmd);

                foreach (ChatCommand chatCmd in commands)
                {
                    if (chatCmd.callback != null)
                    {
                        CommandPermissionEvent permission = new CommandPermissionEvent(player, args, chatCmd);

                        OnNext("On_CommandPermission", permission);

                        if (permission.Blocked)
                        {
                            player.Message(permission.Reply);
                            continue;
                        }

                        try
                        {
                            chatCmd.callback(cmd.Args, player);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(chatCmd.plugin.FormatException(ex));
                        }
                    }
                }
            }

            OnNext("On_Command", cmd);

            if (cmd.Reply != "")
                arg.ReplyWith(cmd.Reply);
        }

        #endregion

        #region Item Hooks

        public static void On_ItemAdded(ItemContainer ic, Item i) => OnNext("On_ItemAdded", new InventoryModEvent(ic, i));

        public static void On_ItemLoseCondition(Item i, float f) => OnNext("On_ItemLoseCondition", new ItemConditionEvent(i, f));

        public static void On_ItemPickup(CollectibleEntity ce, BaseEntity.RPCMessage msg)
        {
            if (!msg.player.IsAlive() || ce.itemList == null)
                return;

            ItemAmount[] array = ce.itemList;

            for (int i = 0; i < array.Length; i++)
            {
                ItemAmount itemAmount = array[i];
                Item item = ItemManager.Create(itemAmount.itemDef, (int) itemAmount.amount, 0);

                OnNext("On_ItemPickup", new ItemPickupEvent(ce, msg, item));

                msg.player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            }

            ce.itemList = null;

            if (ce.pickupEffect.isValid)
            {
                Effect.server.Run(ce.pickupEffect.resourcePath, ce.transform.position, ce.transform.up, null, false);
            }

            msg.player.xp.Add(global::Rust.Xp.Definitions.CollectWorldItem, ce.xpScale, ce.ShortPrefabName, 0uL);

            ce.Kill(BaseNetworkable.DestroyMode.None);
        }

        public static void On_ItemRemoved(ItemContainer ic, Item i) => OnNext("On_ItemRemoved", new InventoryModEvent(ic, i));

        public static void On_ItemRepaired(RepairBench rb, BaseEntity.RPCMessage msg) => OnNext("On_ItemRepaired", new ItemRepairEvent(rb, msg));
        
        public static void On_ItemUsed(Item item, int amountToConsume) => OnNext("On_ItemUsed", new ItemUsedEvent(item, amountToConsume));

        #endregion

        #region Construction Hooks
        
        public static void On_BuildingPartGradeChange(BuildingBlock bb, BaseEntity.RPCMessage msg)
        {
            BasePlayer player = msg.player;
            var bgrade = (BuildingGrade.Enum) msg.read.Int32();
            var bpgce = new BuildingPartGradeChangeEvent(bb, bgrade, player);
            var cg = (ConstructionGrade)bb.CallMethod("GetGrade", bpgce.Grade);

            OnNext("On_BuildingPartGradeChange", bpgce);

            if (bpgce.DoDestroy)
            {
                bpgce.Builder.Message(bpgce.DestroyReason);
                UnityEngine.Object.Destroy(bb);
                return;
            }

            if (cg == null)
                return;

            if (!bpgce.HasPrivilege)
                return;

            if (bpgce.PayForUpgrade && !(bool) bb.CallMethod("CanAffordUpgrade", bpgce.Grade, player))
                return;

            if (bb.TimeSinceAttacked() < 8f)
                return;

            if (bpgce.PayForUpgrade)
                bb.CallMethod("PayForUpgrade", cg, player);

            bb.SetGrade(bpgce.Grade);
            bb.SetHealthToMax();

            if (bpgce.Rotatable)
                bb.CallMethod("StartBeingRotatable");

            bb.SendNetworkUpdate();
            bb.CallMethod("UpdateSkin", false);

            Effect.server.Run("assets/bundled/prefabs/fx/build/promote_" + bpgce.Grade.ToString().ToLower() + ".prefab", bb, 0u, Vector3.zero, Vector3.zero);
        }

        public static void On_BuildingPartDemolished(BuildingBlock bb, BaseEntity.RPCMessage msg) => OnNext("On_BuildingPartDemolished", new BuildingPartDemolishedEvent(bb, msg.player));

        // Construiction.Common.CreateConstruction()
        public static BaseEntity On_Placement(Construction construction, Construction.Target target, bool bNeedsValidPlacement)
        {
            try
            {
                GameObject gameObject = GameManager.server.CreatePrefab(construction.fullName, default(Vector3), default(Quaternion), true);
                BuildingBlock component = gameObject.GetComponent<BuildingBlock>();
                bool flag = construction.UpdatePlacement(gameObject.transform, construction, target);

                if (bNeedsValidPlacement && !flag)
                {
                    UnityEngine.Object.Destroy(gameObject);
                    return null;
                }

                BuildingEvent be = null;

                if (component != null)
                {
                    be = new BuildingEvent(construction, target, component, bNeedsValidPlacement);

                    OnNext("On_Placement", be);
                }

                if (be != null && be.DoDestroy)
                {
                    be.Builder.Message(be.DestroyReason);
                    UnityEngine.Object.Destroy(gameObject);
                    return null;
                }

                return gameObject.GetComponent<BaseEntity>();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                return null;
            }
        }

        #endregion

        #region Trap Hooks

        public static void On_LandmineArmed(Landmine l) => OnNext("On_LandmineArmed", l);

        public static void On_LandmineExploded(Landmine l) => OnNext("On_LandmineExploded", l);

        public static void On_LandmineTriggered(Landmine landmine, BasePlayer basePlayer)
        {
            LandmineTriggerEvent landmineTriggerEvent = new LandmineTriggerEvent(landmine, basePlayer);

            OnNext("On_LandmineTriggered", landmineTriggerEvent);

            if (landmineTriggerEvent.Explode)
            {
                if (basePlayer != null)
                    landmine.SetFieldValue("triggerPlayerID", basePlayer.userID);

                landmine.SetFlag(BaseEntity.Flags.Open, true);
                landmine.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
        }

        #endregion

        #region Door Hooks

        public static void On_DoorCode(CodeLock doorLock, BaseEntity.RPCMessage rpc)
        {
            if (!doorLock.IsLocked())
                return;

            string code = rpc.read.String();
            DoorCodeEvent dc = new DoorCodeEvent(doorLock, rpc.player, code);

            OnNext("On_DoorCode", dc);

            if ((!dc.IsCorrect() || !dc.allowed) && !dc.forceAllow)
            {
                Effect.server.Run(doorLock.effectDenied.resourcePath, doorLock, 0u, Vector3.zero, Vector3.forward);
                rpc.player.Hurt(1f, global::Rust.DamageType.ElectricShock, doorLock, true);
                return;
            }

            Effect.server.Run(doorLock.effectUnlocked.resourcePath, doorLock, 0u, Vector3.zero, Vector3.forward);

            doorLock.SetFlag(BaseEntity.Flags.Locked, false);
            doorLock.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            List<ulong> whitelist = new List<ulong>();

            whitelist = (List<ulong>) doorLock.GetFieldValue("whitelistPlayers");

            if (!whitelist.Contains(rpc.player.userID))
            {
                whitelist.Add(rpc.player.userID);
                doorLock.SetFieldValue("whitelistPlayers", whitelist);
            }
        }

        // Door.RPC_CloseDoor()/RPC_OpenDoor()
        public static void On_DoorUse(Door door, BaseEntity.RPCMessage rpc, bool open)
        {
            if ((open && door.IsOpen()) || (!open && !door.IsOpen()))
                return;

            DoorUseEvent due = new DoorUseEvent(new Entity(door), Server.GetPlayer(rpc.player), open);

            OnNext("On_DoorUse", due);

            if (!due.Allow)
            {
                if (due.DenyReason != "")
                    rpc.player.SendConsoleCommand("chat.add", 0, String.Format("{0}: {1}", Server.server_message_name.ColorText("fa5"), due.DenyReason));

                return;
            }

            bool doaction = true;
            BaseLock baseLock = door.GetSlot(BaseEntity.Slot.Lock) as BaseLock;

            if (!due.IgnoreLock && baseLock != null)
            {
                doaction = open ? baseLock.OnTryToOpen(rpc.player) : baseLock.OnTryToClose(rpc.player);

                if (doaction && open && (baseLock.IsLocked() && Time.realtimeSinceStartup - (float) door.GetFieldValue("decayResetTimeLast") > 60))
                {
                    Decay.RadialDecayTouch(door.transform.position, 40, 270532608);
                    door.SetFieldValue("decayResetTimeLast", Time.realtimeSinceStartup);
                }
            }

            if (doaction)
            {
                door.SetFlag(BaseEntity.Flags.Open, open);
                door.SendNetworkUpdateImmediate(false);
                door.CallMethod("UpdateDoorAnimationParameters", false);
            }
        }

        #endregion

        #region NPC Hooks

        // BaseAnimal.Die()
        public static void On_NPCKilled(BaseNPC bnpc, HitInfo info)
        {
            if (info.Initiator != null && info.Initiator.ToPlayer() != null)
            {
                Server.GetPlayer(info.Initiator as BasePlayer).Stats.AddKill(false, true);
            }

            NPC npc = new NPC(bnpc);

            OnNext("On_NPCKilled", new Events.NPCDeathEvent(npc, info));
        }

        #endregion

        #region Player Hooks

        public static void On_PlayerAssisted(BasePlayer bp) => OnNext("On_PlayerAssisted", Server.GetPlayer(bp));
        
        public static void On_PlayerClothingChanged(PlayerInventory pi, Item i) => OnNext("On_PlayerClothingChanged", new PlayerClothingEvent(pi, i));

        public static void On_PlayerConnected(BasePlayer player)
        {
            var p = new Player(player);

            if (Server.GetInstance().OfflinePlayers.ContainsKey(player.userID))
                Server.GetInstance().OfflinePlayers.Remove(player.userID);

            if (!Server.GetInstance().Players.ContainsKey(player.userID))
                Server.GetInstance().Players.Add(player.userID, p);

            OnNext("On_PlayerConnected", p);
        }

        // BasePlayer.OnDisconnected()
        public static void On_PlayerDisconnected(BasePlayer player)
        {
            Player p = Server.GetPlayer(player);

            if (Server.GetInstance().serverData.ContainsKey("OfflinePlayers", p.SteamID))
            {
                OfflinePlayer op = (Server.GetInstance().serverData.Get("OfflinePlayers", p.SteamID) as OfflinePlayer);

                op.Update(p);

                Server.GetInstance().OfflinePlayers[player.userID] = op;
            }
            else
            {
                var op = new OfflinePlayer(p);

                Server.GetInstance().OfflinePlayers.Add(player.userID, op);
            }

            if (Server.GetInstance().Players.ContainsKey(player.userID))
                Server.GetInstance().Players.Remove(player.userID);

            OnNext("On_PlayerDisconnected", p);
        }
        
        // BasePlayer.Die()
        public static void On_PlayerDied(BasePlayer player, HitInfo info)
        {
            using (TimeWarning.New("Player.Die", 0.1f))
            {
                if (!player.IsDead())
                {
                    if (info == null)
                    {
                        info = new HitInfo();
                        info.damageTypes.Add(player.lastDamage, Single.MaxValue);
                        info.Initiator = player as BaseEntity;
                    }

                    Player victim = Server.GetPlayer(player);

                    if (!((bool) player.CallMethod("WoundInsteadOfDying", info)))
                    {
                        var pde = new PlayerDeathEvent(victim, info);

                        OnNext("On_PlayerDied", pde);

                        if (pde.Die)
                        {
                            if (info.Initiator != null)
                            {
                                PlayerStats statsV = victim.Stats;

                                if (info.Initiator is BasePlayer)
                                {
                                    Server.GetPlayer(info.Initiator as BasePlayer).Stats.AddKill(true, false);
                                    victim.Stats.AddDeath(true, false);
                                }
                                else if (info.Initiator is BaseNPC)
                                {
                                    victim.Stats.AddDeath(false, true);
                                }
                            }

                            if (!pde.dropLoot)
                            {
                                if (player.belt != null)
                                {
                                    var vector = new Vector3(UnityEngine.Random.Range(-2f, 2f), 0.2f, UnityEngine.Random.Range(-2f, 2f));

                                    player.belt.DropActive(vector.normalized * 3f);
                                }

                                player.inventory.Strip();
                            }

                            player.CallMethodOnBase(typeof(BaseCombatEntity), "Die", info);
                        }
                    }
                }
            }
        }

        public static void On_PlayerGathering(ResourceDispenser dispenser, BaseEntity to, ItemAmount itemAmt, float gatherDamage, float destroyFraction)
        {
            var from = (BaseEntity) dispenser.GetFieldValue("baseEntity");

            if (itemAmt.amount == 0)
            {
                return;
            }

            float num = gatherDamage / from.MaxHealth();
            float num2 = itemAmt.startAmount / (float) dispenser.GetFieldValue("startingItemCounts");
            float value = itemAmt.startAmount * num / num2;
            float num3 = Mathf.Clamp(value, 0, itemAmt.amount);
            float num4 = num3 * destroyFraction * 2;

            if (itemAmt.amount < num3 + num4)
            {
                itemAmt.amount -= destroyFraction * num3;
                num3 = itemAmt.amount;
                num4 = 0;
            }

            float amount = itemAmt.amount;

            itemAmt.amount -= num3;

            if (itemAmt.amount < 0)
            {
                itemAmt.amount = 0;
            }

            int num5 = Mathf.Clamp(Mathf.RoundToInt(num3), 0, Mathf.CeilToInt(amount));

            itemAmt.amount -= num4;

            if (itemAmt.amount < 0)
            {
                itemAmt.amount = 0;
            }

            GatherEvent ge = new GatherEvent(dispenser, from, to, itemAmt, num5);

            OnNext("On_PlayerGathering", ge);

            if (ge.Amount <= 0)
            {
                return;
            }

            Item item = ItemManager.CreateByItemID(itemAmt.itemid, ge.Amount);

            if (item == null)
            {
                return;
            }

            to.GiveItem(item, BaseEntity.GiveItemReason.ResourceHarvested);
        }
        
        public static void On_PlayerHealthChange(BasePlayer p, float f, float f2) => OnNext("On_PlayerHealthChange", new PlayerHealthChangeEvent(p, f, f2));
        
        public static void On_PlayerLoaded(BasePlayer bp) => OnNext("On_PlayerLoaded", Server.GetPlayer(bp));
        
        public static void On_PlayerSleep(BasePlayer bp) => OnNext("On_PlayerSleep", Server.GetPlayer(bp));

        // ItemCrafter.CraftItem()
        public static bool On_PlayerStartCrafting(ItemCrafter self, ItemBlueprint bp, BasePlayer owner, ProtoBuf.Item.InstanceData instanceData = null, int amount = 1, int skinID = 0)
        {
            var ce = new CraftEvent(self, bp, owner, instanceData, amount, skinID);

            OnNext("On_PlayerStartCrafting", ce);

            if (!self.CanCraft(bp, ce.Amount))
                return false;

            if (ce.Cancel)
            {
                if (ce.cancelReason != String.Empty) ce.Crafter.Message(ce.cancelReason);
                return false;
            }

            self.taskUID++;

            ItemCraftTask itemCraftTask = Facepunch.Pool.Get<ItemCraftTask>();
            itemCraftTask.blueprint = bp;
            self.CallMethod("CollectIngredients", bp, ce.Amount, owner);
            itemCraftTask.endTime = 0;
            itemCraftTask.taskUID = self.taskUID;
            itemCraftTask.owner = owner;
            itemCraftTask.instanceData = instanceData;

            if (itemCraftTask.instanceData != null)
            {
                itemCraftTask.instanceData.ShouldPool = false;
            }

            itemCraftTask.amount = ce.Amount;
            itemCraftTask.skinID = ce.SkinID;

            self.queue.Enqueue(itemCraftTask);

            if (itemCraftTask.owner != null)
            {
                itemCraftTask.owner.Command("note.craft_add", new object[] {
                    itemCraftTask.taskUID,
                    itemCraftTask.blueprint.targetItem.itemid,
                    amount
                });
            }
            return true;
        }
        
        public static void On_PlayerSyringeSelf(MedicalTool mt, BaseEntity.RPCMessage msg) => OnNext("On_PlayerSyringeSelf", new SyringeUseEvent(mt, msg, true));

        public static void On_PlayerSyringeOther(MedicalTool mt, BaseEntity.RPCMessage msg) => OnNext("On_PlayerSyringeOther", new SyringeUseEvent(mt, msg, false));
        
        // BasePlayer.UpdateRadiation()
        public static void On_PlayerTakeRadiation(BasePlayer player, float radAmount)
        {
            var ptr = new PlayerTakeRadsEvent(player, player.metabolism.radiation_level.value, radAmount);

            OnNext("On_PlayerTakeRadiation", ptr);

            player.metabolism.radiation_level.value = ptr.Next;
        }

        public static void On_PlayerWakeUp(BasePlayer bp) => OnNext("On_PlayerWakeUp", Server.GetPlayer(bp));

        public static void On_PlayerWounded(BasePlayer bp) => OnNext("On_PlayerWounded", Server.GetPlayer(bp));


        // ConnectionAuth.Approve()
        public static void On_ClientAuth(ConnectionAuth ca, Connection connection)
        {
            var ae = new AuthEvent(connection);

            OnNext("On_ClientAuth", ae);

            ConnectionAuth.m_AuthConnection.Remove(connection);

            if (!ae.Approved)
            {
                ConnectionAuth.Reject(connection, ae.Reason);
                return;
            }

            SingletonComponent<ServerMgr>.Instance.ConnectionApproved(connection);
        }

        public static void On_Respawn(BasePlayer player, Vector3 pos, Quaternion quat)
        {
            Player p = Server.GetPlayer(player);
            var re = new RespawnEvent(p, pos, quat);

            OnNext("On_Respawn", re);

            player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.HasBuildingPrivilege, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.InBuildingPrivilege, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            ++ServerPerformance.spawns;
            player.transform.position = re.SpawnPos;
            player.transform.rotation = re.SpawnRot;
            (player.GetFieldValue("tickInterpolator") as TickInterpolator).Reset(pos);
            player.SetFieldValue("lastTickTime", 0f);
            player.CancelInvoke("WoundingEnd");
            player.StopSpectating();
            player.UpdateNetworkGroup();
            player.UpdatePlayerCollider(true, false);
            player.StartSleeping();
            player.Invoke("LifeStoryStart", 0f);
            player.metabolism.Reset();

            if (re.StartHealth < Single.Epsilon)
                player.InitializeHealth(player.StartHealth(), player.StartMaxHealth());
            else
                player.InitializeHealth(re.StartHealth, player.StartMaxHealth());

            if (re.GiveDefault)
                player.inventory.GiveDefaultItems();

            if (re.WakeUp)
                player.EndSleeping();

            player.SendNetworkUpdateImmediate(false);
            player.ClearEntityQueue();
            player.ClientRPCPlayer(null, player, "StartLoading");
            player.SendFullSnapshot();
            // player.SetPlayerFlag (BasePlayer.PlayerFlags.ReceivingSnapshot, false);
            // player.ClientRPCPlayer(null, player, "FinishLoading");
        }
        
        public static void On_RocketShooting(BaseLauncher baseLauncher, BaseEntity.RPCMessage msg, BaseEntity baseEntity) => OnNext("On_RocketShooting", new RocketShootEvent(baseLauncher, msg, baseEntity));
        
        public static void On_Shooting(BaseProjectile baseProjectile, BaseEntity.RPCMessage msg) => OnNext("On_Shooting", new ShootEvent(baseProjectile, msg));

        public static void On_WeaponThrow(ThrownWeapon thrownWeapon, BaseEntity.RPCMessage msg) => OnNext("On_WeaponThrow", new WeaponThrowEvent(thrownWeapon, msg));

        #endregion

        #region Looting Hooks

        // PlayerLoot.StartLootingEntity()
        public static void On_LootingEntity(PlayerLoot playerLoot)
		{
			BasePlayer looter = playerLoot.GetComponent<BasePlayer>();
			var ele = new EntityLootEvent(playerLoot, Server.GetPlayer(looter), new Entity(playerLoot.entitySource));

			OnNext("On_LootingEntity", ele);

			if (ele.Cancel)
            {
				playerLoot.Clear();
				looter.SendConsoleCommand("chat.add", 0, String.Format("{0}: {1}", Server.server_message_name.ColorText("fa5"), ele.cancelReason));
			}
		}

        // PlayerLoot.StartLootingItem()
        public static void On_LootingItem(PlayerLoot playerLoot)
        {
            BasePlayer looter = playerLoot.GetComponent<BasePlayer>();
            var ile = new ItemLootEvent(playerLoot, Server.GetPlayer(looter), playerLoot.itemSource);

            OnNext("On_LootingItem", ile);

            if (ile.Cancel)
            {
                playerLoot.Clear();
                looter.SendConsoleCommand("chat.add", 0, String.Format("{0}: {1}", Server.server_message_name.ColorText("fa5"), ile.cancelReason));
            }
        }

        // PlayerLoot.StartLootingPlayer()
        public static void On_LootingPlayer(PlayerLoot playerLoot)
		{
			BasePlayer looter = playerLoot.GetComponent<BasePlayer>();
			var ple = new PlayerLootEvent(playerLoot, Server.GetPlayer(looter), Server.GetPlayer(playerLoot.entitySource as BasePlayer));

			OnNext("On_LootingPlayer", ple);

			if (ple.Cancel)
            {
				playerLoot.Clear();
				looter.SendConsoleCommand("chat.add", 0, String.Format("{0}: {1}", Server.server_message_name.ColorText("fa5"), ple.cancelReason));
			}
		}

        #endregion

        #region Server Hooks
        
		public static void On_ServerInit()
		{
			Server.GetInstance().SendCommand("plugins.loaded");

			if (Server.GetInstance().Loaded)
                return;

			Server.GetInstance().Loaded = true;

			OnNext("On_ServerInit");
		}

		public static void On_ServerSaved() => OnNext("On_ServerSaved");

		public static void On_ServerShutdown()
		{
			Core.Bootstrap.timers.Dispose();

			OnNext("On_ServerShutdown");

			PluginLoader.GetInstance().UnloadPlugins();

			Core.Bootstrap.SaveAll();
        }

        #endregion

        #region Console Hooks

        // ConsoleSystem.OnClientCommand()
        public static void On_ClientConsole(ConsoleSystem.Arg arg, string rconCmd)
        {
            var ce = new ClientConsoleEvent(arg, rconCmd);

            if (arg.connection != null)
            {
                OnNext("On_ClientConsole", ce);

                if (arg.Invalid)
                {
                    if (!Net.sv.IsConnected())
                    {
                        return;
                    }

                    Net.sv.write.Start();
                    Net.sv.write.PacketID(Message.Type.ConsoleMessage);
                    Net.sv.write.String(ce.Reply);
                    Net.sv.write.Send(new SendInfo(arg.connection));
                }
                else
                {
                    arg.ReplyWith(ce.Reply);
                }
            }
        }

        // ConsoleSystem.SystemRealm.Normal
        public static void On_ServerConsole(ConsoleSystem.Arg arg, string cmd)
        {
            try
            {
                if (!Core.Bootstrap.PlutonLoaded)
                    return;

                var sce = new ServerConsoleEvent(arg, cmd);

                foreach (KeyValuePair<string, BasePlugin> pl in PluginLoader.GetInstance().Plugins)
                {
                    object globalObj = pl.Value.GetGlobalObject("ServerConsoleCommands");

                    if (globalObj is ConsoleCommands)
                    {
                        ConsoleCommand[] commands = (globalObj as ConsoleCommands).getConsoleCommands(sce.Cmd);

                        foreach (ConsoleCommand cc in commands)
                        {
                            if (cc.callback == null)
                                continue;

                            try
                            {
                                cc.callback(arg.ArgsStr.Split(' '));
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(cc.plugin.FormatException(ex));
                            }
                        }
                    }
                }

                OnNext("On_ServerConsole", sce);

                if (arg.Invalid)
                {
                    Debug.Log(sce.Reply);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        #endregion

        public static void SetModded()
		{
			try
            {
                using (TimeWarning.New("UpdateServerInformation", 0.1f))
                {
                    System.Reflection.Assembly assembly = typeof(ServerMgr).Assembly;
                    byte[] byteArray = System.IO.File.ReadAllBytes(assembly.Location);
                    Ionic.Crc.CRC32 cRC = new Ionic.Crc.CRC32();

                    cRC.SlurpBlock(byteArray, 0, byteArray.Length);

                    string _AssemblyHash = cRC.Crc32Result.ToString("x");

                    SteamGameServer.SetServerName(ConVar.Server.hostname);
                    SteamGameServer.SetMaxPlayerCount(ConVar.Server.maxplayers);
                    SteamGameServer.SetPasswordProtected(false);
                    SteamGameServer.SetMapName(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

                    string gameTags = string.Format("mp{0},cp{1},qp{5},v{2}{3},h{4}", new object[] {
                        ConVar.Server.maxplayers,
                        BasePlayer.activePlayerList.Count,
                        global::Rust.Protocol.network,
                        ConVar.Server.pve ? ",pve": string.Empty,
                        pluton.enabled ? ",modded,pluton" : string.Empty,
                        _AssemblyHash,
                        SingletonComponent<ServerMgr>.Instance.connectionQueue.Queued
                    });

                    SteamGameServer.SetGameTags(gameTags);

                    string[] array = ConVar.Server.description.SplitToChunks(100).ToArray();

                    for (int i = 0; i < 16; i++)
                    {
                        if (i < array.Length)
                        {
                            SteamGameServer.SetKeyValue(string.Format("description_{0:00}", i), array[i]);
                        }
                        else
                        {
                            SteamGameServer.SetKeyValue(string.Format("description_{0:00}", i), string.Empty);
                        }
                    }

                    SteamGameServer.SetKeyValue("hash", _AssemblyHash);
                    SteamGameServer.SetKeyValue("country", SteamGameServerUtils.GetIPCountry());
                    SteamGameServer.SetKeyValue("world.seed", global::World.Seed.ToString());
                    SteamGameServer.SetKeyValue("world.size", global::World.Size.ToString());
                    SteamGameServer.SetKeyValue("pve", ConVar.Server.pve.ToString());
                    SteamGameServer.SetKeyValue("headerimage", ConVar.Server.headerimage);
                    SteamGameServer.SetKeyValue("url", ConVar.Server.url);
                    SteamGameServer.SetKeyValue("uptime", ((int)Time.realtimeSinceStartup).ToString());
                    SteamGameServer.SetKeyValue("mem_ws", Performance.report.usedMemoryWorkingSetMB.ToString());
                    SteamGameServer.SetKeyValue("mem_pv", Performance.report.usedMemoryPrivateMB.ToString());
                    SteamGameServer.SetKeyValue("gc_mb", Performance.report.memoryAllocations.ToString());
                    SteamGameServer.SetKeyValue("gc_cl", Performance.report.memoryCollections.ToString());
                    SteamGameServer.SetKeyValue("fps", Performance.report.frameRate.ToString());
                    SteamGameServer.SetKeyValue("fps_avg", Performance.report.frameRateAverage.ToString("0.00"));
                    SteamGameServer.SetKeyValue("ent_cnt", BaseNetworkable.serverEntities.Count.ToString());
                    SteamGameServer.SetKeyValue("build", BuildInformation.VersionStampDays.ToString());
                }
            }
            catch (Exception ex)
            {
				Logger.LogError("[Hooks] Error while setting the server modded.");
				Logger.LogException(ex);
			}
		}
	}
}
