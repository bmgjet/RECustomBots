using Facepunch;
using Oxide.Core.Plugins;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
namespace Oxide.Plugins
{
    [Info("RECustomBots", "bmgjet", "1.0.6")]
    [Description("Improves bots created with NPC_Spawner in rust edit")]
    public class RECustomBots : RustPlugin
    {
        public bool DebugInfo = false;
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
            "Ur 2 gud {N}",
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
        // * New Settings method using keywords in any order.
        // * If a keyword isnt provided it will use the default for that setting.
        // * Only what you provide is adjusted
        // * 
        // * How to use:
        // * Seperate keywords by "." which will show as " " when in rust edit looking at prefab groups.
        // * Prefab group name must start with BMGBOT.
        // * 
        // * Avaliable keywords
        // * name=         if set it will apply a specific name to the bot, Other wise will pick one at random based on userid the spawner creates.
        // * kit=          if set will apply kit to bot, Can do male/female specific kits by using ^ between them kit=malekit^femalekit
        // * stationary    if this keyword is present bot will remain stationary.
        // * parachute     if this keyword is present bot will parachute to navmesh.
        // * replace       if this keyword is present bot replace default items with kit items.
        // * strip         if this keyword is present bot will strip all its lot on death.
        // * radiooff      if this keyword is present bot will not use the radio to chatter.
        // * peacekeeper   if this keyword is present bot will only fire on hostile players.
        // * mount         if this keyword is present bot will mount the closest seat.
        // * taunt         if this keyword is present bot will make taunts in the chat to players it interacts with.
        // * killnotice    if this keyword is present kills/deaths of this bot will be announced to chat.
        // * health=       if set will adjust the bots health to this example health=150     
        // * attack=       if set the boot will attack only up to this range loss of sight is a further 10f from this setting.
        // * roam=         if set this is how far the bot can move from its home spawn before it wants to return to home.
        // * cooldown=     if set changes the default home check rate.
        // * height=       if set can make small adjustment to bots navmesh height usuall settings range will be -3 to +3.
        // * speed=        if set adjusts how fast the bot can run.
        // * steamicon=    if set the bot will use the steamicon from this steamid example would be steamicon=76561198188915047
        // * apcattack     if this keyword is present bot will be targeted by the APC
        // * canheal       if this keyword is present bot will heal its self when its at half halth if its kitted with medical items.
        // * damageScale=  What percentage of normal damage the bot will do. 100% damage is the default which is same damage a player does.
        // * accuracy=     What percentage of shots will be at the target. 50% is the default.
        // * 
        // * Example: bot with 500hp health and is stionary
        // * BMGBOT.stationary.health=500
        // * 
        // * Example: bot that has a 2 different kits for males and females, parachutes in, radio chatter disabled, default items removed.
        // * BMGBOT.kit=guy1^girl1.radiooff.parachute.replace
        // * 
        // * Example: bot with custom name is a peacekeeper and sitting in a chair
        // * BMGBOT.name=Lazy Bot.peacekeeper.mount

        //Weapons with issues
        //crossbow.entity
        //bow_hunter.entity
        //speargun.entity

        //Layers of collision
        int parachuteLayer = 1 << (int)Rust.Layer.Water | 1 << (int)Rust.Layer.Transparent | 1 << (int)Rust.Layer.World | 1 << (int)Rust.Layer.Construction | 1 << (int)Rust.Layer.Debris | 1 << (int)Rust.Layer.Default | 1 << (int)Rust.Layer.Terrain | 1 << (int)Rust.Layer.Tree | 1 << (int)Rust.Layer.Vehicle_Large | 1 << (int)Rust.Layer.Deployed;
        //Stored location of spawner and its settings
        Dictionary<Vector3, BotsSettings> NPC_Spawners = new Dictionary<Vector3, BotsSettings>();
        //Stored bot ID and what location to get settings from
        Dictionary<ulong, Vector3> NPC_Bots = new Dictionary<ulong, Vector3>();
        //Store bots items
        Dictionary<ulong, List<Botsinfo>> NPC_Items = new Dictionary<ulong, List<Botsinfo>>();
        //Ignored shots
        List<ulong> IgnoredShots = new List<ulong>();
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
            public string kitname = "bmgjet";
            public bool stationary = false;
            public int health = 0;
            public float AttackRange = 30f;
            public int roamrange = 20;
            public int cooldown = 30;
            public bool replaceitems = false;
            public bool parachute = false;
            public string name = "";
            public bool radiooff = false;
            public bool peacekeeper = false;
            public bool taunts = false;
            public bool killnotice = false;
            public bool apcattack = false;
            public bool canheal = false;
            public float height = 0f;
            public ulong steamicon = 0;
            public bool strip = false;
            public bool mount = false;
            public int speed = -1;
            public int damageScale = 100;
            public int accuracy = 30;
        }
        //Bot script
        public class BMGBOT : MonoBehaviour
        {
            //vars
            int LastInteraction = 0;
            int FailedHomes = 0;
            bool flying = true;
            Vector3 Home;
            BotsSettings settings;
            //References
            BasePlayer bp;
            HumanNPC bot;
            NPCPlayer NPC;
            BaseAIBrain<ScarecrowNPC> SBrain;
            BaseNavigator BN;
            //is healing
            bool Healing = false;
            //Held Gun
            Item Gun;

