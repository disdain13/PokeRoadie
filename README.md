<!-- define warning icon -->
[1.1]: http://i.imgur.com/M4fJ65n.png (ATTENTION)
[1.2]: http://i.imgur.com/NNcGs1n.png (BTC)
<!-- title -->
<h1>PokeRoadie</h1>
<h2>Advanced Pokemon Go Bot</h2>
<img src="https://github.com/disdain13/PokeRoadie/blob/master/PokemonGo_Bot_Screenshot_2.png?raw=true" alt="PokemonGo_Bot_Screenshot_1.png" title="PokeRoadie PokemonGo Bot"><br/><br/>
<img src="https://github.com/disdain13/PokeRoadie/blob/master/PokemonGo_Bot_Screenshot_1.png?raw=true" alt="PokemonGo_Bot_Screenshot_1.png" title="PokeRoadie PokemonGo Bot"><br/>
A big thank you goes to Ferox, Necronomicon, Spegeli <3<br/>
Based on Spegeli Version : https://github.com/Spegeli/Pokemon-Go-Rocket-API
<br/><br/>
<h2><a name="features">Easy To Use!</a></h2>
 - <a href="https://github.com/disdain13/PokeRoadie/releases" title="Download Pokemon Bot Release"/><b>Download the latest release.</b></a>
 - Unzip to any directory
 - <b>Google Auth</b> (default)<br/>Just run PokeRoadie.exe - When you start the application, it will require you to log into google and approve the application as a "device". We have made this simple as possible. On start, it will pop up a google window. Log in if you are not already. When the google window asks for the code, just hit control-v in the "Enter Code" box. The code will already be on your clipboard. hit submit, and approve the interaction. Once you have done that, the program will continue to run and play the game for you.
 - <b>Ptc Auth</b><br/>Open <i>Configs\Settings.xml</i>. Change <b>AuthType</b> from "Google" to "Ptc". Replace value of <b>PtcUsername</b> and <b>PtcPassword</b> values with your login credentials from the PokemonGo site. Save the file, run PokeRoadie.exe<br/>
 
<h2><a name="features">PokeRoadie New Features</a></h2>
 - <b>Pokemon Moves Imported</b><br/>All the move information imported.<br/>Used in NEW pokemon value calculations.
 - <b>Pokemon True Value</b><br/>Developed a new formula to calculate the <i>True Value</i> of pokemon.<br/>Value is based on CP, Perfect %, Level, Move Power, and Move Accuracy.<br/>Can now use value "V" in <b>PriorityType</b> setting to Transfer based on <i>True Value</i>.<br />Can set <b>KeepAboveV</b> setting to keep any pokemon equal to or above the specified <i>True Value</i>.<br/>
 - <b>Global Destinations</b><br />Create a list of waypoints around the world.<br />Spend x minutes (60 by default) farming in each location.<br />Fly between global destinations at high speed (no catch).<br />Or, Drive to global destinations while catching!<br />Setup elaborate routes to hotspots everywhere!<br/>Manage destinations through the DestinationCoords.ini file.
 - <b>Lure Awareness</b><br />Visit stops with lures first.<br />If there are multiple stops with lures, bounce back and forth a little.<br/>Destinations with lures will say "WITH LURE" after the name.
 - <b>Statistics every x minutes</b><br />Detailed Pokemon stats.<br />Detailed Destination stats.<br />Ability to log all Pokemon.<br />Detailed Inventory Stats.<br/>
 - <b>Dynamic Speed</b><br />Dynamically Adjust speed between 2-55kmph based upon next waypoint distance, and the time to get there.<br />Randomize Speed slightly to present more realistic simulation.
 - <b>PokeBall Selector</b><br/> Will now substitute a slightly better/worse ball based on inventory availability.<br/>
 - <b>Settings Re-Write</b><br/> Have re-written the entire settings system to work out of the "Configs" directory.<br/>No longer using user specific settings stored in the AppData.<br/>
