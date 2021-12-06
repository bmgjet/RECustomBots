Set Custom Prefab Name of a NPC Spawner
Identifiyer Seperated by "."<br>
BMGBOT.Kitname.Stationary.Health.AttackRange.Roamrange.Cooldown.Replaceitems.Parachute.Name
<br> 
*  Kitname = the name of the kit to put on the bot. Leave blank for no kit. If using gender based kits seperate kitnames Male^Female 
*  Stationary = 0 false / 1 true. Not move from spawner unless triggered
*  Health = how much health bot has, 0 for default
*  AttackRange = How far before bot attacks triggers
*  Roamrange = How far the bot can get from its spawner before it returns
*  Cooldown = How long with no activity before it checks roam range.
*  Replaceitems = 0 false / 1 true, will replace default corpse loot with kit loot.
*  Parachute = 0 false / 1 true , will parachute to navmesh
*  Name = custom display name to show on bot leave blank for random name
<br>
How To Video: https://youtu.be/1FzqEGXRDyk
<br>
 <h2>Example: BMGBOT.autokit.0.500.30.50.30.1.0.Starter Bot</h2>
 <br>
That makes that spawner create a bots wearing autokit, <br>
They arent stationary, They Have 500HP, Can shoot upto 30f <br>
Wont return home when within 50f (if player within attack range it wont return home) <br>
Does Home check after 30 ticks of no activity. <br>
Replaces the default corpse with kit items. <br>
Give bot name of Starter Bot. <br>
<p><img src="https://raw.githubusercontent.com/bmgjet/RECustomBots/main/Help-Instructions.jpg" /></p>
