//Custom PrefabName Layout
/*
*  Identifiyer Seperated by "."
*  BMGBOT.Kitname.Stationary.Health.AttackRange.Roamrange.Cooldown.Replaceitems.Parachute.Name
*  
*  Kitname = the name of the kit to put on the bot. 0 for no kit
*  Stationary = 0 false / 1 true. Not move from spawner unless triggered
*  Health = how much health bot has, 0 for default
*  AttackRange = How far before bot attacks triggers
*  Roamrange = How far the bot can get from its spawner before it returns
*  Cooldown = How long with no activity before it checks roam range.
*  Replaceitems = 0 false / 1 true, will replace default corpse loot with kit loot.
*  Parachute = 0 false / 1 true , will parachute to navmesh
*  Name = custom display name to show on bot leave blank for random name (BMGBOT.Kitname.Stationary.Health.AttackRange.Roamrange.Cooldown.Replaceitems.Parachute)
*  
* Example: BMGBOT.autokit.0.500.30.50.30.1.0.Starter Bot
* That makes that spawner create a bots wearing autokit,
* They arent stationary, They Have 500HP, Can shoot upto 30f
* Wont return home when within 50f (if player within attack range it wont return home)
* Does Home check after 30 ticks of no activity.
* Replaces the default corpse with kit items.
* Give bot name of Starter Bot.
*/

//Rust Edits Bot Types
/*
 * scientistnpc_roam
 * scientistnpc_peacekeeper
 * scientistnpc_heavy
 * scientistnpc_junkpile_pistol
 * npc_bandit_guard
 * scarecrow
 */