<br/><br/>
<h2><a name="settings">PokeRoadie Settings</a></h2><br/>(Under Construction)<br/>
 - <b>AuthType</b><br/>The type of login authentication for PokemonGo. Valid values are (default) <b>Google</b>, and <b>Ptc</b>. 
 - <b>CatchPokemon</b><br/>
 - <b>CatchWhileFlying</b><br/>
 - <b>CurrentAltitude</b><br/>
 - <b>CurrentLatitude</b><br/>
 - <b>CurrentLongitude</b><br/>
 - <b>DestinationIndex</b><br/>
 - <b>DestinationsEnabled</b><br/>
 - <b>DisplayAllPokemonInLog</b><br/>
 - <b>DisplayRefreshMinutes</b><br/>
 - <b>EnableSpeedAdjustment</b><br/>
 - <b>EnableSpeedRandomizer</b><br/>
 - <b>EvolveOnlyPokemonAboveIV</b><br/>
 - <b>EvolveOnlyPokemonAboveIVValue</b><br/>
 - <b>EvolvePokemon</b><br/>
 - <b>FlyingEnabled</b><br/>
 - <b>FlyingSpeed</b><br/>
 - <b>GPXFile</b> (Not Currently Supported)<br/>
 - <b>KeepAboveCP</b><br/>
 - <b>KeepAboveIV</b><br/>
 - <b>KeepAboveV</b><br/>
 - <b>LoiteringActive</b><br/>
 - <b>MaxSecondsBetweenStops</b><br/>
 - <b>MaxDistance</b><br/>
 - <b>MinutesPerDestination</b><br/>
 - <b>MoveWhenNoStops</b><br/>
 - <b>NotTransferPokemonsThatCanEvolve</b><br/>
 - <b>PrioritizeStopsWithLures</b><br/>
 - <b>PtcPassword</b><br/>
 - <b>PtcUsername</b><br/>
 - <b>TransferPokemon</b><br/>Enable automatic transfers based on value specified by the <b>PriorityType</b> setting.<br/>
 - <b>KeepDuplicateAmount</b><br/>The acceptable number of each species to keep based on value specified by the <b>PriorityType</b> setting. The default value is 2.<br/>
 - <b>UseGPXPathing</b> (Not Currently Supported)<br/>
 - <b>UseLuckyEggs</b><br/>Specifies if a Lucky Egg should be used when automatically evolving pokemon.
 - <b>UsePokemonToNotCatchList</b><br/>Specifies if the <i>Configs\PokemonsNotToCatch.ini</i> file should be used to ignore specific pokemon. Pokemon listed in this file will be ignored during encounters.<br/>
 - <b>MinSpeed</b><br/>The minimum speed (in kilometers per hour) your player will walk/drive when exploring a destination for pokestops and pokemon.<br/>
 - <b>MaxSpeed</b><br/>The maximum speed (in kilometers per hour) your player will walk/drive when exploring a destination for pokestops and pokemon. Setting this above 80 usually results in pokemon fleeing during encounters.<br/>
 - <b>PriorityType</b><br/>Determines how to prioritize pokemon for transfer and evolution. The valid setting values are (default) <b>V</b> for <i>True Value</i>, <b>IV</b> for Perfection %, and <b>CP</b> for Combat Points.<br/>
<br/><br/>
<h2><a name="features">Spegeli Original Features</a></h2>
 
 - [PTC Login / Google]
 - [Humanlike Walking]<br />
 - [Configurable Custom Pathing]<br />
   (Speed in km/h is configurable via UserSettings)
 - [Farm Pokestops]<br />
   (use always the nearest from the current location)<br />
   (Optional: keep within specific MaxTravelDistanceInMeters to Start Point) (MaxTravelDistanceInMeters configurable via UserSettings)
 - [Farm all Pokemon near your]<br />
   (Optional: PokemonsNotToCatch List. Disabled by default, can be Enabled via UserSettings, configurable Names via File in Config Folder)
 - [Evolve Pokemon]<br />
   (Optional: Enabled by default, can be Disabled via UserSettings)<br />
   (Optional: PokemonsToEvolve List - Only Pokemons in this List will be Evolved, configurable via File in Config Folder)<br />
   (Optional: EvolveOnlyPokemonAboveIV - Will Evolve only Pokemon with IV > EvolveAboveIVValue, Disabled by default, can be Enabled vis UserSettings)
 - [Transfer duplicate Pokemon]<br />
   (ignore favorite/gym marked)<br />
   (Optional: Enabled by default, can be Disabled via UserSettings.)<br />
   (Optional: PrioritizeIVOverCP - Determines the sorting sequence - CP or IV, Disabled by default, can be Enabled via UserSettings.)<br />
   (Optional: KeepMinDuplicatePokemon - The amount of X best Pokemon he should keep, 2 by default, configurable via UserSettings)<br />
   (Optional: PokemonsNotToTransfer List - Pokemon on this List will be not Transfered, configurable via File in Config Folder)<br />
   (Optional: NotTransferPokemonsThatCanEvolve - Will keep all Pokemons which can be Evolve not matter if they on PokemonsToEvolve List or not, Disabled by default, can be Enabled via UserSettings)
 - [Throws away unneeded items]<br />
   (configurable via UserSettings)
 - [Use best Pokeball & Berry]<br />
   (depending on Pokemon CP and IV)
 - [Creates Excel CSV File on Startup with your current Pokemon]<br />
   (including Number, Name, CP,Perfection and many more) (can be found in the Export Folder)
 - [Log File System]<br />
   (all activity will be tracked in a Log File)
 - [Random Task Delays]
 - [Statistic in the Header]
 - [Very color and useful Logging]<br />
   (so you every time up2date what currently happened)
 - and many more ;-)

<hr/>
<br/>


