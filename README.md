 <h2>RECustomBots</h2>
 This plugin modifies the bots getting spawned by rust edits NPC Spawners.<br>
Just prefab group the NPC Spawn to a placeholder prefab (Random Switch Deployed by default)<br>
And set the name of the prefab group to have what settings you want to change. <br>
<br>
<br>
  Avaliable keywords
<br><br>

 * name=&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;if set it will apply a specific name to the bot, Other wise will pick one at random based on userid the spawner creates.
 * kit= &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;if set will apply kit to bot, Can do male/female specific kits by using ^ between them kit=malekit^femalekit
 * stationary &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;if this keyword is present bot will remain stationary.
 * parachute &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;    if this keyword is present bot will parachute to navmesh.
 * replace  &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;     if this keyword is present bot replace default items with kit items.
 * strip   &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;     if this keyword is present bot will strip all its loot on death.
 * radiooff   &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;   if this keyword is present bot will not use the radio to chatter.
 * peacekeeper &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;  if this keyword is present bot will only fire on hostile players.
 * mount   &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;     if this keyword is present bot will mount the closest seat.
 * taunt     &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;    if this keyword is present bot will make taunts in the chat to players it interacts with.
 * killnotice  &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;  if this keyword is present kills/deaths of this bot will be announced to chat.
 * health=    &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;   if set will adjust the bots health to this example health=150     
 * attack=   &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;    if set the bot will attack only up to this range.
 * roam=      &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;   if set this is how far the bot can move from its home spawn before it wants to return.
 * cooldown=  &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;   if set changes the default home check rate.
 * height=     &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;  if set can make small adjustment to bots navmesh height usual settings range will be -3 to +3.
 * speed=     &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;   if set adjusts how fast the bot can run.
 * steamicon=  &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;  if set the bot will use this steam profiles icon example steamicon=76561198188915047

<br>

 <h2>How To Use:</h2>
Create a NPC Spawner and set the type of bot you want.
Create a placeholder prefab, By default this is Random Switch Deployed prefab.
You can change this placeholder in the plugin file to any other prefab you wish to use.

Copy the NPC Spawner position. And paste that position into the placeholders so they are both centered.
Now select both and create a selection group.<br>The name you give this is what will set up the bots from that spawner.
Keywords can be in any order. Not providing a keyword will use the default setting. If the setting has a = in it you must provide a number after the =<br><br>
Example:<br>If you wanted a bot that was just stationary and no other changes youd call it<br>BMGBOT.stationary<br><br>If you wanted a bot that parachutes in and has 500 hp youd enter<br>BMGBOT.parachute.health=500
<br><br>
Here is a video to show it being done.<br>
VIDEO:<br>https://youtu.be/ckGnEDdqwZQ<br><br>
You can also do gender based kits by providing a ^ between the male and female kit.
As shown the screen shot. That will give male bots the kit called "malekit" and female bots the kit called "femalekit"<br>
The setting replace will remove the default items of that bot and use the kits items.<br>
<p><img src="https://github.com/bmgjet/RECustomBots/raw/main/RECustomBotsImage.jpg" /></p>

<br> <h2>Advanced settings in the plugin:</h2>
These can be edited inside the plugin cs file.<br><br>
* Debug = false or true This setting will show debug info in the console if set to true<br>
* MapLockLocation Set the X,Y,Z locations of a invalid prefab here to use it as copy protection for your map. A example would be place a pumpjack item at some location under the map. Then copy and paste its position in here.
* FixDoors = true or false. This setting will scale doors X-axis to 0.1 to allow players to move around them but still provide some navmesh to bots.
* prefabplaceholder this is the path of the prefab your using as placeholder for grouping with.
* bot_tick how fast the scripts tick rate is, 1 = once a second 0.50 wouldnt be uncommon to increase responsiveness of bots.
* ScanDistance is how far away from the npc spawner the placeholder prefab can be at most to be detected.
* MountScan How much of a area to seach for mountable seats
* AnnouncementIcon enter a steamid for the profile you want the kill/death announcer to take its icon from.
* color the color of the announcer name example red,orange,blue
* SleepProtect Stops custom bots from shooting at sleepers
* ChuteSider timer that will destroy the parachute after X seconds if it gets stuck on something
* ColliderDistance How close to a object before parachute is removed automatically
* private string[] Dead is a collection of things the bot will say on its death if taunting is enabled.Using {N} replaces that with the players name.
* private string[] Killed is a collection of things the bot will say when it kills a player. Using {N} replaces that with the players name.
