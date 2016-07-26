<!-- define warning icon -->
[1.1]: http://i.imgur.com/M4fJ65n.png (ATTENTION)
[1.2]: http://i.imgur.com/NNcGs1n.png (BTC)
<!-- title -->
<h1>A Pokemon Go Bot based on Spegeli Fork - PokeRodie</h1>
<!-- disclaimer -->
![alt text][1.1] <strong><em> The contents of this repo are a proof of concept and are for educational use only </em></strong>![alt text][1.1]
<br/>

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
<br /><br /><h2><a name="features">New Features - Only in PokeRoadie</a></h2>
 - [Global Destinations]<br />Create a list of waypoints around the world.<br />Spend x minutes (60 by default) farming in each location.<br />Fly between global destinations at high speed (no catch).<br />Or, Drive to global destinations while catching!<br />Setup elaborate routes to hotspots everywhere!
 - [Prioritize Stops with Lures]<br />Visit stops with lures first.<br />If there are multiple stops with lures, bounce back and forth a little.
 - [Statistics every x minutes]<br />Detailed Pokemon stats.<br />Detailed Destination stats.<br />Ability to log all Pokemon.<br />
 - [Dynamic Speed]<br />Dynamically Adjust speed between 2-55kmph based upon next waypoint distance, and the time to get there.<br />Randomize Speed slightly to present more realistic simulation.
<br/>
<hr/>
<br/>

<h2><a name="credits">Credits</a></h2>
A big thank you goes to Ferox, Necronomicon, Spegeli <3
<br/>
