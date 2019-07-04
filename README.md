TestLite
-------------------------

**TestLite** is a mod for the Squad game [Kerbal Space Program](https://www.kerbalspaceprogram.com/). It's a lightweight replacement for other part-failure mods like *TestFlight*, limiting configurability and extensibility in exchange for simplicity and *(hopefully)* better performance.  It is designed for use with the [Realism Overhaul suite of mods](https://github.com/KSP-RO).

TestLite is currently in *beta*; it hasn't had much testing, and there may still be one or two missing features.

TestLite is developed by [Edward Cree](https://github.com/ec429), aka *`soundnfury`*.

TestLite is licensed under the [MIT License](https://github.com/ec429/TestLite/blob/master/LICENSE).  Its source code can be found [here](https://github.com/ec429/TestLite).

What are its dependencies?
--------------------------

* The plugin depends directly on [RealFuels](https://github.com/NathanKell/ModularFuelSystem/releases).  
* The reliability configs come from
 [RealismOverhaul](https://github.com/KSP-RO/RealismOverhaul/releases) *(which thinks it's creating them for TestFlight, but we
 steal them first)*.
* And of course you need [ModuleManager](https://github.com/sarbian/ModuleManager/releases).

How do I install it?
--------------------

Assuming you've installed all the dependencies *(see above)*, copy the `GameData/TestLite` folder into your KSP install's `GameData/`, then delete `GameData/RealismOverhaul/TestFlight_Generic_Engines.cfg`.

Where do I report bugs?
-----------------------

Open an issue on [the project's GitHub page](https://github.com/ec429/TestLite/issues). Sometimes the developer will be on [IRC](irc.esper.net) as *`soundnfury`* on [irc.esper.net](irc.esper.net), or on the [KSP-RO Discord server](https://discordapp.com/invite/3xMtyBg) under the same nickname.
