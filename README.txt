TestLite
========

TestLite is a mod for the Squad game "Kerbal Space Program".
It's a lightweight replacement for other part-failure mods like TestFlight,
 limiting configurability and extensibility in exchange for simplicity and
 (hopefully) better performance.  It is designed for use with the Realism
 Overhaul suite of mods.

TestLite is currently in beta; it hasn't had much testing, and there may
 still be one or two missing features.

TestLite is developed by Edward Cree, aka 'soundnfury'.

TestLite is licensed under the MIT License.  Its source code can be found at
 <https://github.com/ec429/TestLite>.

What are its dependencies?
--------------------------

The plugin depends directly on RealFuels.  The reliability configs come from
 RealismOverhaul (which thinks it's creating them for TestFlight, but we
 steal them first).
And of course you need ModuleManager.

How do I install it?
--------------------

Assuming you've installed all the dependencies (see above), copy the
 GameData/TestLite folder into your KSP install's GameData/.

How do I build it?
------------------

If you want to help develop TestLite, there are two ways to compile it from
 source.  The canonical and supported way is through the Unix makefile;
 ensure that $(HOME)/ksp is a KSP install (or more likely, a symlink to it),
 then run 'make' in src/.  The DLL will be copied into GameData.
As an alternative, Windows users can build with the VS solution file; this
 was provided by siimav <siim.aaver@gmail.com>, so go pester him if it
 doesn't work — soundnfury is unable to provide support.

Where do I report bugs?
-----------------------

Open an issue on the project's GitHub page,
	https://github.com/ec429/TestLite/issues
Sometimes the developer will be on IRC as soundnfury on irc.esper.net, or on
 the KSP-RO Discord server under the same nickname.

Acknowledgements
----------------

TestLite could never have existed without the example set by John Vanderbeck
 (Agathorn), whose original TestFlight mod created the basic concepts used
 here, and who produced most of the original engine reliability configs for
 Realism Overhaul (which TestLite now consumes).