            //Keep track of parachute collider for other plugins
            CapsuleCollider paracol;
            private void Awake()
            {
                bp = this.GetComponent<BasePlayer>();
                bot = this.GetComponent<HumanNPC>();
                BN = this.GetComponent<BaseNavigator>();
                NPC = this.GetComponent<NPCPlayer>();
                if (bot == null)
                {
                    SBrain = this.GetComponent<ScarecrowNPC>().Brain;
                }

                if (bp == null || BN == null || NPC == null) return;
                //Check if its a bot from spawner
                if (plugin.NPC_Bots.ContainsKey(bp.userID))
                {
                    //Set a home point
                    Home = plugin.NPC_Bots[bp.userID];
                    var col = bp.gameObject.AddComponent<BoxCollider>();
                    col.size = new Vector3(1, 1f, 1);
                    //Load settings
                    settings = plugin.NPC_Spawners[plugin.NPC_Bots[bp.userID]];
                    //Check there are settings
                    if (settings == null)
                    {
                        //No Settings so be default
                        return;
                    }
                    //Pick a random Icon
                    if (settings.steamicon == 0)
                    {
                        settings.steamicon = plugin.SteamIds[Random.Range(0, plugin.SteamIds.Length - 1)] + (uint)100;
                    }
                    //Set as own owner
                    bp.OwnerID = bp.userID;
                    //Set bots name
                    if (settings.name == "" || settings.name == "0")
                    {
                        //Face Punches random function using steamid
                        settings.name = RandomUsernames.Get((int)bp.userID);
                        bp._name = settings.name;
                        plugin.NPC_Spawners[plugin.NPC_Bots[bp.userID]].name = settings.name;
                    }
                    else
                    {
                        //Set custom botname
                        bp._name = settings.name;
                    }
                    //Update the bot
                    bp.displayName = settings.name;
                    //Set bot health
                    if (settings.health != 0)
                    {
                        bp.startHealth = settings.health;
                        bp.InitializeHealth(settings.health, settings.health);
                    }
                    //Stop bot moving until its activated
                    if (settings.stationary)
                    {
                        BN.CanUseNavMesh = false;
                    }
                    //Stops attack range being completely 0 or negitive
                    if (settings.AttackRange <= 0)
                    {
                        settings.AttackRange = 1.5f;
                    }
                    if (settings.roamrange <= 0)
                    {
                        settings.roamrange = 5;
                    }
                    //Adds parachute to spawning
                    if (settings.parachute)
                    {
                        //Trigger flying and get collider around NPC
                        flying = true;
                        paracol = bp.GetComponent<CapsuleCollider>();
                        if (paracol != null)
                        {
                            //If not created then adjust radius
                            paracol.isTrigger = true;
                            bp.GetComponent<CapsuleCollider>().radius += 4f;
                        }
                        //Move bot
                        bp.transform.position = Home + new Vector3(0, 100, 0);
                        bp.gameObject.layer = 0;
                        //Adjust phyics stuff
                        var rb = bp.gameObject.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.drag = 0f;
                            rb.useGravity = false;
                            rb.isKinematic = false;
                            rb.velocity = new Vector3(bp.transform.forward.x * 0, 0, bp.transform.forward.z * 0) - new Vector3(0, 10, 0);
                        }
                        //Create the parachute
                        var Chute = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab", bp.transform.position, Quaternion.Euler(0, 0, 0));
                        if (Chute != null)
                        {
                            Chute.gameObject.Identity();
                            //Offset it to players back
                            Chute.transform.localPosition = Chute.transform.localPosition + new Vector3(0f, 1.3f, 0f);
                            //Attach player and spawn parachute
                            Chute.SetParent(bp);
                            Chute.Spawn();
                            plugin.ParachuteSuiside(bp);
                        }
                    }
                    else
                    {
                        //Attach bot to the ground.
                        ClipGround();
                    }
                    //Sets kit onto bot if ones set
                    if (settings.kitname != "")
                    {
                        string kitname = settings.kitname;
                        if (settings.kitname.Contains("^"))
                        {
                            //Do male or female specific kits
                            if (IsFemale(NPC.userID))
                            {
                                kitname = kitname.Split('^')[1];
                            }
                            else
                            {
                                kitname = kitname.Split('^')[0];
                            }
                        }
                        if (plugin.IsKit(kitname))
                        {
                            plugin.BotSkin(NPC, kitname, settings.replaceitems);
                        }
                    }
                    //stops down spamming when cool down set too low
                    if (settings.cooldown < 5)
                    {
                        settings.cooldown = 10;
                    }
                    //Do setting up of behavour
                    if (bot != null)
                    {
                        bot.Brain.SenseRange = settings.AttackRange;
                        bot.Brain.ListenRange = settings.AttackRange;
                        bot.Brain.TargetLostRange = settings.AttackRange;
                        bot.Brain.Senses.maxRange = settings.AttackRange;
                        bot.Brain.Navigator.MaxRoamDistanceFromHome = settings.roamrange;
                        bot.Brain.AttackRangeMultiplier = 1f;
                        bot.Brain.Senses.Memory.Targets.Clear();
                        bot.Brain.IgnoreSafeZonePlayers = true;
                        //Adjust speed
                        if (settings.speed != -1)
                        {
                            bot.Brain.Navigator.Speed = settings.speed;
                        }
                        //peacekeeper check;
                        bot.Brain.HostileTargetsOnly = settings.peacekeeper;
                        bot.Brain.Senses.Update();
                    }
                    else if (SBrain != null)
                    {
                        SBrain.SenseRange = settings.AttackRange;
                        SBrain.ListenRange = settings.AttackRange;
                        SBrain.TargetLostRange = settings.AttackRange;
                        SBrain.Senses.maxRange = settings.AttackRange;
                        SBrain.Navigator.MaxRoamDistanceFromHome = settings.roamrange;
                        SBrain.AttackRangeMultiplier = 1f;
                        SBrain.Senses.Memory.Targets.Clear();
                        SBrain.IgnoreSafeZonePlayers = true;
                        //Adjust speed
                        if (settings.speed != -1)
                        {
                            SBrain.Navigator.Speed = settings.speed;
                        }
                        //peacekeeper check;
                        SBrain.HostileTargetsOnly = settings.peacekeeper;
                        SBrain.Senses.Update();
                    }
                    else
                    {
                        bp.Kill();
                        return;
                    }
                    //Allows bot every topo
                    BN.topologyPreference = ((TerrainTopology.Enum)TerrainTopology.EVERYTHING);
                    NPC.damageScale = settings.damageScale / 100f;
                    //Output Debug Info
                    if (plugin.DebugInfo) plugin.Puts("Bot " + bp.displayName + " spawned,Health:" + bp.health + " Kit:" + settings.kitname + " Range:" + settings.AttackRange.ToString() + " Roam:" + settings.roamrange.ToString() + " Cooldown:" + settings.cooldown.ToString() + " Default Items:" + !settings.replaceitems + " Stationary:" + settings.stationary.ToString() + " Parachute:" + settings.parachute);
                    //Update Server With Bot
                    bp.SendNetworkUpdate();
                    //Setup repeating script after 5 secs at tick rate
                    InvokeRepeating("_tick", 5, plugin.bot_tick);
                }
            }

