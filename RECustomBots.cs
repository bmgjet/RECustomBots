using Facepunch;
using Oxide.Core.Plugins;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
namespace Oxide.Plugins
{
    [Info("RECustomBots", "bmgjet", "1.0.4")]
    [Description("Fixes bots created with NPC_Spawner in rust edit")]
    public class RECustomBots : RustPlugin
    {
        public bool Debug = false;
        //Plugin will remove any prefab at this exact location on server starting. You use it as a copy protection by placing something that stops
        //servers from booting like a invisable pumpjack item on your map. Then copy its position value into below.
        Vector3 MapLockLocation = new Vector3(0, 0, 0);
        //Fixes doors collider to a smaller size so player can walk closer to them. But not break collider allowing bots to shoot though.
        bool FixDoors = true;
        //Prefab placeholder
        string prefabplaceholder = "assets/prefabs/deployable/playerioents/gates/randswitch/electrical.random.switch.deployed.prefab";
        //Bot Tick Rate
        float bot_tick = 1f; //Default Apex was 0.5f, Default AINew was 0.25f
        //Place holder scan distance
        float ScanDistance = 1f;
        //mount scan distance
        float MountScan = 8f;
        //Change the icon of the message announcer
        ulong AnnouncementIcon = 0;
        //Colour of announcer
        string color = "red";
        //Protect Sleepers
        bool SleepProtect = true;
        //Parachute Suiside Timer (will cut parachute off after this many seconds)
        float ChuteSider = 20f;
        //Checks this area around parachute for collision
        float ColliderDistance = 2f;
        //List of random taunt dead messages to send {N} gets replaced with attacker/victims name.
        private string[] Dead =
        {
            "You got me, GG",
            ":(",
            "Thats not nice {N}",
            "Come on man?",
            "Did you have to do that {N}",
            "Argh again",
            "Ur a cheater {N}",
            "SAD GUY",
        };
        private string[] Killed =
        {
            "haha your bad {N}",
            ":P EZ",
            "Git Gud {N}",
            "Wasted",
            "Im not even trying {N}",
            "HAHAHAHA {N}",
        };
/* New Settings method using keywords in any order.
 * If a keyword isnt provided it will use the default for that setting.
 * Only what you provide is adjusted
 * 
 * How to use:
 * Seperate keywords by "." which will show as " " when in rust edit looking at prefab groups.
 * Prefab group name must start with BMGBOT.
 * 
 * Avaliable keywords
 * name=         if set it will apply a specific name to the bot, Other wise will pick one at random based on userid the spawner creates.
 * kit=          if set will apply kit to bot, Can do male/female specific kits by using ^ between them kit=malekit^femalekit
 * stationary    if this keyword is present bot will remain stationary.
 * parachute     if this keyword is present bot will parachute to navmesh.
 * replace       if this keyword is present bot replace default items with kit items.
 * strip         if this keyword is present bot will strip all its lot on death.
 * radiooff      if this keyword is present bot will not use the radio to chatter.
 * peacekeeper   if this keyword is present bot will only fire on hostile players.
 * mount         if this keyword is present bot will mount the closest seat.
 * taunt         if this keyword is present bot will make taunts in the chat to players it interacts with.
 * killnotice    if this keyword is present kills/deaths of this bot will be announced to chat.
 * health=       if set will adjust the bots health to this example health=150     
 * attack=       if set the boot will attack only up to this range loss of sight is a further 10f from this setting.
 * roam=         if set this is how far the bot can move from its home spawn before it wants to return to home.
 * cooldown=     if set changes the default home check rate.
 * height=       if set can make small adjustment to bots navmesh height usuall settings range will be -3 to +3.
 * speed=        if set adjusts how fast the bot can run.
 * steamicon=    if set the bot will use the steamicon from this steamid example would be steamicon=76561198188915047
 * 
 * Example: bot with 500hp health and is stionary
 * BMGBOT.stationary.health=500
 * 
 * Example: bot that has a 2 different kits for males and females, parachutes in, radio chatter disabled, default items removed.
 * BMGBOT.kit=guy1^girl1.radiooff.parachute.replace
 * 
 * Example: bot with custom name is a peacekeeper and sitting in a chair
 * BMGBOT.name=Lazy Bot.peacekeeper.mount
*/
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
        //Random list of Steam IDs to use for bot images
        private ulong[] SteamIds = {
            76561198201252634,
            76561199110323558,
            76561199194720194,
            76561198129951438,
            76561198144464080,
            76561198101891231,
            76561198157637478,
            76561199225578578,
            76561198188915047,
            76561199219052430,
            76561198138463675,
            76561199172929853,
            76561198401529908,
            76561198032752525,
            76561199135596964,
            76561198065829079,
            76561198800018143,
            76561198091879314
        };
        //Last taunt said to prevent it repeating them in a row.
        int lasttaunt = -1;
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
            //BotSettings Defaults
            public string kitname = "";
            public bool stationary = false;
            public int health = 0;
            public float AttackRange = 20f;
            public int roamrange = 30;
            public int cooldown = 30;
            public bool replaceitems = false;
            public bool parachute = false;
            public string name = "";
            public bool radiooff = false;
            public bool peacekeeper = false;
            public bool taunts = false;
            public bool killnotice = false;
            public float height = 0f;
            public ulong steamicon = 0;
            public bool strip = false;
            public bool mount = false;
            public int speed = 0;
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
            bool peacekeeper;
            float height;
            bool mount;
            //References
            NPCPlayer bot;
            ScientistBrain SB;
            ScarecrowBrain SB2;
            //Keep track of parachute collider for other plugins
            CapsuleCollider paracol;
            private void Awake()
            {
                //Sets up
                bot = this.GetComponent<NPCPlayer>();
                SB = this.GetComponent<ScientistBrain>();
                SB2 = this.GetComponent<ScarecrowBrain>();

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
                        peacekeeper = settings.peacekeeper;
                        height = settings.height;
                        mount = settings.mount;
                    }
                    else
                    {
                        //No settings act default
                        return;
                    }
                    //Pick a random Icon
                    if (settings.steamicon == 0)
                    {
                        settings.steamicon = plugin.SteamIds[Random.Range(0, plugin.SteamIds.Length - 1)] + 100u;
                    }
                    //Set as own owner
                    bot.OwnerID = bot.userID;
                    //Set bots name

