//Custom PrefabName Layout
/*
*  Identifiyer Seperated by "."
*  BMGBOT.Kitname.Stationary.Health.AttackRange.Roamrange.Cooldown.Replaceitems.Parachute.Name
*  
*  Kitname = the name of the kit to put on the bot. Leave Blank for no kit, Male/Fem
*  Stationary = 0 false / 1 true. Not move from spawner (Note Some NPC types wont be able to turn anymore to test the type your using)
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

//ToDo: Redo distance checks to allow for larger groupings.

using Facepunch;
using Oxide.Core.Plugins;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
namespace Oxide.Plugins
{
    [Info("RECustomBots", "bmgjet", "1.0.2")]
    [Description("Fixes bots created with NPC_Spawner in rust edit")]
    public class RECustomBots : RustPlugin
    {
        public bool Debug = false;
        //Prefab placeholder
        string prefabplaceholder = "assets/prefabs/deployable/playerioents/gates/randswitch/electrical.random.switch.deployed.prefab";
        //Bot Tick Rate
        float bot_tick = 0.5f; //Default Apex was 0.5f, Default AINew was 0.25f
        //Place holder scan distance
        float ScanDistance = 1f;
        //Parachute Suiside Timer (will cut parachute off after this many seconds)
        float ChuteSider = 20f;
        //Checks this area around parachute for collision
        float ColliderDistance = 2f;
        //Layers of collision
        int parachuteLayer = 1 << (int)Rust.Layer.Water | 1 << (int)Rust.Layer.Transparent | 1 << (int)Rust.Layer.World | 1 << (int)Rust.Layer.Construction | 1 << (int)Rust.Layer.Debris | 1 << (int)Rust.Layer.Default | 1 << (int)Rust.Layer.Terrain | 1 << (int)Rust.Layer.Tree | 1 << (int)Rust.Layer.Vehicle_Large | 1 << (int)Rust.Layer.Deployed;
        //Stored location of spawner and its settings
        Dictionary<Vector3, BotsSettings> NPC_Spawners = new Dictionary<Vector3, BotsSettings>();
        //Stored bot ID and what location to get settings from
        Dictionary<ulong, Vector3> NPC_Bots = new Dictionary<ulong, Vector3>();
        //Store bots items
        Dictionary<ulong, List<Botsinfo>> NPC_Items = new Dictionary<ulong, List<Botsinfo>>();
        //Store List of place holders to remove
        List<BaseEntity> PlaceHolders = new List<BaseEntity>();
        //Check if fully started.
        private bool HasLoadedSpawner = false;
        //reference to kits plugin
        [PluginReference]
        private Plugin Kits;
        //reference to self
        private static RECustomBots plugin;
        //Process Kit items into Botinfo
        Botsinfo ProcessKitItem(Item item)
        {
            Botsinfo bi = new Botsinfo();
            bi.item_ammount = item.amount;
            bi.idef = item.info;
            bi.item_skin = item.skin;
            return bi;
        }
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
            public bool replaceitems = false;
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
            //Keep track of parachute collider for other plugins
            CapsuleCollider paracol;
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
                        Parachute = settings.parachute;
                        Stationary = settings.stationary;
                        replaceitems = settings.replaceitems;
                    }
                    else
                    {
                        //No settings act default
                        return;
                    }
                    //Set bots name
                    if (Name == "")
                    {
                        //Face Punches random function using steamid
                        bot._name = RandomUsernames.Get((int)bot.userID);
                        plugin.NPC_Spawners[plugin.NPC_Bots[bot.userID]].name = bot._name;
                    }
                    else
                    {
                        //Set custom botname
                        bot._name = Name;
                    }
                    //Update the bot
                    bot.displayName = bot._name;
                    //Set bot health
                    if (Health != 0)
                    {
                        bot.startHealth = Health;
                        bot.InitializeHealth(Health, Health);
                    }
                    //Stop bot moving until its activated
                    if (Stationary)
                    {
                        BaseNavigator BN = bot.gameObject.GetComponent<BaseNavigator>();
                        BN.CanUseNavMesh = false;
                    }
                    //Stops attack range being completely 0 or negitive
                    if (AttackRange <= 0)
                    {
                        AttackRange = 0.8f;
                    }
                    //Adds parachute to spawning
                    if (Parachute)
                    {
                        //Trigger flying and get collider around NPC
                        flying = true;
                        paracol = bot.GetComponent<CapsuleCollider>();
                        if (paracol != null)
                        {
                            //If not created then adjust radius
                            paracol.isTrigger = true;
                            bot.GetComponent<CapsuleCollider>().radius += 4f;
                        }
                        //Move bot
                        bot.transform.position = Home + new Vector3(0, 100, 0);
                        bot.gameObject.layer = 0;
                        //Adjust phyics stuff
                        var rb = bot.gameObject.GetComponent<Rigidbody>();
                        rb.drag = 0f;
                        rb.useGravity = false;
                        rb.isKinematic = false;
                        rb.velocity = new Vector3(bot.transform.forward.x * 0, 0, bot.transform.forward.z * 0) - new Vector3(0, 10, 0);
                        //Create the parachute
                        var Chute = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab", bot.transform.position, Quaternion.Euler(0, 0, 0));
                        Chute.gameObject.Identity();
                        //Offset it to players back
                        Chute.transform.localPosition = Chute.transform.localPosition + new Vector3(0f, 1.3f, 0f);
                        //Attach player and spawn parachute
                        Chute.SetParent(bot);
                        Chute.Spawn();
                        plugin.ParachuteSuiside(bot);
                    }
                    else
                    {
                        //Attach bot to the ground.
                        ClipGround();
                    }
                    //Sets kit onto bot if ones set
                    if (Kitname != "")
                    {
                        if(Kitname.Contains("^"))
                        {
                            //Do male or female specific kits
                            if(IsFemale(bot.userID))
                            {
                                Kitname = Kitname.Split('^')[1];
                            }
                            else
                            {
                                Kitname = Kitname.Split('^')[0];
                            }
                        }
                        //codes choake point since hooks have to be single threaded.
                        if (plugin.IsKit(Kitname))
                        {
                            plugin.BotSkin(bot, Kitname, replaceitems);
                        }
                    }
                    //Output Debug Info
                    if (plugin.Debug) plugin.Puts("Bot " + bot.displayName + " spawned,Health:" + bot.health + " Kit:" + Kitname + " Range:" + AttackRange.ToString() + " Roam:" + RoamDistance.ToString() + " Cooldown:" + Cooldown.ToString() + " Default Items:" + replaceitems + " Stationary:" + Stationary.ToString() + " Parachute:" + Parachute);
                    bot.SendNetworkUpdate();
                    //Setup repeating script after 5 secs at tick rate
                    InvokeRepeating("_tick", 5, plugin.bot_tick);
                }
            }

            private void ClipGround()
            {
                //get rigidbody reference
                var rb = bot.gameObject.GetComponent<Rigidbody>();
                //Scan for ground.
                NavMeshHit hit;
                if (NavMesh.SamplePosition(bot.transform.position, out hit, 30, -1))
                {
                    //parachute colider reference remove
                    if (paracol != null)
                    {
                        paracol.isTrigger = false;
                        bot.GetComponent<CapsuleCollider>().radius -= 4f;
                    }
                    //Water check
                    if (bot.WaterFactor() > 0.9f)
                    {
                        bot.Kill();
                        return;
                    }
                    //Remove any phyics alterations
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    bot.gameObject.layer = 17;
                    //Offset adjustment
                    bot.ServerPosition = hit.position -= new Vector3(0, 0.2f, 0);
                    bot.NavAgent.Move(bot.ServerPosition);
                    //Remove any attached Parachutes
                    bool Destroyed = false;
                    foreach (var child in bot.children.Where(child => child.name.Contains("parachute")))
                    {
                        child.SetParent(null);
                        child.Kill();
                        Destroyed = true;
                        break;
                    }
                    if (Destroyed)
                    {
                        Effect.server.Run("assets/bundled/prefabs/fx/player/groundfall.prefab", bot.transform.position);
                    }
                }
                else
                {
                    //Unable to detect ground. Let player fall.
                    rb.useGravity = true;
                    rb.drag = 1f;
                    rb.velocity = new Vector3(bot.transform.forward.x * 15, 11, bot.transform.forward.z * 15);
                }
                //Player has been grounded
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

            bool CheckColliders()
            {
                foreach (Collider col in Physics.OverlapSphere(bot.transform.position, plugin.ColliderDistance, plugin.parachuteLayer))
                {
                    string thisobject = col.gameObject.ToString();
                    if (thisobject.Contains("modding") || thisobject.Contains("props") || thisobject.Contains("structures") || thisobject.Contains("building core")) { return true; }

                    BaseEntity baseEntity = col.gameObject.ToBaseEntity();
                    if (baseEntity != null && (baseEntity == bot || baseEntity == bot.GetComponent<BaseEntity>()))
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                return false;
            }

            void _tick()
            {
                //Check if bot exsists
                if (bot == null)
                {
                    Destroy(this);
                    return;
                }
                //Fall back check if bot falls though map while parachuting with out hitting a navmesh.
                if (flying)
                {
                    if (TerrainMeta.HeightMap.GetHeight(bot.transform.position) >= bot.transform.position.y || CheckColliders())
                    {
                        ClipGround();
                        return;
                    }
                }

                //Dont run check if bot is downed
                if (!bot.IsCrawling() && bot.IsAlive())
                {
                    if (Stationary)
                    {
                        if (Vector3.Distance(bot.transform.position, Home) > 1f)
                        {
                            bot.NavAgent.Warp(Home);
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
                        if (LastInteraction >= Cooldown && Vector3.Distance(bot.transform.position, Home) >= RoamDistance && !flying)
                        {
                            //Remove details from bot of its last taget
                            bot.LastAttackedDir = Home;
                            bot.lastAttacker = null;
                            bot.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
                            //If its still not with in its roam distance after 5 checks force warp it back.
                            if (FailedHomes >= 5)
                            {
                                if (plugin.Debug) plugin.Puts(bot.displayName + " Forced Home");
                                bot.NavAgent.Warp(Home);
                                FailedHomes = 0;
                                LastInteraction = 0;
                                return;
                            }
                            //Checking its in roam distance
                            if (Vector3.Distance(bot.transform.position, Home) >= RoamDistance)
                            {
                                FailedHomes++;
                            }
                            else
                            {
                                //With in roaming distance dont force home.
                                FailedHomes = 0;
                            }
                            //Reset interaction and movement
                            LastInteraction = 0;
                            if (plugin.Debug) plugin.Puts(bot.displayName + " Going Home");
                            bot.NavAgent.isStopped = false;
                            //bot.SetDestination(Home);
                            bot.NavAgent.Move(Home);
                            //bot.NavAgent.Move(Home);
                            return;
                        }
                        //No interaction this tick
                        LastInteraction++;
                        return;
                    }
                    //Make sure its not another bot
                    if (!AttackPlayer.IsNpc)
                    {
                        LastInteraction = 0;
                        //Checks if its a gun or melee
                        var gun = bot.GetGun();

                        AttackEntity AE = bot?.GetHeldEntity() as AttackEntity;
                        if (gun == null && AE != null)
                        {
                            //Do melee slash
                            if (!AE.HasAttackCooldown())
                            {
                                //melee hit
                                if (bot.MeleeAttack())
                                {
                                    AE.StartAttackCooldown(AE.attackSpacing);
                                    BaseMelee weapon = bot?.GetHeldEntity() as BaseMelee;
                                    //Apply Damage
                                    if (weapon != null)
                                    {
                                        plugin.BotMeleeAttack(bot, AttackPlayer, weapon);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Do gun trigger
                            try{bot.TriggerDown();}catch{ }
                            //reload gun if less than 1 shot left.
                            int ammo = 0;
                            try{ammo = gun.primaryMagazine.contents;}catch { }
                            if (ammo < 1){bot.AttemptReload();}
                        }
                    }
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
            //Waits for fully loaded before running script to help first startup performance.
            timer.Once(10f, () =>
            {
                try
                {
                    if (Rust.Application.isLoading)
                    {
                        //Still starting so run a timer again in 10 sec to check.
                        Fstartup();
                        return;
                    }
                }
                catch { }
                //Starup script now.
                Startup();
            });
        }

        private void Startup()
        {
            //Clears clocksettings incase its triggered reload.
            NPC_Spawners.Clear();
            uint placeholderid = StringPool.toNumber[prefabplaceholder];
            //Find All NPCSpawners in the map
            for (int i = World.Serialization.world.prefabs.Count - 1; i >= 0; i--)
            {
                PrefabData prefabdata = World.Serialization.world.prefabs[i];

                //Check the prefab datas category since thats where customprefabs names are stored
                if (prefabdata.id == placeholderid && prefabdata.category.Contains("BMGBOT"))
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
                            //parse out first skipping 0 since thats the tag.
                            try { bs.kitname = ParsedSettings[1]; } catch { }
                            //2
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
                            //3
                            try { bs.health = int.Parse(ParsedSettings[3]); } catch { }
                            //4
                            try { bs.AttackRange = int.Parse(ParsedSettings[4]); } catch { }
                            //5
                            try { bs.roamrange = int.Parse(ParsedSettings[5]); } catch { }
                            //6
                            try { bs.cooldown = int.Parse(ParsedSettings[6]); } catch { }
                            //7
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
                            //8
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
                            //9
                            try { bs.name = ParsedSettings[9]; } catch { }
                        }
                        //Create Dictornary reference
                        NPC_Spawners.Add(prefabdata.position, bs);
                    }
                }
            }
            //Set plugin as fully ready
            HasLoadedSpawner = true;
            //Clean up place holders
            RemoveAllPlaceHolders();
            //Outputs debug info
            Puts("Found " + NPC_Spawners.Count.ToString() + " NPC Spawners");
            //Checks if theres any bots already spawned.
            foreach (NPCPlayer bot in BaseNetworkable.FindObjectsOfType<NPCPlayer>())
            {
                CheckBot(bot);
            }
        }

        private void Unload()
        {
            plugin = null;
            //removes the script
            foreach (var script in GameObject.FindObjectsOfType<NPCPlayer>())
            {
                foreach (var af in script.GetComponentsInChildren<BMGBOT>())
                {
                    UnityEngine.Object.DestroyImmediate(af);
                    //Kill bot since wont get re hooked if its moved from its spawner
                    script.Kill();
                }
            }
        }

        void OnWorldPrefabSpawned(GameObject gameObject, string str)
        {
            //Creates a list of prefab used as placeholders to make the prefab group.
            if (gameObject.name == prefabplaceholder)
            {
                PlaceHolders.Add(gameObject.GetComponent<BaseEntity>());
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            //Cast as a NPCPlayer
            NPCPlayer bot = entity as NPCPlayer;
            //Check if NPC
            if (bot != null)
            {
                //Checks if NPC_Spawner list has been filled
                if (!HasLoadedSpawner)
                    Startup();

                //Checks bots against NPC_Spawner
                CheckBot(bot);
            }
            //Check corpses
            if (entity.ShortPrefabName == "frankensteinpet_corpse")
            {
                //Delay until next frame to allow it to be spawned
                NextFrame(() =>
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
                //Delay until next frame to allow it to be spawned
                NextFrame(() =>
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
            //Checks if its a NPC
            if (NPC_Bots.ContainsKey(corpse.playerSteamID))
            {
                //Checks if settings from the spawner
                if (NPC_Spawners.ContainsKey(NPC_Bots[corpse.playerSteamID]))
                {
                    //If its set to replace items
                    if (NPC_Spawners[NPC_Bots[corpse.playerSteamID]].replaceitems)
                    {
                        //Empty default items off corpse
                        for (int i = 0; i < 3; i++)
                        {
                            corpse.containers[i].Clear();
                        }
                        //Checks list of saved items from kit
                        if (NPC_Items.ContainsKey(corpse.playerSteamID))
                        {
                            if (Debug) Puts("Moving Items");
                            foreach (Botsinfo items in NPC_Items[corpse.playerSteamID])
                            {
                                corpse.containers[0].AddItem(items.idef, items.item_ammount, items.item_skin);
                            }
                            //Emptys kit item list
                            NPC_Items.Remove(corpse.playerSteamID);
                        }
                        //Wait 1 sec since RE and other plugins might be adding stuff on the next frame already.
                        timer.Once(1f, () =>
                        {
                            if (corpse == null)
                            {
                                return;
                            }
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
                    //Update corpses name
                    corpse.playerName = NPC_Spawners[NPC_Bots[corpse.playerSteamID]].name;
                    //Remove stored bot since its dead.
                    NPC_Bots.Remove(corpse.playerSteamID);
                }
            }
        }

        void RemoveAllPlaceHolders()
        {
            //Go though each placeholder and make sure its with in range of the NPC Spawner before removing it.
            foreach (BaseEntity PH in PlaceHolders)
            {
                if (PH != null)
                {
                    foreach (KeyValuePair<Vector3, BotsSettings> Spawners in NPC_Spawners)
                    {
                        if (Vector3.Distance(PH.transform.position, Spawners.Key) < ScanDistance)
                        {
                            try { PH.Kill(); } catch { }
                        }
                    }
                }
            }
        }

        void CheckBot(NPCPlayer bot)
        {
            //Loop all the NPC_Spawners that have been hooked.
            for (int i = 0; i < NPC_Spawners.Count; i++)
            {
                //Checks with in 5f range of spawner since bot could of moved slightly.
                if (Vector3.Distance(NPC_Spawners.ElementAt(i).Key, bot.transform.position) < 5f)
                {
                    if (!NPC_Bots.ContainsKey(bot.userID))
                    {
                        NPC_Bots.Add(bot.userID, NPC_Spawners.ElementAt(i).Key);
                        //Attach script if its not already attached
                        if (bot.gameObject.GetComponent<BMGBOT>() == null)
                        {
                            bot.gameObject.AddComponent<BMGBOT>();
                        }
                    }
                }
            }
            //Murdurer Fix Delay so it can spawn chainsaw then start it so it will do damage.
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

        void ParachuteSuiside(NPCPlayer bot)
        {
            //Destroys parachute after 20 secs incase bot gets stuck in trees/buildings
            timer.Once(ChuteSider, () =>
            {
                bool Destroyed = false;
                foreach (var child in bot.children.Where(child => child.name.Contains("parachute")))
                {
                    child.SetParent(null);
                    child.Kill();
                    Destroyed = true;
                    break;
                }
                if (Destroyed)
                {
                    Effect.server.Run("assets/bundled/prefabs/fx/player/groundfall.prefab", bot.transform.position);
                }
            });
        }

        //Do attack damage and sfx slightly delayed to match with time swing takes to happen
        void BotMeleeAttack(NPCPlayer bot, BasePlayer AttackPlayer, BaseMelee weapon)
        {
            if (bot == null || AttackPlayer == null || weapon == null)
                return;

            //Create a delay for the animation
            float delay = 0.2f;
            try { delay = weapon.aiStrikeDelay; } catch { }
            timer.Once(delay, () =>
            {
                //Check weapons reach.
                if (Vector3.Distance(bot.transform.position, AttackPlayer.transform.position) < weapon.maxDistance)
                {
                    //Apply damage and play SFX
                    AttackPlayer.Hurt(weapon.TotalDamage(), Rust.DamageType.Slash, null, true);
                    Effect.server.Run("assets/bundled/prefabs/fx/headshot.prefab", AttackPlayer.transform.position);
                }
            });
        }

        public static bool IsFemale(ulong userID)
        {
            //Save current random state
            UnityEngine.Random.State state = UnityEngine.Random.state;
            //initilise in a known state so we already know the outcome of the random generator
            //Feed userid as the seed
            UnityEngine.Random.InitState((int)(4332UL + userID));
            //Determin gender
            bool Gender = (UnityEngine.Random.Range(0f, 1f) > 0.5f);
            //Reset state back to a random unknown state we saved.
            UnityEngine.Random.state = state;
            return Gender;
        }

        private bool IsKit(string kit)
        {
            //Call kit plugin to check if its valid kit
            var success = Kits?.Call("isKit", kit);
            if (success == null || !(success is bool))
            {
                return false;
            }
            return (bool)success;
        }

        void BotSkin(NPCPlayer bot, string Skin, bool replacement)
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

                //Delay for items removal to take effect
                NextFrame(() =>
                {
                    //Try apply the kit
                    Kits?.Call("GiveKit", bot, Skin);
                    //Trys to equip stuff after a delay for kits plugin to of ran
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
                            //Find a melee weapon in the belt
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
                        //Only do this if bot wants items replaced on the corpse
                        if (replacement)
                        {
                            NextTick(() =>
                            {
                                //Update bot item list for corpse use
                                List<Botsinfo> items = new List<Botsinfo>();
                                foreach (Item item in bot.inventory.containerBelt.itemList)
                                {
                                    if (item.info != null)
                                    {
                                        Botsinfo bi = ProcessKitItem(item);
                                        if (!items.Contains(bi))
                                            items.Add(bi);
                                    }

                                }
                                foreach (Item item in bot.inventory.containerMain.itemList)
                                {
                                    if (item.info != null)
                                    {
                                        Botsinfo bi = ProcessKitItem(item);
                                        if (!items.Contains(bi))
                                            items.Add(bi);
                                    }
                                }
                                foreach (Item item in bot.inventory.containerWear.itemList)
                                {
                                    if (item.info != null)
                                    {
                                        Botsinfo bi = ProcessKitItem(item);
                                        if (!items.Contains(bi))
                                            items.Add(bi);
                                    }
                                }
                                NPC_Items.Add(bot.userID, items);
                            });
                        }
                    });
                });
            });
        }
    }
}