            public BasePlayer GetBestTarget()
            {
                //Find best target from the list of targets the bot has
                List<BaseEntity> Targets = new List<BaseEntity>();
                //Get settings from senses
                float SenseRange = settings.AttackRange;
                float VisionCone = 0;
                if (bot != null)
                {
                    Targets = bot.Brain.Senses.Memory.Targets;
                    SenseRange = bot.Brain.SenseRange;
                    VisionCone = bot.Brain.VisionCone;
                }
                else if (SBrain != null)
                {
                    Targets = SBrain.Senses.Memory.Targets;
                    SenseRange = SBrain.SenseRange;
                    VisionCone = SBrain.VisionCone;
                }
                else
                {
                    return null;
                }
                BasePlayer target = null;
                float delta = -1f;
                foreach (BaseEntity baseEntity in Targets)
                {
                    //Dont target low health or dead
                    if (baseEntity == null || baseEntity.Health() <= 0f) continue;
                    BasePlayer basePlayer = baseEntity as BasePlayer;
                    if (!CanTargetPlayer(basePlayer)) continue;
                    //Get closest player
                    float rangeDelta = 1f - Mathf.InverseLerp(1f, SenseRange, Vector3.Distance(basePlayer.transform.position, bp.transform.position));
                    float dot = Vector3.Dot((basePlayer.transform.position - bp.eyes.position).normalized, bp.eyes.BodyForward());
                    //Check if in vision
                    if (dot < VisionCone) continue;
                    rangeDelta += Mathf.InverseLerp(VisionCone, 1f, dot) / 2f;
                    if (bot != null)
                    {
                        rangeDelta += (bot.Brain.Senses.Memory.IsLOS(basePlayer) ? 2f : 0f);
                    }
                    else
                    {
                        rangeDelta += (SBrain.Senses.Memory.IsLOS(basePlayer) ? 2f : 0f);
                    }
                    if (rangeDelta <= delta) continue;
                    target = basePlayer;
                    delta = rangeDelta;
                }
                if (plugin.DebugInfo && target != null)
                {
                    plugin.Puts("Found Target " + target.ToString());
                }
                return target;
            }

            public bool CanTargetPlayer(BasePlayer player)
            {
                //Conditions not to attack under
                if (player == null || player.IsFlying || player.IsSleeping() || player.IsWounded() || player.IsDead()) return false;
                return true;
            }