                    if (Name == "" || Name == "0")
                    {
                        //Face Punches random function using steamid
                        Name = RandomUsernames.Get((int)bot.userID);
                        bot._name = Name;
                        plugin.NPC_Spawners[plugin.NPC_Bots[bot.userID]].name = Name;
                    }
                    else
                    {
                        //Set custom botname
                        bot._name = Name;
                    }
                    //Update the bot
                    bot.displayName = Name;

                    //Set bot health
                    if (Health != 0)
                    {
                        bot.startHealth = Health;
                        bot.InitializeHealth(Health, Health);
                    }
                    //Stop bot moving until its activated
                    if (Stationary)
                    {
                        if (SB != null) SB.Navigator.CanUseNavMesh = false;
                        if (SB2 != null) SB2.Navigator.CanUseNavMesh = false;
                    }
                    //Stops attack range being completely 0 or negitive
                    if (AttackRange <= 0)
                    {
                        AttackRange = 1.5f;
                    }
                    if (RoamDistance <= 0)
                    {
                        RoamDistance = 3;
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
                        if (Kitname.Contains("^"))
                        {
                            //Do male or female specific kits
                            if (IsFemale(bot.userID))
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
                    //stops down spamming when cool down set too low
                    if (Cooldown < 5)
                    {
                        Cooldown = 10;
                    }
                    //Do setting up of behavour
                    if (SB != null)
                    {
                        SB.SenseRange = AttackRange;
                        SB.TargetLostRange = AttackRange + 10;
                        SB.Navigator.MaxRoamDistanceFromHome = RoamDistance;
                        SB.AttackRangeMultiplier = 1f;
                        SB.Senses.Memory.Targets.Clear();
                        //Adjust speed
                        SB.Navigator.Speed = settings.speed;
                        //peacekeeper check;
                        SB.HostileTargetsOnly = peacekeeper;
                    }
                    if (SB2 != null)
                    {
                        SB2.SenseRange = AttackRange;
                        SB2.TargetLostRange = AttackRange + 10;
                        SB2.Navigator.MaxRoamDistanceFromHome = RoamDistance;
                        SB2.AttackRangeMultiplier = 1f;
                        SB2.Senses.Memory.Targets.Clear();
                        //Adjust speed
                        SB2.Navigator.Speed = settings.speed;
                        //peacekeeper check;
                        SB2.HostileTargetsOnly = peacekeeper;
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
                    bot.ServerPosition = hit.position -= new Vector3(0, (height / 10), 0);
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
                    //Play sound fx if a parachute gets destroyed
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
                if (SB != null) SB.Navigator.CanUseNavMesh = !Stationary;
                if (SB2 != null) SB2.Navigator.CanUseNavMesh = !Stationary;
                //mounts bot onto nearby mount point if set.
                if (mount)
                {
                    plugin.MountBot(bot);
                }
            }

            //Collider to remove parachute
            private void OnCollisionEnter(Collision col)
            {
                //Ignore collider if not flying
                if (!flying)
                    return;
                //Attach to navmesh
                ClipGround();
            }

            bool CheckColliders()
            {
                //Scans the area
                foreach (Collider col in Physics.OverlapSphere(bot.transform.position, plugin.ColliderDistance, plugin.parachuteLayer))
                {
                    //Converts each collider to a string
                    string thisobject = col.gameObject.ToString();
                    //Checks if collider contains partial names
                    if (thisobject.Contains("modding") || thisobject.Contains("props") || thisobject.Contains("structures") || thisobject.Contains("building core")) { return true; }
                    //Check if its a base entity
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

            void GoHome()
            {
                //Improve return home function
                if (SB != null)
                {
                    SB.Events.RemoveAll();
                    SB.Navigator.SetDestination(Home, BaseNavigator.NavigationSpeed.Normal);
                    SB.SwitchToState(AIState.TakeCover);

                }
                if (SB2 != null)
                {
                    SB2.Events.RemoveAll();
                    SB2.Navigator.SetDestination(Home, BaseNavigator.NavigationSpeed.Normal);
                    SB2.SwitchToState(AIState.TakeCover);
                }
                //Change flags
                bot.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
                bot.SetPlayerFlag(BasePlayer.PlayerFlags.Aiming, false);
            }

            void ForgetAll(int resetdelay)
            {
                //Clear all memory and disable senses for a little bit to gain some distance.
                if (SB != null)
                {
                    SB.Senses.Memory.Targets.Clear();
                    SB.Senses.Memory.Threats.Clear();
                    SB.Senses.Memory.All.Clear();
                    SB.Senses.Memory.LOS.Clear();
                    SB.CurrentState.StateLeave();
                    SB.HostileTargetsOnly = peacekeeper;
                    SB.SetEnabled(false);
                    Invoke("ResetSenses", resetdelay);
                }
                if (SB2 != null)
                {
                    SB2.Senses.Memory.Targets.Clear();
                    SB2.Senses.Memory.Threats.Clear();
                    SB2.Senses.Memory.All.Clear();
                    SB2.Senses.Memory.LOS.Clear();
                    SB2.CurrentState.StateLeave();
                    SB2.HostileTargetsOnly = peacekeeper;
                    SB2.SetEnabled(false);
                    Invoke("ResetSenses", resetdelay);
                }
            }

            void ResetSenses()
            {
                //Delayed enabling of senses
                if (SB != null)
                {
                    SB.SetEnabled(true);
                }
                if (SB2 != null)
                {
                    SB2.SetEnabled(true);
                }
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
                //Stops attacking when in safe zone
                if (bot.InSafeZone())
                {
                    ForgetAll(30);
                    GoHome();
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

                    //More reliable
                    BasePlayer AttackPlayer = null;
                    List<BasePlayer> PlayerScan = new List<BasePlayer>();
                    Vis.Entities<BasePlayer>(bot.transform.position, AttackRange, PlayerScan);
                    foreach (BasePlayer entity in PlayerScan)
                    {
                        //Stops shooting though walls/doors
                        if (BasePlayer.activePlayerList.Contains(entity) && bot.IsVisibleAndCanSee(entity.eyes.position))
                        {
                            AttackPlayer = entity;
                            break;
                        }
                    }

                    //No players with in range do nothing.
                    if (AttackPlayer == null)
                    {
                        //Dont try to move if bot is sitting.
                        if (bot.isMounted)
                        {
                            //Do Some Seated stuff
                            return;
                        }
                        ////Return home if no more activity.
                        if (LastInteraction >= Cooldown && Vector3.Distance(bot.transform.position, Home) >= RoamDistance && !flying)
                        {
                            //Remove details from bot of its last taget
                            bot.LastAttackedDir = Home;
                            bot.lastAttacker = null;
                            //If its still not with in its roam distance after 5 checks force warp it back.
                            if (FailedHomes >= 5)
                            {
                                if (plugin.Debug) plugin.Puts(bot.displayName + " Forced Home");
                                bot.NavAgent.Warp(Home);
                                FailedHomes = 0;
                                LastInteraction = 0;
                                GoHome();
                                return;
                            }
                            FailedHomes++;
                            //Reset interaction and movement
                            LastInteraction = 0;
                            if (plugin.Debug) plugin.Puts(bot.displayName + " Going Home");
                            try
                            {
                                ForgetAll(10);
                                GoHome();
                                return;
                            }
                            catch
                            {
                                bot.Kill();
                                return;
                            }
                        }
                        //No interaction this tick
                        LastInteraction++;
                        return;
                    }
                    //Make sure its not another bot
                    if (!AttackPlayer.IsNpc)
                    {
                        FailedHomes = 0;
                        LastInteraction = 0;
                        //Peacekeeper bypass attack code.
                        if (peacekeeper && !AttackPlayer.IsHostile())
                        {
                            return;
                        }
                        //Sleeper Protection
                        if (plugin.SleepProtect && AttackPlayer.IsSleeping())
                        {
                            return;
                        }
                        //Checks if its a gun or melee
                        var gun = bot.GetGun();

                        AttackEntity AE = bot?.GetHeldEntity() as AttackEntity;
                        if (gun == null && AE != null)
                        {
                            BaseMelee weapon = bot?.GetHeldEntity() as BaseMelee;
                            //Do melee slash if in its reach
                            if (!AE.HasAttackCooldown() && Vector3.Distance(AttackPlayer.transform.position, bot.transform.position) <= weapon.maxDistance)
                            {
                                //melee hit
                                if (plugin.Debug) plugin.Puts("Trigger Melee");
                                if (bot.MeleeAttack())
                                {
                                    AE.StartAttackCooldown(AE.attackSpacing);

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
                            if (plugin.Debug) plugin.Puts("Trigger shoot");
                            bot.triggerEndTime = Time.time + UnityEngine.Random.Range(gun.attackLengthMin, gun.attackLengthMax);
                            try { InvokeRepeating("burstfire", 0f, 0.025f); } catch { }

                            //reload gun if less than 1 shot left.
                            int ammo = 0;
                            try { ammo = gun.primaryMagazine.contents; } catch { }
                            if (ammo < 1) { bot.AttemptReload(); }
                        }
                    }
                }
            }

            void burstfire()
            {
                //Rapid fire fox a white
                if (bot.triggerEndTime > Time.time)
                {
                    bot.TriggerDown();
                    return;
                }
                CancelInvoke("burstfire");
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
                            bool oldsettings = true;
                            //Try new method first of using just keywords not dependant on position.
                            foreach (string keyword in ParsedSettings)
                            {
                                if (keyword.ToLower().Contains("stationary")) { bs.stationary = true; oldsettings = false; }
                                else if (keyword.ToLower().Contains("name")) { try { bs.name = keyword.Split('=')[1]; } catch { bs.name = ""; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("kit")) { try { bs.kitname = keyword.Split('=')[1]; } catch { bs.kitname = ""; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("parachute")) { bs.parachute = true; oldsettings = false; }
                                else if (keyword.ToLower().Contains("replace")) { bs.replaceitems = true; oldsettings = false; }
                                else if (keyword.ToLower().Contains("health")) { try { bs.health = int.Parse(keyword.Split('=')[1]); } catch { bs.health = 0; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("attack")) { try { bs.AttackRange = int.Parse(keyword.Split('=')[1]); } catch { bs.AttackRange = 30; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("roam")) { try { bs.roamrange = int.Parse(keyword.Split('=')[1]); } catch { bs.roamrange = 30; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("cooldown")) { try { bs.cooldown = int.Parse(keyword.Split('=')[1]); } catch { bs.cooldown = 15; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("peacekeeper")) { try { bs.peacekeeper = true; } catch { bs.peacekeeper = false; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("radiooff")) { try { bs.radiooff = true; } catch { bs.radiooff = false; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("taunt")) { try { bs.taunts = true; } catch { bs.taunts = false; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("height")) { try { bs.height = int.Parse(keyword.Split('=')[1]); } catch { bs.height = 0; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("steamicon")) { try { bs.steamicon = ulong.Parse(keyword.Split('=')[1]); } catch { bs.steamicon = 0; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("speed")) { try { bs.speed = int.Parse(keyword.Split('=')[1]); } catch { bs.speed = 0; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("strip")) { try { bs.strip = true; } catch { bs.strip = false; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("mount")) { try { bs.mount = true; } catch { bs.mount = false; } oldsettings = false; }
                                else if (keyword.ToLower().Contains("killnotice")) { try { bs.killnotice = true; } catch { bs.killnotice = false; } oldsettings = false; }
                            }

                            //Do static settings layout
                            if (oldsettings)
                            {
                                //parse out first skipping 0 since thats the tag.
                                try { bs.kitname = ParsedSettings[1]; } catch { }
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
                                try { bs.name = ParsedSettings[9]; } catch { bs.name = ""; }
                            }
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
        }

        private void Unload()
        {
            plugin = null;
            //removes the script
            foreach (var script in GameObject.FindObjectsOfType<NPCPlayer>())
            {
                script.EnsureDismounted();
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
            //Fix invalid prefab stopping server starting
            BaseEntity component = gameObject.GetComponent<BaseEntity>();
            if (component != null)
            {
                if (component.OwnerID == 0 && gameObject.transform.position == MapLockLocation && MapLockLocation != new Vector3(0, 0, 0))
                {
                    if (Debug) Puts("Removed Invalid Prefab @ " + MapLockLocation);
                    //Remove Invalid Prefab thats used as somewhat protection.
                    component.Kill();
                    return;
                }

                //Door collider shrink but not destroy to stop bots shooting though then.
                if (FixDoors)
                {
                    Door doorfix = component.GetComponent<Door>();
                    if (doorfix != null)
                    {
                        if (Debug) Puts("Door Fixed @ " + doorfix.transform.position.ToString());
                        //Scales door X to 0.1 so it still has some thickness to stop bot bullet and vison but not so thick player hits on it.
                        doorfix.transform.localScale = new Vector3(0.1f, doorfix.transform.localScale.y, doorfix.transform.localScale.z);
                    }
                }
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            //Dismount bots on seat break
            BaseMountable seat = entity.GetComponent<BaseMountable>();
            if (seat != null && seat._mounted.IsNpc)
            {
                seat._mounted.EnsureDismounted();
            }
            //Get player
            BasePlayer player = entity.ToPlayer();
            if (player == null) return;
            //Get who was the attacker
            BasePlayer attacker = player.lastAttacker.ToPlayer();
            if (attacker == null) return;
            //Failsafe so while loop cant get stuck going on for ever
            int failsafe = 0;

            //Taunt bot kills
            if (NPC_Bots.ContainsKey(attacker.userID))
            {
                if (NPC_Spawners.ContainsKey(NPC_Bots[attacker.userID]))
                {
                    if (NPC_Spawners[NPC_Bots[attacker.OwnerID]].taunts)
                    {
                        //Pick a taunt at randm making sure not to be a repeat
                        int seed = Random.Range(0, Killed.Length - 1);
                        while (seed == lasttaunt && failsafe < 10)
                        {
                            failsafe++;
                            seed = Random.Range(0, Killed.Length - 1);
                        }
                        lasttaunt = seed;
                        //Send taunt to chat.
                        CreateTaunt(Killed[seed].Replace("{N}", player.displayName), attacker.ToPlayer(), NPC_Spawners[NPC_Bots[attacker.OwnerID]].steamicon);
                        return;
                    }
                }
            }
            //taunt bot dies
            if (NPC_Bots.ContainsKey(player.userID))
            {
                if (NPC_Spawners.ContainsKey(NPC_Bots[player.userID]))
                {
                    //Public bot death annoucements
                    if (NPC_Spawners[NPC_Bots[player.OwnerID]].killnotice)
                    {
                        //Send kill announcement to chat.
                        CreateAnouncment(attacker.displayName + " Killed " + player._name + " With " + info.damageTypes.GetMajorityDamageType());
                    }
                    if (NPC_Spawners[NPC_Bots[player.OwnerID]].taunts)
                    {
                        //Pick a taunt at randm making sure not to be a repeat
                        int seed = Random.Range(0, Dead.Length - 1);
                        while (seed == lasttaunt && failsafe < 10)
                        {
                            failsafe++;
                            seed = Random.Range(0, Dead.Length - 1);
                        }
                        lasttaunt = seed;
                        //Send taunt to chat.
                        CreateTaunt(Dead[seed].Replace("{N}", attacker.displayName), player.ToPlayer(), NPC_Spawners[NPC_Bots[player.OwnerID]].steamicon);
                        return;
                    }
                }
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            //Fix console spam
            BaseNavigator baseNavigator = entity.GetComponent<BaseNavigator>();
            //Checks if has navigator AI
            if (baseNavigator != null)
            {
                //temp position
                Vector3 pos;
                //Checks if its within the default navmesh scan settings
                if (!baseNavigator.GetNearestNavmeshPosition(entity.transform.position + (Vector3.one * 2f), out pos, (baseNavigator.IsSwimming() ? 30f : 6f)))
                {
                    //Sets as stationary
                    baseNavigator.CanUseNavMesh = false;
                }
            }
            //Cast as a NPCPlayer
            NPCPlayer bot = entity as NPCPlayer;
            //Check if NPC
            if (bot != null)
            {
                //Checks if NPC_Spawner list has been filled
                if (!HasLoadedSpawner)
                    Startup();
                //Checks bots against NPC_Spawner
                NextFrame(() => { CheckBot(bot); });
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
                    //If its set to replace items or strip them
                    if (NPC_Spawners[NPC_Bots[corpse.playerSteamID]].replaceitems || NPC_Spawners[NPC_Bots[corpse.playerSteamID]].strip)
                    {
                        //Empty default items off corpse
                        for (int i = 0; i < 3; i++)
                        {
                            corpse.containers[i].Clear();
                        }
                        //Only remove items 
                        if (NPC_Spawners[NPC_Bots[corpse.playerSteamID]].strip) return;

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

        //Sends message to all active players under a steamID
        void CreateAnouncment(string msg)
        {
            foreach (BasePlayer current in BasePlayer.activePlayerList.ToArray())
            {
                if (current.IsConnected)
                {
                    rust.SendChatMessage(current, "<color=" + color + ">Death</color>", msg, AnnouncementIcon.ToString());
                }
            }
        }

        //Sends chat message to all active players.
        void CreateTaunt(string msg, BasePlayer player, ulong steamid)
        {
            foreach (BasePlayer current in BasePlayer.activePlayerList.ToArray())
            {
                if (current.IsConnected)
                {
                    rust.SendChatMessage(current, "<color=#55aaff>" + player.displayName + "</color>:", msg, steamid.ToString());
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
            timer.Once(2f, () =>
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

                    if (UnityEngine.Random.Range(1, 101) <= 25)
                        Effect.server.Run("assets/bundled/prefabs/fx/headshot.prefab", AttackPlayer.transform.position);
                }
            });
        }

        //Blocks shooting when under attack range on setup bots except heavy and junkpile for some reason.
        private object OnNpcTarget(NPCPlayer npc, BaseEntity entity)
        {
            if (entity == null || npc == null) return null;
            if (NPC_Bots.ContainsKey(npc.userID))
            {
                if (NPC_Spawners.ContainsKey(NPC_Bots[npc.userID]))
                {
                    if (Vector3.Distance(npc.transform.position, entity.transform.position) >= NPC_Spawners[NPC_Bots[npc.userID]].AttackRange + 10)
                    {
                        return true;
                    }
                    //Cast target to baseplayer to check some conditions
                    BasePlayer TargetedPlayer = entity.ToPlayer();
                    if (TargetedPlayer == null)
                    {
                        return null;
                    }
                    //Block targeting of non-hostile players from peacekeeper bots.
                    if (NPC_Spawners[NPC_Bots[npc.userID]].peacekeeper && !TargetedPlayer.IsHostile())
                    {
                        return true;
                    }
                    //Protects sleepers
                    if (SleepProtect && TargetedPlayer.IsSleeping())
                    {
                        return true;
                    }
                }
            }
            return null;
        }

        //Hook to disable radio
        private object OnNpcRadioChatter(ScientistNPC npc)
        {
            //Check if a custom bot
            if (NPC_Bots.ContainsKey(npc.userID))
            {
                //check if has setting
                if (NPC_Spawners.ContainsKey(NPC_Bots[npc.userID]))
                {
                    //check radio status and disable radio if set
                    if (NPC_Spawners[NPC_Bots[npc.userID]].radiooff)
                    {
                        //no chatter
                        return true;
                    }
                }
            }
            //Normal bahavour
            return null;
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

        //Scans around the bot for seats, Put the bot in the closest seat to it.
        public void MountBot(NPCPlayer bot)
        {
            List<BaseMountable> Seats = new List<BaseMountable>();
            Vis.Entities<BaseMountable>(bot.transform.position, MountScan, Seats);
            BaseMountable closest_seat = null;
            foreach (BaseMountable seat in Seats)
            {
                if (seat.HasFlag(BaseEntity.Flags.Busy)) continue;
                if (closest_seat == null) closest_seat = seat;
                if (Vector3.Distance(bot.transform.position, seat.transform.position) <= Vector3.Distance(bot.transform.position, closest_seat.transform.position))
                    closest_seat = seat;
            }
            if (closest_seat != null)
            {
                closest_seat.GetComponent<BaseMountable>().AttemptMount(bot);
                closest_seat.SendNetworkUpdateImmediate();
                bot.SendNetworkUpdateImmediate();
                if (plugin.Debug) plugin.Puts(bot.displayName + " Forced into Seat @ " + closest_seat.transform.position.ToString());
            }
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
                //Move default items into a droped backpack at GC, Ugly way to fix errors from NPCs spawning with same userids and having links to destroyed items
                var belt = bot.inventory.containerBelt.Drop("assets/prefabs/misc/item drop/item_drop_backpack.prefab", new Vector3(0, 0, 0), bot.transform.rotation);
                var main = bot.inventory.containerMain.Drop("assets/prefabs/misc/item drop/item_drop_backpack.prefab", new Vector3(0, 0, 0), bot.transform.rotation);
                var wear = bot.inventory.containerWear.Drop("assets/prefabs/misc/item drop/item_drop_backpack.prefab", new Vector3(0, 0, 0), bot.transform.rotation);

                //Delay for items removal to take effect
                NextFrame(() =>
                {
                    //Remove dropped backpacks
                    if (belt != null){belt.RemoveMe();}
                    if (main != null){main.RemoveMe();}
                    if (wear != null){wear.RemoveMe();}
                    //Try apply the kit
                    Kits?.Call("GiveKit", bot, Skin);
                    //Trys to equip stuff after a delay for kits plugin to of ran
                    try
                    {
                        timer.Once(1f, () =>
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
                            //Try get gun ready.
                            try
                            {
                                timer.Once(2f, () =>
                                 {
                                     bot.AttemptReload();
                                 });
                            }
                            catch { }
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
                    }
                    catch { }
                });
            });
        }
    }
}