using Facepunch;
using Oxide.Core.Plugins;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
namespace Oxide.Plugins
{
    [Info("RECustomBots", "bmgjet", "1.0.0")]
    [Description("Fixes bots created with NPC_Spawner in rust edit")]
    public class RECustomBots : RustPlugin
    {
        public bool Debug = false;
        //Prefab placeholder
        string prefabplaceholder = "assets/prefabs/deployable/playerioents/gates/randswitch/electrical.random.switch.deployed.prefab";
        //Bot Tick Rate
        float bot_tick = 1.2f;
        //Stored location of spawner and its settings
        Dictionary<Vector3, BotsSettings> NPC_Spawners = new Dictionary<Vector3, BotsSettings>();
        //Stored bot ID and what location to get settings from
        Dictionary<ulong, Vector3> NPC_Bots = new Dictionary<ulong, Vector3>();
        //Store bots items
        Dictionary<ulong, List<Botsinfo>> NPC_Items = new Dictionary<ulong, List<Botsinfo>>();
        //Parachute Collider
        CapsuleCollider paracol;
        //Check if fully started.
        private bool HasLoadedSpawner = false;
        //reference to kits plugin
        [PluginReference]
        private Plugin Kits;
        //reference to self
        private static RECustomBots plugin;
        //Info on items held by bot.
        private class Botsinfo
        {
            public ItemDefinition idef;
            public int item_ammount;
            public ulong item_skin;
        }
        //Settings of the bot.
        private class BotsSettings
        {
            public string kitname = "";
            public bool stationary = false;
            public int health = 0;
            public float AttackRange = 0.8f;
            public int roamrange = 20;
            public int cooldown = 30;
            public bool replaceitems = true;
            public bool parachute = false;
            public string name = "";
        }
        //Bot script
        public class BMGBOT : MonoBehaviour
        {
            //vars
            int LastInteraction = 0;
            int FailedHomes = 0;
            bool flying = true;
            int RoamDistance;
            int Cooldown;
            Vector3 Home;
            string Kitname;
            string Name;
            int Health;
            float AttackRange;
            bool Parachute;
            bool Stationary;
            bool replaceitems;
            //Reference
            NPCPlayer bot;
            private void Awake()
            {
                //Sets up
                bot = this.GetComponent<NPCPlayer>();
                //Check if its a bot from spawner
                if (plugin.NPC_Bots.ContainsKey(bot.userID))
                {
                    //Set a home point
                    Home = plugin.NPC_Bots[bot.userID];
                    var col = bot.gameObject.AddComponent<BoxCollider>();
                    col.size = new Vector3(1, 1f, 1);
                    //Load settings
                    BotsSettings settings = plugin.NPC_Spawners[plugin.NPC_Bots[bot.userID]];
                    //Check there are settings
                    if (settings != null)
                    {
                        RoamDistance = settings.roamrange;
                        Cooldown = settings.cooldown;
                        Kitname = settings.kitname;
                        Name = settings.name;
                        Health = settings.health;
                        AttackRange = settings.AttackRange;
                        Parachute =  settings.parachute;
                        Stationary = settings.stationary;
                        replaceitems = settings.replaceitems;
                    }
                    else
                    {
                        //No settings to act default
                        return;
                    }

                    //Set bots name
                    plugin.BotName(bot, Name);
                    //Set bot health
                    if (Health != 0)
                    {
                        plugin.BotHealth(bot, Health);
                    }
                    //Stop bot moving until its activated
                    bot.NavAgent.isStopped = Stationary;
                    bot.SetPlayerFlag(BasePlayer.PlayerFlags.NoSprint, Stationary);
                    //Sets kit onto bot if ones set
                    if (Kitname != "")
                    {
                        plugin.BotSkin(bot, Kitname);
                    }
                    //Stops attack range being completely 0 or negitive
                    if (AttackRange <= 0)
                    {
                        AttackRange = 0.8f;
                    }
                    //Adds parachute to spawning
                    if (Parachute)
                    {
                        flying = true;
                        plugin.paracol = bot.GetComponent<CapsuleCollider>();
                        if (plugin.paracol != null)
                        {
                            plugin.paracol.isTrigger = true;
                            bot.GetComponent<CapsuleCollider>().radius += 2f;
                        }
                        plugin.AddParaChute(bot, Home + new Vector3(0, 100, 0));
                    }
                    else
                    {
                        ClipGround();
                    }
                    if (plugin.Debug) plugin.Puts("Bot " + bot.displayName + " spawned,Health:" + bot.health + " Kit:" + Kitname + " Range:" + AttackRange.ToString() + " Roam:" + RoamDistance.ToString() + " Cooldown:" + Cooldown.ToString() + " Default Items:" + replaceitems + " Stationary:" + Stationary.ToString() + " Parachute:" + Parachute);
                    bot.SendNetworkUpdateImmediate();
                    //Setup repeating script
                    InvokeRepeating("_tick", 5, plugin.bot_tick);
                }
            }

            private void ClipGround()
            {
                var rb = bot.gameObject.GetComponent<Rigidbody>();
                NavMeshHit hit;
                if (NavMesh.SamplePosition(bot.transform.position, out hit, 20, -1))
                {
                    if (bot.WaterFactor() > 0.9f)
                    {
                        bot.Kill();
                        return;
                    }
                    if (plugin.paracol != null)
                    {
                        plugin.paracol.isTrigger = false;
                        bot.GetComponent<CapsuleCollider>().radius -= 2f;
                    }
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    bot.gameObject.layer = 17;
                    bot.ServerPosition = hit.position -= new Vector3(0,0.2f,0);
                    bot.NavAgent.Move(bot.ServerPosition);
                    foreach (var child in bot.children.Where(child => child.name.Contains("parachute")))
                    {
                        child.SetParent(null);
                        child.Kill();
                        break;
                    }
                }
                else
                {
                    rb.useGravity = true;
                    rb.velocity = new Vector3(bot.transform.forward.x * 15, 11, bot.transform.forward.z * 15);
                    rb.drag = 1f;
                }
                flying = false;
            }

            //Collider to remove parachute
            private void OnCollisionEnter(Collision col)
            {
                //Ignore collider if not flying
                if (!flying)
                    return;

                ClipGround();
            }