            private void ClipGround()
            {
                //get rigidbody reference
                var rb = bp.gameObject.GetComponent<Rigidbody>();
                //Scan for ground.
                NavMeshHit hit;
                if (NavMesh.SamplePosition(bp.transform.position, out hit, 30, -1))
                {
                    //parachute colider reference remove
                    if (paracol != null)
                    {
                        paracol.isTrigger = false;
                        bp.GetComponent<CapsuleCollider>().radius -= 4f;
                    }
                    //Water check
                    if (bp.WaterFactor() > 0.9f)
                    {
                        bp.Kill();
                        return;
                    }
                    //Remove any phyics alterations
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    bp.gameObject.layer = 17;
                    //Offset adjustment
                    bp.ServerPosition = hit.position -= new Vector3(0, (settings.height / 10), 0);
                    BN.Agent.Move(bp.ServerPosition);
                    //Remove any attached Parachutes
                    bool Destroyed = false;
                    foreach (var child in bp.children.Where(child => child.name.Contains("parachute")))
                    {
                        child.SetParent(null);
                        child.Kill();
                        Destroyed = true;
                        break;
                    }
                    //Play sound fx if a parachute gets destroyed
                    if (Destroyed)
                    {
                        Effect.server.Run("assets/bundled/prefabs/fx/player/groundfall.prefab", bp.transform.position);
                    }
                }
                else
                {
                    //Unable to detect ground. Let player fall.
                    rb.useGravity = true;
                    rb.drag = 1f;
                    rb.velocity = new Vector3(bp.transform.forward.x * 15, 11, bp.transform.forward.z * 15);
                }
                //Player has been grounded
                flying = false;
                if (BN != null)
                {
                    // BN.Warp(Home);
                    BN.CanUseNavMesh = !settings.stationary;
                }
                //mounts bot onto nearby mount point if set.
                if (settings.mount)
                {
                    plugin.MountBot(bp);
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
                foreach (Collider col in Physics.OverlapSphere(bp.transform.position, plugin.ColliderDistance, plugin.parachuteLayer))
                {
                    //Converts each collider to a string
                    string thisobject = col.gameObject.ToString();
                    //Checks if collider contains partial names
                    if (thisobject.Contains("modding") || thisobject.Contains("props") || thisobject.Contains("structures") || thisobject.Contains("building core")) { return true; }
                    //Check if its a base entity
                    BaseEntity baseEntity = col.gameObject.ToBaseEntity();
                    if (baseEntity != null && (baseEntity == bp || baseEntity == bp.GetComponent<BaseEntity>()))
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
                //Remove events from bot, Set to normal movement speed, Make them act if they are going to cover
                if (bot != null)
                {
                    bot.Brain.Events.RemoveAll();
                    bot.Brain.Navigator.SetDestination(Home, BaseNavigator.NavigationSpeed.Normal);
                    bot.Brain.SwitchToState(AIState.TakeCover);

                }
                else if (SBrain != null)
                {
                    SBrain.Events.RemoveAll();
                    SBrain.Navigator.SetDestination(Home, BaseNavigator.NavigationSpeed.Normal);
                    SBrain.SwitchToState(AIState.TakeCover);
                }
                //Change flags
                bp.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
                bp.SetPlayerFlag(BasePlayer.PlayerFlags.Aiming, false);
            }

            void ForgetAll(int resetdelay)
            {
                //Clear all memory and disable senses for a little bit to gain some distance.
                if (bot != null)
                {
                    bot.Brain.Senses.Memory.Targets.Clear();
                    bot.Brain.Senses.Memory.Threats.Clear();
                    bot.Brain.Senses.Memory.All.Clear();
                    bot.Brain.Senses.Memory.LOS.Clear();
                    bot.Brain.CurrentState.StateLeave();
                    bot.Brain.HostileTargetsOnly = settings.peacekeeper;
                    bot.Brain.SetEnabled(false);
                    Invoke("ResetSenses", resetdelay);
                }
                else if (SBrain != null)
                {
                    SBrain.Senses.Memory.Targets.Clear();
                    SBrain.Senses.Memory.Threats.Clear();
                    SBrain.Senses.Memory.All.Clear();
                    SBrain.Senses.Memory.LOS.Clear();
                    SBrain.CurrentState.StateLeave();
                    SBrain.HostileTargetsOnly = settings.peacekeeper;
                    SBrain.SetEnabled(false);
                    Invoke("ResetSenses", resetdelay);
                }
            }

            void ResetSenses()
            {
                //Delayed enabling of senses
                if (bot != null)
                {
                    bot.Brain.SetEnabled(true);
                }
                else if (SBrain != null)
                {
                    SBrain.SetEnabled(true);
                }
            }

            void HomeChecks()
            {
                //Dont try to move if bot is sitting.
                if (bp.isMounted)
                {
                    //Do Some Seated stuff
                    return;
                }
                //Return home if no more activity.
                if (LastInteraction >= settings.cooldown && Vector3.Distance(bp.transform.position, Home) > settings.roamrange && !flying)
                {
                    //Remove details from bot of its last taget
                    bp.LastAttackedDir = Home;
                    bp.lastAttacker = null;
                    //If its still not with in its roam distance after 5 checks force warp it back.
                    if (FailedHomes >= 5)
                    {
                        if (plugin.DebugInfo) plugin.Puts(bp.displayName + " Forced Home");
                        BN.Agent.Warp(Home);
                        FailedHomes = 0;
                        LastInteraction = 0;
                        GoHome();
                        return;
                    }
                    FailedHomes++;
                    //Reset interaction and movement
                    LastInteraction = 0;
                    if (plugin.DebugInfo) plugin.Puts(bp.displayName + " Going Home");
                    try
                    {
                        ForgetAll(10);
                        GoHome();
                        return;
                    }
                    catch
                    {
                        bp.Kill();
                        return;
                    }
                }
                //No interaction this tick
                LastInteraction++;
            }

            void AttackLogic(BasePlayer AttackPlayer)
            {
                //Checks if its a gun or melee
                var gun = NPC.GetGun();
                AttackEntity AE = NPC?.GetHeldEntity() as AttackEntity;
                //Gives them a rock if they have nothing
                if (AE == null)
                {
                    ItemDefinition itemDefinition = ItemManager.FindItemDefinition("rock");
                    Item rock = ItemManager.Create(itemDefinition, 1, 0);
                    NPC.GiveItem(rock);
                    NPC.UpdateActiveItem(rock.uid);
                }
                if (gun == null && AE != null)
                {
                    if (plugin.DebugInfo) plugin.Puts("Melee Trigger " + AE.ShortPrefabName);
                    switch (AE.ShortPrefabName)
                    {
                        case "flamethrower.entity":
                            NPC.triggerEndTime = Time.time + 5f;
                            InvokeRepeating("use", 0f, 0.025f);
                            AE.StartAttackCooldown(AE.attackSpacing);
                            plugin.BotMeleeAttack(NPC, AttackPlayer, AE, Rust.DamageType.Heat, 5, "assets/bundled/prefabs/fx/impacts/additive/fire.prefab", 100);
                            return;

                        case "watergun.entity":
                            //Need Fix
                            LiquidWeapon LW = AE as LiquidWeapon;
                            LW.AutoPump = true;
                            plugin.FireWaterGun(NPC, LW);
                            return;

                        case "waterpistol.entity":
                            //Need Fix
                            LiquidWeapon LW2 = AE as LiquidWeapon;
                            LW2.AutoPump = true;
                            plugin.FireWaterGun(NPC, LW2);
                            return;

                        case "grenade.f1.entity":
                            AE.repeatDelay = 5f;
                            plugin.ServerThrow(AttackPlayer.transform.position, AE as ThrownWeapon, NPC);
                            return;
                        case "grenade.beancan.entity":
                            AE.repeatDelay = 5f;
                            plugin.ServerThrow(AttackPlayer.transform.position, AE as ThrownWeapon, NPC);
                            return;
                        case "smoke_grenade.weapon":
                            AE.repeatDelay = 5f;
                            plugin.ServerThrow(AttackPlayer.transform.position, AE as ThrownWeapon, NPC);
                            return;
                        case "snowball.entity":
                            AE.repeatDelay = 5f;
                            return;
                    };

                    BaseMelee weapon = NPC?.GetHeldEntity() as BaseMelee;
                    if (weapon == null)
                    {
                        return;
                    }
                    //Do melee slash if in its reach
                    if (!AE.HasAttackCooldown() && Vector3.Distance(AttackPlayer.transform.position, NPC.transform.position) <= weapon.maxDistance)
                    {
                        //melee hit
                        if (plugin.DebugInfo) plugin.Puts("Trigger Melee Attack");
                        if (NPC.MeleeAttack())
                        {
                            AE.StartAttackCooldown(AE.attackSpacing);
                            //Apply Damage
                            plugin.BotMeleeAttack(NPC, AttackPlayer, weapon, Rust.DamageType.Slash, weapon.TotalDamage());
                        }
                    }
                }
                else
                {
                    switch (NPC.GetHeldEntity().ShortPrefabName)
                    {
                        //Rocket Launcher Logic
                        case "rocket_launcher.entity":
                            gun.repeatDelay = 5f;
                            plugin.EmulatedFire(gun, NPC, AttackPlayer);
                            return;
                        //Grenade Launcher Logic
                        case "mgl.entity":
                            gun.repeatDelay = 2f;
                            plugin.EmulatedFire(gun, NPC, AttackPlayer);
                            return;
                    }

                    if (gun != null)
                    {
                        //Do gun trigger
                        if (plugin.DebugInfo) plugin.Puts("Trigger Shoot");
                        float triggertime = UnityEngine.Random.Range(gun.attackLengthMin, gun.attackLengthMax);
                        if (triggertime == -1)
                        {
                            //Some guns have -1 as there attacklength so give them one based on ammo count
                            triggertime = gun.primaryMagazine.contents / 4;
                            if (triggertime == 0) triggertime = 1f;
                        }
                        NPC.triggerEndTime = Time.time + triggertime;
                        InvokeRepeating("use", 0f, 0.025f);
                    }
                    //reload gun if less than 1 shot left.
                    int ammo = 0;
                    try { ammo = gun.primaryMagazine.contents; } catch { return; }
                    if (ammo < 1)
                    {
                        if (plugin.DebugInfo) plugin.Puts("Trigger Reload");
                        NPC.AttemptReload();
                    }
                }
            }

            void use()
            {
                if (NPC != null)
                {
                    try
                    {
                        //Checks that trigger end is after current time
                        if (NPC.triggerEndTime > Time.time)
                        {
                            //Keep Looping
                            NPC.GetHeldEntity().ServerUse(NPC.damageScale);
                            return;
                        }
                        else
                        {
                            //Give a single shot
                            NPC.GetHeldEntity().ServerUse(NPC.damageScale);
                        }
                    }
                    catch { }
                }
                //Stop Invoke
                CancelInvoke("use");
            }

            void SwitchGun()
            {
                NPC.UpdateActiveItem(Gun.uid);
                NPC.SendNetworkUpdateImmediate();
                Healing = false;
            }

            void _tick()
            {
                //Check if bot exsists
                if (bp == null)
                {
                    Destroy(this);
                    return;
                }
                //Fall back check if bot falls though map while parachuting with out hitting a navmesh.
                if (flying)
                {
                    if (TerrainMeta.HeightMap.GetHeight(bp.transform.position) >= bp.transform.position.y || CheckColliders())
                    {
                        ClipGround();
                        return;
                    }
                }
                //Stops attacking when in safe zone
                if (bp.InSafeZone())
                {
                    ForgetAll(30);
                    GoHome();
                    return;
                }
                //Dont run check if bot is downed or dead
                if (bp.IsCrawling() || bp.IsDead() || bp.IsWounded())
                {
                    return;
                }
                //Check if bot should be stationary but has moved.
                if (settings.stationary)
                {
                    if (Vector3.Distance(bp.transform.position, Home) > 1f)
                    {
                        //Force back to spawners location
                        BN.Agent.Warp(Home);
                    }
                }
                //Heal logic
                if (NPC._health < NPC._maxHealth / 2 && !Healing && settings.canheal)
                {
                    if (plugin.DebugInfo) plugin.Puts("Bot is Healing");
                    //Store current held gun if not medical item
                    if (NPC.GetHeldEntity() is MedicalTool)
                    {
                        return;
                    }
                    try
                    {
                        //Store gun
                        Gun = NPC.GetHeldEntity().GetItem();
                    }
                    catch
                    {
                        //Failed to store gun so end function
                        return;
                    }
                    //Check bot for med items
                    foreach (var item in NPC.inventory.containerBelt.itemList.ToList())
                    {
                        if (item.GetHeldEntity() is MedicalTool)
                        {
                            //Switch to the meds
                            NPC.UpdateActiveItem(item.uid);
                            NPC.inventory.UpdatedVisibleHolsteredItems();
                            //use meds
                            MedicalTool meds = NPC.GetHeldEntity() as MedicalTool;
                            if (meds != null)
                            {
                                Healing = true;
                                meds.ServerUse();
                            }
                            //Switch back to gun
                            Invoke("SwitchGun", 4f);
                            break;
                        }
                    }
                    return;
                }

                //Find Targeted Player
                BasePlayer AttackPlayer = GetBestTarget();
                //Fallback funding players in CQ slower method if nothing found
                if (AttackPlayer == null)
                {
                    List<BasePlayer> PlayerScan = new List<BasePlayer>();
                    Vis.Entities<BasePlayer>(bp.transform.position, 10, PlayerScan);
                    foreach (BasePlayer entity in PlayerScan)
                    {
                        //Stops shooting though walls/doors
                        if (BasePlayer.activePlayerList.Contains(entity) && bp.IsVisibleAndCanSee(entity.eyes.position))
                        {
                            //Found a player to attack
                            AttackPlayer = entity;
                            break;
                        }
                    }
                }
                //No players with in range do nothing.
                if (AttackPlayer == null)
                {
                    HomeChecks();
                    return;
                }
                //Reset Home Checking
                FailedHomes = 0;
                LastInteraction = 0;

                //Make sure its not another bot
                if (!AttackPlayer.IsNpc)
                {
                    //Peacekeeper bypass attack code.
                    if (settings.peacekeeper && !AttackPlayer.IsHostile())
                    {
                        return;
                    }
                    //Sleeper Protection
                    if (plugin.SleepProtect && AttackPlayer.IsSleeping())
                    {
                        return;
                    }

                    //Adjust Chance faces the right way.
                    //BN.SetFacingDirectionOverride(AttackPlayer.transform.position += new Vector3(-UnityEngine.Random.Range(0.0f, 1.0f), -UnityEngine.Random.Range(0.0f , 1.0f), -UnityEngine.Random.Range(0.0f, 1.0f))); ;
                    //Turn to the player thats the target
                    if (UnityEngine.Random.Range(1, 101) <= settings.accuracy)
                    {
                        BN.SetFacingDirectionEntity(AttackPlayer);
                        if (plugin.IgnoredShots.Contains(bp.userID))
                            plugin.IgnoredShots.Remove(bp.userID);
                    }
                    else
                    {
                        if(!plugin.IgnoredShots.Contains(bp.userID))
                        plugin.IgnoredShots.Add(bp.userID);
                    }



                    //Attack
                    AttackLogic(AttackPlayer);
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
                            foreach (string keyword in ParsedSettings)
                            {
                                if (keyword.ToLower().Contains("stationary")) { bs.stationary = true; }
                                else if (keyword.ToLower().Contains("name")) { try { bs.name = keyword.Split('=')[1]; } catch { bs.name = ""; } }
                                else if (keyword.ToLower().Contains("kit")) { try { bs.kitname = keyword.Split('=')[1]; } catch { bs.kitname = ""; } }
                                else if (keyword.ToLower().Contains("parachute")) { bs.parachute = true; }
                                else if (keyword.ToLower().Contains("replace")) { bs.replaceitems = true; }
                                else if (keyword.ToLower().Contains("health")) { try { bs.health = int.Parse(keyword.Split('=')[1]); } catch { bs.health = 0; } }
                                else if (keyword.ToLower().Contains("attack")) { try { bs.AttackRange = int.Parse(keyword.Split('=')[1]); } catch { bs.AttackRange = 30; } }
                                else if (keyword.ToLower().Contains("roam")) { try { bs.roamrange = int.Parse(keyword.Split('=')[1]); } catch { bs.roamrange = 30; } }
                                else if (keyword.ToLower().Contains("cooldown")) { try { bs.cooldown = int.Parse(keyword.Split('=')[1]); } catch { bs.cooldown = 15; } }
                                else if (keyword.ToLower().Contains("peacekeeper")) { bs.peacekeeper = true; }
                                else if (keyword.ToLower().Contains("radiooff")) { bs.radiooff = true; }
                                else if (keyword.ToLower().Contains("taunt")) { bs.taunts = true; }
                                else if (keyword.ToLower().Contains("height")) { try { bs.height = int.Parse(keyword.Split('=')[1]); } catch { bs.height = 0; } }
                                else if (keyword.ToLower().Contains("steamicon")) { try { bs.steamicon = ulong.Parse(keyword.Split('=')[1]); } catch { bs.steamicon = 0; } }
                                else if (keyword.ToLower().Contains("speed")) { try { bs.speed = int.Parse(keyword.Split('=')[1]); } catch { bs.speed = 0; } }
                                else if (keyword.ToLower().Contains("accuracy")) { try { bs.accuracy = int.Parse(keyword.Split('=')[1]); } catch { bs.accuracy = 50; } }
                                else if (keyword.ToLower().Contains("damageScale")) { try { bs.damageScale = int.Parse(keyword.Split('=')[1]); } catch { bs.damageScale = 100; } }
                                else if (keyword.ToLower().Contains("strip")) { bs.strip = true; }
                                else if (keyword.ToLower().Contains("mount")) { bs.mount = true; }
                                else if (keyword.ToLower().Contains("killnotice")) { bs.killnotice = true; }
                                else if (keyword.ToLower().Contains("apcattack")) { bs.apcattack = true; }
                                else if (keyword.ToLower().Contains("canheal")) { bs.canheal = true; }
                            }
                        }
                        //Create Dictornary reference
                        if (!NPC_Spawners.ContainsKey(prefabdata.position))
                        {
                            NPC_Spawners.Add(prefabdata.position, bs);
                        }
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
                    if (DebugInfo) Puts("Removed Invalid Prefab @ " + MapLockLocation);
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
                        if (DebugInfo) Puts("Door Fixed @ " + doorfix.transform.position.ToString());
                        //Scales door X to 0.1 so it still has some thickness to stop bot bullet and vison but not so thick player hits on it.
                        doorfix.transform.localScale = new Vector3(0.1f, doorfix.transform.localScale.y, doorfix.transform.localScale.z);
                    }
                }
            }
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info != null && info.InitiatorPlayer != null && entity != null)
            {
                BasePlayer player = info.InitiatorPlayer;
                if (IgnoredShots.Contains(player.userID))
                {
                    info.damageTypes.ScaleAll(0.1f);
                    IgnoredShots.Remove(player.userID);
                }

            }
        }

        void OnEntityDeath(BaseEntity entity, HitInfo info)
        {
            try
            {
                if (entity == null || info == null) return;
                //Dismount bots on seat break
                BaseMountable seat = entity.GetComponent<BaseMountable>();
                if (seat != null)
                {
                    if (seat._mounted.IsNpc)
                    {
                        seat._mounted.EnsureDismounted();
                    }
                }
                //Get player
                BasePlayer player = null;
                player = entity.ToPlayer();
                if (player == null) return;
                //Get who was the attacker
                BasePlayer attacker = null;
                attacker = player.lastAttacker.ToPlayer();
                if (attacker == null) return;
                //Failsafe so while loop cant get stuck going on for ever
                int failsafe = 0;

                //Taunt bot kills
                if (NPC_Bots.ContainsKey(attacker.userID))
                {
                    if (NPC_Spawners.ContainsKey(NPC_Bots[attacker.userID]))
                    {
                        if (NPC_Spawners[NPC_Bots[attacker.userID]].taunts)
                        {
                            //Pick a taunt at randm making sure not to be a repeat
                            if (Killed == null) return;
                            int seed = 0;
                            seed = Random.Range(0, Killed.Length - 1);
                            while (seed == lasttaunt && failsafe < 10)
                            {
                                failsafe++;
                                seed = Random.Range(0, Killed.Length - 1);
                            }
                            lasttaunt = seed;
                            //Send taunt to chat.
                            CreateTaunt(Killed[seed].Replace("{N}", player.displayName), attacker, NPC_Spawners[NPC_Bots[attacker.userID]].steamicon);
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
                        if (NPC_Spawners[NPC_Bots[player.userID]].killnotice)
                        {
                            //Send kill announcement to chat.
                            CreateAnouncment(attacker.displayName + " Killed " + player._name + " With " + info.damageTypes.GetMajorityDamageType());
                        }
                        if (NPC_Spawners[NPC_Bots[player.userID]].taunts)
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
                            CreateTaunt(Dead[seed].Replace("{N}", attacker.displayName), player.ToPlayer(), NPC_Spawners[NPC_Bots[player.userID]].steamicon);
                            return;
                        }
                    }
                }
            }
            catch { }
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
                    if (DebugInfo) Puts("No Navmesh found below Bot @ " + entity.transform.position.ToString() + " bot will be frozen in place");
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
                            if (DebugInfo) Puts("Moving Items");
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
            if (bot == null) return;
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

        void ParachuteSuiside(BasePlayer bot)
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
        void BotMeleeAttack(NPCPlayer bot, BasePlayer AttackPlayer, BaseEntity weapon, Rust.DamageType damagetype, float Damage, string sfx = "assets/bundled/prefabs/fx/headshot.prefab", int HeadshotPercentage = 10)
        {
            if (bot == null || AttackPlayer == null || weapon == null)
                return;

            //Create a delay for the animation
            float delay = 0.2f;
            try { delay = (weapon as BaseMelee).aiStrikeDelay; } catch { try { delay = (weapon as AttackEntity).animationDelay; } catch { } }
            timer.Once(delay, () =>
            {
                float range = 0.8f;
                try { range = (weapon as BaseMelee).maxDistance; } catch { try { range = (weapon as AttackEntity).effectiveRange; } catch { } }
                //Check weapons reach.
                if (Vector3.Distance(bot.transform.position, AttackPlayer.transform.position) < range)
                {
                    //Apply damage and play SFX
                    //AttackPlayer.Hurt(Damage, damagetype, bot, true);
                    if (UnityEngine.Random.Range(1, 101) <= HeadshotPercentage)
                        Effect.server.Run(sfx, AttackPlayer.transform.position);
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

        //Fix for bradly going nuts on NPCs
        private object CanBradleyApcTarget(BradleyAPC apc, BaseEntity baseentity)
        {
            if (baseentity == null) return null;
            BasePlayer player = baseentity.ToPlayer();
            if (apc == null && player == null) return null;
            if (NPC_Bots.ContainsKey(player.userID))
            {
                if (NPC_Spawners.ContainsKey(NPC_Bots[player.userID]))
                {
                    //Checks if doesnt have flag to allow NPC to attack
                    if (!NPC_Spawners[NPC_Bots[player.userID]].apcattack) return false;
                }
            }
            return null;
        }

        //Fix for NPCs with rocket launchers just firing at there feet.
        private void EmulatedFire(BaseProjectile _ProectileWeapon, NPCPlayer player, BasePlayer targetplayer)
        {
            if (_ProectileWeapon == null || _ProectileWeapon.HasAttackCooldown()) return;
            //Get distance from bot to player to work out rocket arch
            float AimDistance = Vector3.Distance(player.transform.position, targetplayer.transform.position);
            //Get direction of launcher
            Vector3 vector3 = _ProectileWeapon.MuzzlePoint.transform.forward;
            //Get height of launcher
            Vector3 position = _ProectileWeapon.MuzzlePoint.transform.position + (Vector3.up * 1.6f);
            //Create a new rocket
            BaseEntity entity = null;
            switch (_ProectileWeapon.ShortPrefabName)
            {
                case "rocket_launcher.entity":
                    entity = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_basic.prefab", position, player.eyes.GetLookRotation());
                    break;

                case "mgl.entity":
                    entity = GameManager.server.CreateEntity("assets/prefabs/ammo/40mmgrenade/40mm_grenade_he.prefab", position, player.eyes.GetLookRotation());
                    AimDistance += 5f;
                    break;

                default:
                    return;
            }
            if (entity == null) return;
            //Get rockets settings
            var proj = entity.GetComponent<ServerProjectile>();
            if (proj == null) return;
            //Set creator of rocket to bot
            entity.creatorEntity = player;
            //Set fly angle
            proj.InitializeVelocity(Quaternion.Euler(vector3) * entity.transform.forward * AimDistance);
            //Adjust explosive delay
            TimedExplosive rocketExplosion = entity.GetComponent<TimedExplosive>();
            if (rocketExplosion != null)
            {
                rocketExplosion.timerAmountMin = 1;
                rocketExplosion.timerAmountMax = 15;
            }
            //Make shot happen.
            entity.Spawn();
            _ProectileWeapon.StartAttackCooldown(_ProectileWeapon.repeatDelay);
        }

        public void FireWaterGun(BasePlayer ownerPlayer, BaseEntity wep)
        {
            //Get as item
            global::Item item = wep.GetItem();
            if (item == null)
            {
                return;
            }
            //Creates water
            ItemDefinition itemDefinition = ItemManager.FindItemDefinition("water");
            //Get direction
            Ray ray = ownerPlayer.eyes.BodyRay();
            //Draw water line
            Debug.DrawLine(ray.origin, ray.origin + ray.direction * 10f, Color.blue, 1f);
            //Do damage
            RaycastHit hitInfo;
            Physics.Raycast(ray, out hitInfo, 10f, 1218652417);
            List<Rust.DamageTypeEntry> damage = new List<Rust.DamageTypeEntry>();
            WaterBall.DoSplash(hitInfo.point, 2f, itemDefinition, 10);
            DamageUtil.RadiusDamage(ownerPlayer, wep.LookupPrefab(), hitInfo.point, 0.15f, 0.15f, damage, 131072, true);
        }

        //Make NPCs able to throw
        public void ServerThrow(Vector3 targetPosition, ThrownWeapon wep, NPCPlayer player)
        {
            if (wep == null || wep.HasAttackCooldown()) return;
            //Get aim direction
            Vector3 position = player.eyes.position;
            Vector3 vector3 = player.eyes.BodyForward();
            float AimDistance = 1f;
            //Trigger throw server code
            wep.SignalBroadcast(BaseEntity.Signal.Throw, string.Empty);
            //Create the nade
            BaseEntity entity = GameManager.server.CreateEntity(wep.prefabToThrow.resourcePath, position, Quaternion.LookRotation(wep.overrideAngle == Vector3.zero ? -vector3 : wep.overrideAngle));
            if ((UnityEngine.Object)entity == (UnityEngine.Object)null)
                return;
            //Set owner
            entity.creatorEntity = (BaseEntity)player;
            //Get throw arch
            Vector3 aimDir = vector3 + Quaternion.AngleAxis(10f, Vector3.right) * Vector3.up;
            float f = 6f;
            if (float.IsNaN(f))
            {
                aimDir = vector3 + Quaternion.AngleAxis(20f, Vector3.right) * Vector3.up;
                f = 6f;
                if (float.IsNaN(f))
                    f = 5f;
            }
            entity.SetVelocity(aimDir * f * AimDistance);
            if ((double)wep.tumbleVelocity > 0.0)
                entity.SetAngularVelocity(new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * wep.tumbleVelocity);
            //Make sure doesnt dud
            DudTimedExplosive dud = entity.GetComponent<DudTimedExplosive>();
            if (dud != null)
                dud.dudChance = 0f;
            entity.Spawn();
            wep.StartAttackCooldown(wep.repeatDelay);
        }

        public static bool IsFemale(ulong userID)
        {
            //Save current random state
            UnityEngine.Random.State state = UnityEngine.Random.state;
            //initilise in a known state so we already know the outcome of the random generator
            //Feed userid as the seed
            UnityEngine.Random.InitState((int)((uint)4332 + userID));
            //Determin gender
            bool Gender = (UnityEngine.Random.Range(0f, 1f) > 0.5f);
            //Reset state back to a random unknown state we saved.
            UnityEngine.Random.state = state;
            return Gender;
        }

        //Scans around the bot for seats, Put the bot in the closest seat to it.
        public void MountBot(BasePlayer bot)
        {
            List<BaseMountable> Seats = new List<BaseMountable>();
            Vis.Entities<BaseMountable>(bot.transform.position, MountScan, Seats);
            BaseMountable closest_seat = null;
            //Finds closest seat thats not already mounted
            foreach (BaseMountable seat in Seats)
            {
                if (seat.HasFlag(BaseEntity.Flags.Busy)) continue;
                if (closest_seat == null) closest_seat = seat;
                if (Vector3.Distance(bot.transform.position, seat.transform.position) <= Vector3.Distance(bot.transform.position, closest_seat.transform.position))
                    closest_seat = seat;
            }
            //Trys to mount seat
            if (closest_seat != null)
            {
                closest_seat.GetComponent<BaseMountable>().AttemptMount(bot);
                closest_seat.SendNetworkUpdateImmediate();
                bot.SendNetworkUpdateImmediate();
                if (plugin.DebugInfo) plugin.Puts(bot.displayName + " Forced into Seat @ " + closest_seat.transform.position.ToString());
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
            //Remove Default kit
            ItemManager.DoRemoves();
            foreach (Item i in bot.inventory.AllItems())
            {
                i.Remove();
            }
            ItemManager.DoRemoves();

            //Try apply the kit
            Kits?.Call("GiveKit", bot, Skin);
            //Trys to equip stuff after a delay for kits plugin to of ran
            Item projectileItem = null;
            //Find first gun
            timer.Once(0.5f, () =>
             {
                 foreach (var item in bot.inventory.containerBelt.itemList.ToList())
                 {
                     if (item.GetHeldEntity() is BaseProjectile)
                     {
                         projectileItem = item;
                         break;
                     }
                    //Move medial items out of hot bar
                    if (item.GetHeldEntity() is MedicalTool)
                     {
                         if (bot.inventory.containerBelt.GetSlot(5) != null)
                         {
                             bot.inventory.containerBelt.GetSlot(5).MoveToContainer(bot.inventory.containerMain);
                         }
                         item.MoveToContainer(bot.inventory.containerBelt, 5);
                         continue;
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
                    foreach (var item in bot.inventory.containerBelt.itemList.ToList())
                     {
                        //Move medial items out of hot bar
                        if (item.GetHeldEntity() is MedicalTool)
                         {
                             if (bot.inventory.containerBelt.GetSlot(5) != null)
                             {
                                 bot.inventory.containerBelt.GetSlot(5).MoveToContainer(bot.inventory.containerMain);
                             }
                             item.MoveToContainer(bot.inventory.containerBelt, 5);
                             continue;
                         }
                         if (item.GetHeldEntity() is BaseMelee)
                         {
                             projectileItem = item;
                             break;
                         }
                     }
                    //Try pull out active weapon
                    try
                     {
                         bot.UpdateActiveItem(projectileItem.uid);
                         bot.inventory.UpdatedVisibleHolsteredItems();
                     }
                     catch { }
                 }
                //Try get gun ready.
                try
                 {
                     timer.Once(1f, () =>
                    {
                     (bot as NPCPlayer).AttemptReload();
                 });
                 }
                 catch { }
                //Only do this if bot wants items replaced on the corpse
                if (replacement)
                 {
                     timer.Once(0.5f, () =>
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
                     if (NPC_Items.ContainsKey(bot.userID))
                     {
                         NPC_Items[bot.userID] = items;
                     }
                     else
                     {
                         NPC_Items.Add(bot.userID, items);
                     }
                 });
                 }
             });
        }
    }
}