            void _tick()
            {
                //Check if bot exsists
                if (bot == null)
                {
                    return;
                }
                //Dont run check if bot is downed
                if (!bot.IsCrawling() && bot.IsAlive())
                {
                    if(Stationary)
                    {
                        if (Vector3.Distance(bot.transform.position, Home) > 1f)
                        {
                            bot.NavAgent.Warp(Home);
                            bot.NavAgent.isStopped = true;
                        }
                    }
                    BasePlayer AttackPlayer = null;
                    ////Casts a ray from eyes to check if player is there.
                    try
                    {
                        RaycastHit hit;
                        var raycast = Physics.Raycast(bot.eyes.HeadRay(), out hit, AttackRange, LayerMask.GetMask("Player (Server)"));
                        ////Cast result of first ray hit to base player
                        AttackPlayer = raycast ? hit.GetEntity() as BasePlayer : null;
                    }
                    catch { }

                    //No players with in range do nothing.
                    if (AttackPlayer == null)
                    {
                        ////Return home if no more activity.
                        if (LastInteraction >= Cooldown && Vector3.Distance(bot.transform.position, Home) >= RoamDistance)
                        {
                            //plugin.BotForget(bot);
                            bot.LastAttackedDir = Home;
                            bot.lastAttacker = null;
                            bot.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
                            bot.NavAgent.ResetPath();
                            //bot.NavAgent.SetDestination(Home);
                            //bot.NavAgent.Move(Home);
                            bot.MovePosition(Home);
                            if (plugin.Debug) plugin.Puts(bot.displayName + " Going Home");
                            if (FailedHomes >= 5)
                            {
                                if (plugin.Debug) plugin.Puts(bot.displayName + " Forced Home");
                                bot.NavAgent.Warp(Home);
                                FailedHomes = 0;
                            }
                            if (Vector3.Distance(bot.transform.position, Home) >= RoamDistance)
                            {
                                FailedHomes++;
                            }
                            else
                            {
                                FailedHomes = 0;
                            }
                            LastInteraction = 0;
                            bot.NavAgent.isStopped = false;
                            return;
                        }
                        LastInteraction++;
                        return;
                    }
                    //Make sure its not another bot
                    if (!AttackPlayer.IsNpc)
                    {
                        LastInteraction = 0;
                        //Checks if its a gun or melee
                        var gun = bot.GetGun();
                        if (gun == null)
                        {
                            //Do melee slash
                            if (bot.MeleeAttack())
                            {
                                BaseMelee weapon = bot?.GetHeldEntity() as BaseMelee;
                                //Apply Damage
                                if (weapon != null)
                                {
                                    plugin.BotMeleeAttack(bot, AttackPlayer, weapon);
                                }
                            }
                        }
                        else
                        {
                            //Do gun trigger
                            bot.TriggerDown();
                            //reload gun
                            int ammo = gun.primaryMagazine.contents;
                            if (gun.primaryMagazine.contents < 1)
                                bot.AttemptReload();
                        }
                    }
                }
            }
        }

        void OnWorldPrefabSpawned(GameObject gameObject, string str)
        {
            //Destroys random switch which is used to prefab ncp spawners
            if (gameObject.name == prefabplaceholder)
            {
                gameObject.GetComponent<BaseEntity>().Kill();
            }
        }

        private void Startup()
        {
            //Int to keep track of added scripts
            int NPCSpawners = 0;
            //Clears clocksettings incase its triggered reload.
            NPC_Spawners.Clear();
            //Find All NPCSpawners in the map
            for (int i = World.Serialization.world.prefabs.Count - 1; i >= 0; i--)
            {
                PrefabData prefabdata = World.Serialization.world.prefabs[i];
                //Check the prefab datas category since thats where customprefabs names are stored
                //491222911 = Letter B since you cant prefab botspawners on there own.
                if (prefabdata.id == StringPool.toNumber[prefabplaceholder] && prefabdata.category.Contains("BMGBOT"))
                {
                    //Pull settings from prefab name
                    string settings = prefabdata.category.Split(':')[1].Replace("\\", "");
                    //Check if it is already in dict
                    if (!NPC_Spawners.ContainsKey(prefabdata.position))
                    {
                        BotsSettings bs = new BotsSettings();
                        if (settings != null)
                        {
                            //Settings are seperated by a fullstop
                            string[] ParsedSettings = settings.Split('.');
                            try { if (plugin.IsKit(ParsedSettings[1])) { bs.kitname = ParsedSettings[1]; } } catch { }
                            try
                            {
                                switch (ParsedSettings[2])
                                {
                                    case "0":
                                        bs.stationary = false;
                                        break;
                                    case "1":
                                        bs.stationary = true;
                                        break;
                                }
                            }
                            catch { }
                            try { bs.health = int.Parse(ParsedSettings[3]); } catch { }
                            try { bs.AttackRange = int.Parse(ParsedSettings[4]); } catch { }
                            try { bs.roamrange = int.Parse(ParsedSettings[5]); } catch { }
                            try { bs.cooldown = int.Parse(ParsedSettings[6]); } catch { }
                            try
                            {
                                switch (ParsedSettings[7])
                                {
                                    case "0":
                                        bs.replaceitems = false;
                                        break;
                                    case "1":
                                        bs.replaceitems = true;
                                        break;
                                }
                            }
                            catch { }
                            try
                            {
                                switch (ParsedSettings[8])
                                {
                                    case "0":
                                        bs.parachute = false;
                                        break;
                                    case "1":
                                        bs.parachute = true;
                                        break;
                                }
                            }
                            catch { }
                            try { bs.name = ParsedSettings[9]; } catch { }
                        }
                        NPC_Spawners.Add(prefabdata.position, bs);
                        NPCSpawners++;
                    }
                }
            }
            HasLoadedSpawner = true;
            //Outputs debug info
            Puts("Found " + NPCSpawners.ToString() + " NPC Spawners");

            //Checks if theres any bots already spawned.
            foreach (NPCPlayer bot in BaseNetworkable.FindObjectsOfType<NPCPlayer>())
            {
                CheckBot(bot);
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            NPCPlayer bot = entity as NPCPlayer;
            if (bot != null)
            {
                //Checks if NPC_Spawner list has been filled
                if (!HasLoadedSpawner)
                    Startup();

                //Checks bots against NPC_Spawner
                CheckBot(bot);
            }
            if (entity.ShortPrefabName == "frankensteinpet_corpse")
            {
                NextTick(() =>
                {
                    LootableCorpse corpse = entity as LootableCorpse;
                    if (corpse != null)
                    {
                        UpdateCorpse(corpse);
                        corpse.SendNetworkUpdateImmediate();
                    }
                });
            }
            else if (entity.ShortPrefabName == "scientist_corpse")
            {
                NextTick(() =>
                {
                    LootableCorpse corpse = entity as LootableCorpse;
                    if (corpse != null)
                    {
                        UpdateCorpse(corpse);
                        corpse.SendNetworkUpdateImmediate();
                    }
                });
            }
        }

        void UpdateCorpse(LootableCorpse corpse)
        {
            for (int i = 0; i < 3; i++)
            {
                corpse.containers[i].Clear();
            }
            if (NPC_Bots.ContainsKey(corpse.playerSteamID))
            {
                if (NPC_Spawners.ContainsKey(NPC_Bots[corpse.playerSteamID]))
                {
                    if (NPC_Spawners[NPC_Bots[corpse.playerSteamID]].replaceitems)
                    {
                        if (NPC_Items.ContainsKey(corpse.playerSteamID))
                        {
                            if (Debug) Puts("Moving Items");
                            foreach (Botsinfo items in NPC_Items[corpse.playerSteamID])
                            {
                                corpse.containers[0].AddItem(items.idef, items.item_ammount, items.item_skin);
                            }
                            NPC_Items.Remove(corpse.playerSteamID);
                        }
                        timer.Once(1f, () =>
                        {
                            //Remove the delayed outfits rust edit adds to frankenstine and scientist corpses
                            foreach (ItemContainer ic in corpse.containers)
                            {
                                foreach (Item it in ic.itemList.ToArray())
                                {
                                    if (it.info.shortname.Contains("halloween.mummysuit") || it.info.shortname.Contains("scarecrow.suit") || it.info.shortname.Contains("hazmatsuit_scientist"))
                                    {
                                        it.DoRemove();
                                    }
                                }
                            }
                        });

                    }
                    corpse.playerName = NPC_Spawners[NPC_Bots[corpse.playerSteamID]].name;
                    //Remove stored bot since its dead.
                    NPC_Bots.Remove(corpse.playerSteamID);
                }
            }
        }

        void CheckBot(NPCPlayer bot)
        {
            for (int i = 0; i < NPC_Spawners.Count; i++)
            {
                if (Vector3.Distance(NPC_Spawners.ElementAt(i).Key, bot.transform.position) < 5f)
                {
                    if (!NPC_Bots.ContainsKey(bot.userID))
                    {
                        NPC_Bots.Add(bot.userID, NPC_Spawners.ElementAt(i).Key);
                        //Attach fix if its not already attached
                        if (bot.gameObject.GetComponent<BMGBOT>() == null)
                        {
                            bot.gameObject.AddComponent<BMGBOT>();
                        }
                    }
                }
            }

            //Delay so it can spawn
            timer.Once(5f, () =>
            {
                //checks if it has a chainsaw
                Chainsaw cs = bot.GetHeldEntity() as Chainsaw;
                if (cs != null)
                {
                    //Turn on chainsaw
                    cs.SetFlag(Chainsaw.Flags.On, true);
                    cs.SendNetworkUpdateImmediate();
                }
            });
        }

        private void Unload()
        {
            plugin = null;
            //removes the fix script
            foreach (var script in GameObject.FindObjectsOfType<NPCPlayer>())
            {
                foreach (var af in script.GetComponentsInChildren<BMGBOT>())
                {
                    UnityEngine.Object.DestroyImmediate(af);
                    //Kill bot since wont get re hooked off its spawner.
                    script.Kill();
                }
            }
        }

        private void OnServerInitialized(bool initial)
        {
            plugin = this;
            //Delay startup if fresh server boot. Helps hooking on slow servers
            if (initial)
            {
                Fstartup();
                return;
            }
            //Startup plugin
            Startup();
        }
        private void Fstartup()
        {
            //Waits for player before running script to help first startup performance.
            timer.Once(10f, () =>
            {
                if (Rust.Application.isLoading)
                {
                    //Still starting so run a timer again in 10 sec to check.
                    Fstartup();
                    return;
                }
                //Starup script now.
                Startup();
            });
        }

        private bool IsKit(string kit)
        {
            //Call kit plugin check if its valid kit
            var success = Kits?.Call("isKit", kit);
            if (success == null || !(success is bool))
            {
                return false;
            }
            return (bool)success;
        }

        //Changes the bots name
        void BotName(NPCPlayer bot, string NewName = "")
        {
            if (NewName == "")
            {
                //Face Punches random function using steamid
                bot._name = RandomUsernames.Get((int)bot.userID);
            }
            else
            {
                bot._name = NewName;
            }
            bot.displayName = bot._name;
        }

        void BotForget(NPCPlayer npc)
        {
            if (npc == null)
            {
                return;
            }
            if (Debug) Puts(npc.displayName + " Memory Wiped");
            npc.lastDealtDamageTime = Time.time;
            npc.lastAttackedTime = Time.time;
            npc.lastAttacker = null;
            HumanNPC bot = npc as HumanNPC;
            if (bot != null)
            {
                bot.Brain.SwitchToState(AIState.None);
            }
            npc.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
        }

        void BotHealth(NPCPlayer bot, float healthmulti)
        {
            //Change health and max health.
            bot.startHealth = healthmulti;
            bot.InitializeHealth(healthmulti, healthmulti);
        }

        //Do attack damage and sfx slightly delayed to match with time swing takes to happen
        void BotMeleeAttack(NPCPlayer bot, BasePlayer AttackPlayer, BaseMelee weapon)
        {
            timer.Once(0.2f, () =>
             {
                 if (Vector3.Distance(bot.transform.position, AttackPlayer.transform.position) < weapon.maxDistance)
                 {
                     AttackPlayer.Hurt(weapon.TotalDamage(), Rust.DamageType.Slash, null, true);
                     Effect.server.Run("assets/bundled/prefabs/fx/headshot.prefab", AttackPlayer.transform.position);
                 }
             });
        }

        //Add parachute and move player to location
        void AddParaChute(NPCPlayer bot, Vector3 Pos)
        {
            bot.transform.position = Pos;
            bot.gameObject.layer = 0;
            var rb = bot.gameObject.GetComponent<Rigidbody>();
            rb.drag = 0f;
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.velocity = new Vector3(bot.transform.forward.x * 0, 0, bot.transform.forward.z * 0) - new Vector3(0, 10, 0);
            var Chute = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab", Pos, Quaternion.Euler(0, 0, 0));
            Chute.gameObject.Identity();
            Chute.transform.localPosition = Chute.transform.localPosition + new Vector3(0f, 1.3f, 0f);
            Chute.SetParent(bot);
            Chute.Spawn();
        }

        void BotSkin(NPCPlayer bot, string Skin)
        {
            //Delayed to make sure its not fired before bot gets orignal kit.
            NextFrame(() =>
            {
                //Remove default items
                foreach (Item item in bot.inventory.containerBelt.itemList)
                {
                    item.Remove();
                }
                foreach (Item item in bot.inventory.containerMain.itemList)
                {
                    item.Remove();
                }
                foreach (Item item in bot.inventory.containerWear.itemList)
                {
                    item.Remove();
                }

                NextFrame(() =>
                {
                    //Try apply the kit
                    Kits?.Call("GiveKit", bot, Skin);
                    //Trys to equip stuff
                    timer.Once(2f, () =>
                     {
                         Item projectileItem = null;
                         //Find first gun
                         foreach (var item in bot.inventory.containerBelt.itemList)
                         {
                             if (item.GetHeldEntity() is BaseProjectile)
                             {
                                 projectileItem = item;
                                 break;
                             }
                         }
                         if (projectileItem != null)
                         {
                             //pull out gun.
                             bot.UpdateActiveItem(projectileItem.uid);
                             bot.inventory.UpdatedVisibleHolsteredItems();
                         }
                         else
                         {
                             foreach (var item in bot.inventory.containerBelt.itemList)
                             {
                                 if (item.GetHeldEntity() is BaseMelee)
                                 {
                                     projectileItem = item;
                                     break;
                                 }
                             }
                             //Pull out melee
                             bot.UpdateActiveItem(projectileItem.uid);
                         }
                         NextTick(() =>
                         {
                             //Update bot item list for corpse.
                             List<Botsinfo> items = new List<Botsinfo>();
                             foreach (Item item in bot.inventory.containerBelt.itemList)
                             {
                                 if (item.info != null)
                                 {
                                     Botsinfo bi = new Botsinfo();
                                     bi.item_ammount = item.amount;
                                     bi.idef = item.info;
                                     bi.item_skin = item.skin;
                                     if (!items.Contains(bi))
                                         items.Add(bi);
                                 }
                             }
                             foreach (Item item in bot.inventory.containerMain.itemList)
                             {
                                 if (item.info != null)
                                 {
                                     Botsinfo bi = new Botsinfo();
                                     bi.item_ammount = item.amount;
                                     bi.idef = item.info;
                                     bi.item_skin = item.skin;
                                     if (!items.Contains(bi))
                                         items.Add(bi);
                                 }
                             }
                             foreach (Item item in bot.inventory.containerWear.itemList)
                             {
                                 if (item.info != null)
                                 {
                                     Botsinfo bi = new Botsinfo();
                                     bi.item_ammount = 1;
                                     bi.idef = item.info;
                                     bi.item_skin = item.skin;
                                     if (!items.Contains(bi))
                                         items.Add(bi);
                                 }
                             }
                             NPC_Items.Add(bot.userID, items);
                         });
                     });
                });
            });
        }
    }
}