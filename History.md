
n.n.n / 2025-03-23
==================

  * os-webrtc-janus: initial compiling addition of os-webrtc-janus to NGC (#107)
  * Update test  class for interface change.
  * Merge remote-tracking branch 'upstream/master' ithrough 3/19 into hotfix/0.9.3.9208
  * Add hotfix branch with version# 9208
  * Update dotnetci.yml
  * Merge branch 'release/0.9.3.9037'
  * Fix for the script reset that was occuring exactly once on region startup.  If you are loading script state from a region that was not previously running experiences a variable would be null and the null needed to be tested for when parsing a UUID.  After this initial load the experience entry is saved and exists on future restarts.  Also bumped the version number to 9037
  * Merge tag '0.9.3.9030' into develop
  * Merge branch 'release/0.9.3.9030'
  * Example entry for alias service
  * Bump version for changes
  * Merge remote-tracking branch 'origin/develop' into release/0.9.3.9030
  * Fixed silly mistakes (#95)
  * Correct the case of the UserAccounts table name in migration statement for display names.  The Table is actually MixedCase where the original statement was using all lower case.
  * Merge branch 'feature/load-oar-enhancements' into develop
  * Cache negative lookups for aliases when doing lookups so a missed entry doesnt hammer the alias connector.   Log a message the first time we lookup an alias and its not found.  Bump version number.
  * Add options to OAR handling to allow disabling use of a defaultUser if no local user is found and to resolve from an alias if one is defined.  Default should work as an oar currently does.
  * Never call Environment.Exit() from a library,  This terminates the process on an estate lookup that fails if the estate doesnt exist, Do a null exit to signal no available data and let the caller decide how to handle it.
  * Display Names (#94)
  * Create xinv.php (#92)
  * Add a location for helper scripts and tools to live
  * Upstream changes through 9/5, 2024 (#91)
  * Update message used when blocking unsupported viewers. (#89)
  * Bump Version Numer - 8999
  * Unecessary cast,  the object is already of the cast type.
  * Local branch for StolenRuby experience changes (#86)
  * Merge tag 'opensim-rel-0.9.3.8984' into develop
  * Merge branch 'release/opensim-rel-0.9.3.8984'
  * Fix fat fingered typo.
  * Dont call Add when updating a kvp in the LinksetData list.  Add throws if a key already exists.  Use an array operator [] which handles add or update/
  * Get rid of the meaningless 0 in the 4 digit version number.   It's now 0.9.3.generationnumber.
  * Limit output if we hit a desrtialization exception due to format change.  This isnt really an exceptional condition and we handle it by parsing old format.  Log it as Debug.
  * Features/upstream-08062024 (#84)
  * Feature/lsd fix (#83)
  * Bump System.Text.Json (#79)
  * Upgrade Mailkit and MimeKit to >= 4.7.1 to address a Github reported vulnerability. The issue is actually in a dependency but this changes picks that up.
  * Merge tag '0.9.3.0.8939' into develop
  * Merge branch 'release/0.9.3.0.8939'
  * Look for default config files in dll.config instead of exe.config because of dotnet changes.
  * Add a default value of NULL_KEY for PBR Terrain textures to keep mixed grids of new and old code happy.  Probably not an issue ultimately but during migration to the new code its likely to be.    Bump the version number.
  * Merge remote-tracking branch 'upstream/master' through 6/19/2024 into develop
  * Merge tag '0.9.3.0.8915' into develop
  * Merge branch 'release/0.9.3.0.8915'
  * Log message at Info level when an Asset PUT/POST is done to create a new asset. We log IP address, Asset name and type, creator and description.
  * Set RenderMinHeight to Z = 0 and clamp values to that.
  * bump version number
  * Merge branch 'feature/8882-fixes' into develop
  * Remove duplicate log4j reference in OpenSim.Framework
  * A bit more defensive code around serialization.   Still causing issues. Im considering moving it to the SOG (in memory) as Core did.  There is no sog table so persistence is the same to the root part.
  * Be a bit more defensive on handling LinksetData items.  Check for empty or null key, etc.
  * Commit the same changes for LinkSetData,cs after save
  * Clean up a few LinkSet Data calls and make sure we lock around state changes.
  * Clean up references to runtime classes that are explicitely listed and coming from bin
  * Remove reference to System.Security. It comes from the framework.
  * Merge remote-tracking branch 'upstream/master' into feature/8882-fixes
  * use hash.HashData() and other cosmetics
  * change pgsql UUID casting (UNTESTED :( )
  * fix compile with new npgsql
  * update npgsql to version 8.0.2 (from nuget) set its sll mode to disable. UNTESTED :(
  * make some use of frozendicionaries on xml processors
  * Remove some cruft from earlier merge around LinkSetData.  We don't store it in the SOG.
  * mantis 9127 add missing linksetdata serializer
  * Missing BEGIN for MatOvrd migration in primshapes. It fails and the migration code continues on.  If The mgrations table  says its done through 65 in RegionStore you will likely have to apply this manually.
  * Merge tag '0.9.3.8882' into develop
  * Merge branch 'release/0.9.3.8882'
  * add another asset
  * Merge remote-tracking branch 'upstream/master' into release/0.9.3.8882
  * Remove references to System.Web.  Thats a .NET Framework thing.
  * Merge remote-tracking branch 'upstream/master' into release/0.9.3.8882
  * Bump Version Number
  * Update workflow to use dotnet 8 when building
  * Merge branch 'feature/pbr-on-mono' into develop
  * Merge remote-tracking branch 'upstream/master' into feature/pbr-on-mono
  * Put back the crappy dll copying for now.  There are other changes that require c# 12 which is a part of dotnet 8 and the use of System.Drawing.Common in dotnet 8 is entirely unsupported now.  I may just have to live with this and get the System.Drawing porting work out of the way.
  * Remove tests as they are non functional. We will add them back in later.  Remove some funkiness with copying of DLLs for System.Drawing.  We're pulling the v6.0 version from Nuget which is bad enough but at least this way its pinned or replaced in code.
  * Resolve some missing references. Specifically Cecil so plugins work and load.
  * Bump to DotNet Core 8.0 to match upstream.  They are still disabling dependency generation and checking which disables a big security element of the framework. More on that in Tranquillity.
  * Catch up merge with 0.9.3.0.  This is just simply a merge of core into the mono tree.  It lacks the refactoring thats in the Tranquillity work and hence has some of the issues that net6 on core has.  But perhaps it will be a step in the direction of a more architecturally sound version.
  * .. and bad merge
  * Cherry Pick through fb1ef7f7844120279e0279712163cc4708c99aad
  * Merge tag 'opensim-rel-0.9.2.2.8738' into develop
  * Merge branch 'release/opensim-rel-0.9.2.2.8738'
  * Bump version in prep for a release
  * Bug fixes for LinkSet Data related to matching SL behavior (#68)
  * Bump version number
  * LinkSet Data feature from SL (#64)
  * Next release is OpenSim-NGC Tranquillity
  * Fix clamp: focus of the image (hardness / softness) – range -20 to +20. (#62)
  * Merge branch 'feature/getwallclock' into develop
  * Fix for llGetWallclock() to return PST as wallclock time regardless of the current setting of the system clock.  Requires setting GetWallclockTimeZone in the OpenSim.ini XEngine or YEngine config sections respectively to define the timezone to use (the name can vary depending on system type and configuration).  The default is that GetWallclockTimeZone is undefined and the system local time will be returned which was the previous default behaviour.
  * Simplify the return from GetWallclock.  LSL_Floats are internally doubles so no need to cast to float.
  * llGetWallclock() should return time in PST regardless of what the system timezone is set to.  Its also supposed to truncate to whole seconds.  The existing implementation did neither.  Use TimeZoneInfo to insure we return PST.
  * Merge tag 'opensim-rel-0.9.2.2.8617' into develop
  * Merge branch 'release/opensim-rel-0.9.2.2.8617'
  * Fix for exception when reading classifieds in the profile module.  Category is a varchar string in the database but was treated as an int.  Changed the code to call System.Convert.ToInt32 which will handle an object and convert correctly regardless of the table type.  This way the same code should work on core or on a database where the database Category has been migrated to an int.  Bumped version to 8619
  * Fix an issue with profile fetching query where its passing a UUID down to the query incorrectly.   Did an exhaustive search of all the statements building queries and found 2 more unhandled cases which I also fixed. Bumped the version number to 8617
  * Merge tag 'opensim-rel-0.9.2.2.8603' into develop
  * Merge branch 'release/opensim-rel-0.9.2.2.8603'
  * Rework Gloebit Subscriptions and Transactions to use the native database type (strings) for ID based entries instead of UUIDs.  You can't hand a UUID to the database layer because it doesnt know how to convert it (or if it should).  This means the MoneyModule is doing some conversions to/from UUIDs using ToString and UUID.Parse when persisting data but that actually correct unless we revise the database types to use a MySQL Guid (in which case we would have to convert to that). Bumped Build Version to 8615.
  * Merge branch 'feature/gloebit-configuration' into develop
  * Bump version to 8603
  * Add a log message on intiialization indicating wether the Gloebit Noney Module is enabled or disabled.  Debug Aid.
  * Rework the Gloebit startup logic so its consistent with the other money modules and allows more than one money module to be present.  This will short-circuit some startup actions if the Gloebit module is not configured which is really more correct anyway.  It shouldn't be trying to start at all in that case.
  * Missed a database update where UUID is used. Bumped Version
  * One more database change.  Add an options field for the MoneyServer connect string so we can add things like OldGuids=true
  * Merge branch 'feature/mysql-connector' into develop
  * Bump version number
  * A few more cases of needing ToString() on an UUID for persistence, Found by inspection walking through each of the persistence modules.
  * Additional changes in Region persistence to explicitely call ToString() with UUID rather than assume the database command handler will do it for you.  This is a compatability issue with SqlConnector vs the Oracle connector.
  * Migrate from the Oracle Mysql connector to the more performance Pomello MySqlConnector dll.
  * Remove Cores CI scripts so we dont accidentally run their actions
  * Merge tag 'opensim-rel-0.9.2.2.8510' into develop
  * Merge branch 'release/opensim-rel-0.9.2.2.8510'
  * Disable region_owner_is_god by default.
  * Bump version (8510)
  * Remove prebuild.xml. We dont use prebuild to generate a solution any longer.
  * Merge remote-tracking branch 'upstream/master' into develop
  * Merge tag '0.9.2.2.8451' into develop
  * Merge branch 'release/0.9.2.2.8451'
  * Release notes
  * Remove some uneeded references to System.Drawing to constrain the problem a bit.   Bump version number (8451).
  * Comment out load oar help text until the new code is fully ready.
  * Revert to Core behaviour for OAR Restore for now.
  * Merge remote-tracking branch 'upstream/master' into develop
  * Merge tag '0.9.2.2.8425' into develop
  * Merge branch 'release/0.9.2.2.8425'
  * Tweaking BUILDING.md for updated location of build output.
  * Update Build instructions for dotnet based method
  * Bump version number. Pick up changes through 1-24-2023
  * Upstream changes through 1-24-2023
  * Bump build version to 8424.  opensim-rel-0.9.2.2.8424
  * Dont use the addins xml definitions for CoreModules just yet.  Need to rationalize all the modules names against the config files to verify modules are loaded correctly.  We're using the old style Assembly information for now which matches up with the annotations in the files.
  * Changes to get addins to initialize properly with NuGet version.
  * Fix a typo introduced in the merge for Groups addons.
  * Fix up resource location for sample addin.xmk
  * Merge -maint down to develop for testing.  We're using the static AssemblyInfo.cs again instead of the generated values. Hopefully this helps to address plugin loading.
  * Bump version number to 2022-12-23
  * Merge remote-tracking branch 'upstream/master' into sasquatch-prebuild-maint
  * Placeholder plugin definitions in xml for coremodules
  * Changes to take plugin metadata from XML instead of annotations.
  * Duplicated code from merge in RegionModulesControllerPlugin.cs.  Generate VersionInfo using template.
  * Bavl merge develop into addins branch
  * Pick up addin manifest from Resources.
  * Remove Mono.Data.SqlLite in preference to the default upstream version.
  * Remove 2 more dlls and replace with nuget variabnts.   Also pickup updates including mono.addins which has a patch for threaded access to the metadata database.
  * Update test methods to use new attribiutes for pre/post setup. The old ones are deprecated.
  * Remove Ionic.Zip.dll, libzlib.net and log4net.dll from bin and instead pull from Nuget a replacement version that is also compatible with net6.  The Log4Net change is especially important because we want to use the logging interfaces in dotnet but wont be able to switch everything over day one. So we'll use the log4net logging provider and we can tackle it in chunks.
  * set binary/text attributes correctly
  * Merge remote-tracking branch 'upstream/master' through 10/27/2020 into develop
  * Merge tag '0.9.2.8309-release' into develop
  * Merge branch 'release/0.9.2.8309-release'
  * Additions from upstream through 10/20
  * Merge remote-tracking branch 'upstream/master' into sasquatch-prebuild-maint through 10/20
  * Bump version string to 0.9.2.20221013
  * Updates to example OpenSim.ini and MoneyServer.ini, Bump Version.
  * Updates to exmple OpenSim.ini and MoneyServer.ini
  * Updates to re-enable economy addons for the DTL Money Module
  * Changes from Balpien + reformatting to handle the economy helper actions correctly in the DTL money module.
  * Comment out some additions from the Gloebits codebase that was trying to handle helper apps in process.  They were never correctly plumbed and so fail.
  * Bump version.  Rework oar logic to correctly handle lookups for aliases and to also correctly initialize prims based on GridUser and --allow-reassign being set.
  * Update codeql-analysis.yml (#45)
  * Add an --allow-reassign switch to oar loads which controls wether we reassign prims to the default region owner/specified default user if no local user is found for an id.
  * Fix for OfflineIM.V2, Explicitely check for the RESULT tag in returned data before accessing it.
  * Merge remote-tracking branch 'upstream/master' into develop
  * Bump VersionInfo
  * Don't access RESULT in the XML Response from StoreMessage if it doesn't exist in the payload.  It's not an error if its not present.  If it is we will return the value passed back.
  * Merge remote-tracking branch 'upstream/master' into sasquatch-prebuild-maint Through 7/20/2022
  * Updates through 7/15/2022 Merge remote-tracking branch 'upstream/master' into develop
  * Merge remote-tracking branch 'upstream/master' into sasquatch-prebuild-maint Changes through 7/15/2022
  * Merge tag '0.9.2.8227' into develop
  * Merge branch 'release/0.9.2.8227'
  * Remove ICSharpZipLib abd mautil.  Get them from NuGet instead.  Bump version number
  * Merge remote-tracking branch 'upstream/master' into develop
  * Merge branch 'feature/useralias' into develop
  * Add additional methods to Create and Delete Aliases.  Generate REL build (8225) from sources.
  * Add console commands to lookup aliases for a userid and resolve a userid given an alias.  Also create an alias entry.   Rework the calls to the alias service to fallback to the region owner if no alias is found.
  * Remove deprecated Cecil binary and get the compatible version from NuGet.
  * Merge develop into branch
  * Local copies of Mono.Cecil and Mono.Addins + Mono.Addins.Setup are gone in favor of the upsteam modules from NuGet.
  * Cleam up the id handling code.  Still need to track down the serialization code cause its not getting embedded stuff right by all appearances.
  * Merge remote-tracking branch 'origin/develop' into feature/useralias
  * Merge branch 'develop' of github.com:OpenSim-NGC/OpenSim-Sasquatch into develop
  * Upstream changes through 6/23
  * Merge remote-tracking branch 'upstream/master' into sasquatch-prebuild-maint
  * Remove the new Alias support from this csproj, the way the directories are nested it gets picked up automatically and should not.
  * Added logging for alias lookup on archive read.
  * Fixup sql create statement to only declare AliasID unique in one place.  Where the key is defined.   Dont pull the interface functions also into the test project.  Bump version for testing.
  * UserAlias support runs in the UserService so we need XMLRPC handlers to match whats in the region code
  * Connectors and Service definitions for UserAliasService.  This will be initially used in the OAR code to lookup user aliasesfor local accounts.
  * Add code to lookup aliased users for creator, last owner and owner for objects we make including task inventory. We only do this if the configuration indicates to do so.  Otherwise the default of using the estate owner is used on lookup of local users that are not found.
  * Stubbed out implementations for the UserAlias additions to UserService
  * Add a new table to record UserAliases.  Schema Migration.  Add methods on UserServer to return data from this table.
  * Comment out the test step for now
  * Add test run to CI.
  * Update the CI dotnet version to 6.0.*
  * Upgraded MySql.Data nuget (#41)
  * Switch to use the T4 templating accessible from the Build menu to generate VersionInfo.cs
  * Merge remote-tracking branch 'upstream/master' into sasquatch-prebuild-maint
  * Merge remote-tracking branch 'upstream/master' into develop
  * Comment out template generation for now until I can get a solution that works for both VS and dotnet builds
  * Generate VersionInfo.cs.  This may only work in VStudio. Need to look at a portable solution.
  * Merge remote-tracking branch 'upstream/master' into develop
  * Update Version Release info.
  * Merge remote-tracking branch 'upstream/master' into sasquatch-prebuild-maint
  * Fix up some csproj references for new files introduced.  USe MimeKit and MailKit from NuGet rather than the hard coded versions in Bin so we can keep up to date. This merges all changes in core through 4/29 to develop
  * Don't include unused prebuild artifacts.
  * Merge remote-tracking branch 'upstream/master' into develop
  * Sync up to upstream through 04/26/2022
  * Autogenerate VersionInfo.cs from a Text Template at build time
  * Merge remote-tracking branch 'upstream/master' into sasquatch-prebuild-maint
  * Merge tag '2022.02' into develop
  * Merge branch 'release/2022.02'
  * Merge remote-tracking branch 'upstream/master' into develop
  * Enable unsafe code in ubOdeMeshing.  More pointer crap where safer options exist.
  * Merge upstream through 2/23/2022
  * resolve conflicts with some library dependencies
  * Upstream catchup through 2/17/2022
  * Replaced Log4Net dll with version 2.0.13, previously 2.0.8, (#39)
  * Sync to OpenSim 0.9.2 release.
  * Merge remote-tracking branch 'upstream/master' into sasquatch-prebuild-maint
  * Merge remote-tracking branch 'upstream/master' into sasquatch-052021-maint
  * Bump version string
  * Merge remote-tracking branch 'upstream/master' into sasquatch-052021-maint
  * Dont source control osslEnable.ini
  * Upgrade Cecil.
  * Bump version info to show how current we are relative to upstream core
  * Remove AddinInfo.cs from CoreModules and use the xml metadata instead.  Remove direct dependency on Cecil, we'll use the one referenced by Mono.Addins, Update metadata for correct syntax.
  * Merge branch 'feature/upstream-addins' of github.com:OpenSim-NGC/OpenSim-Sasquatch into feature/upstream-addins
  * Add a manifest for CoreModules and include it as a resource.  Additional debug for pluginloader class.
  * Remove the old 32bit launch support.  It's no longer used
  * Try-convert on projects.  Cleaned up the xml proj definitions and normalized things so its fully compatible with current msbuild.
  * Upgrade Mono.Cecil to 0.11.4
  * Merge remote-tracking branch 'origin/develop' into feature/upstream-addins
  * Merge branch 'feature/upstream-2021.08' into develop
  * Remove the xml version of the plugins definitions for CoreModules and instead use the inline definitions in Addins.cs.  Some debugging in the RegionModules Loader.
  * Merge remote-tracking branch 'upstream/master' into feature/upstream-2021.08
  * Check initialization status for Plugins and init it if needed before loading Region Modules
  * Remove SharpZipLib from bin and use the version built for the NUGet packages.  Add package refs to OpenSim.Framework for Addins.
  * Merge remote-tracking branch 'upstream/master' into feature/upstream-2021.08
  * Changes to version strings to rationalize things.  The Assembly version follows the stricter Version field used for assembly loading while PackageVersion defines a Human readable version string.
  * Merge remote-tracking branch 'upstream/master' into feature/upstream-2021.08
  * Delete codacy workflow
  * Merge remote-tracking branch 'origin/develop' into feature/upstream-addins
  * Merge branch 'feature/upstream-2021.07' into develop
  * Update MYSQL Connector
  * Move from embedded Addins to NuGet version
  * Create codacy-analysis.yml
  * Create SECURITY.md
  * Merge remote-tracking branch 'upstream/master' into feature/upstream-2021.07
  * Merge remote-tracking branch 'upstream/master' into feature/upstream-2021.07 through 07/27/2021
  * Merge branch 'feature/execonfig' into develop
  * Removed origin bin versions of exe.config files and replaced them with project specific App.config templates that are merged with runtime support when the project is built.   Removed old MySql.Data package from bin as it comes from NuGet now.   Removed old crufty 32 bit support.  It likely wouldn't work and anything that can run net48 is 64bit capable anyway.  The original templates are still in share/ for the time being.
  * Use the new config we created
  * Add config options
  * Update CodeQL config
  * Create codeql-analysis.yml
  * Merge tag 'sasquatch-2021.06' into develop
  * Merge branch 'release/sasquatch-2021.06'
  * Bump version number
  * Merge branch 'feature/upstream-062021' into develop
  * Merge remote-tracking branch 'upstream/master' into feature/upstream-062021
  * Merge remote-tracking branch 'upstream/master' into feature/upstream-062021
  * Merge branch 'feature/nunit-update' into develop
  * Merge remote-tracking branch 'origin/develop' into feature/nunit-update
  * Disable tests for CI checks for now
  * Migrate Startup and Teardown Methods from Obsolete to supported tags.
  * Merge remote-tracking branch 'upstream/master' into feature/upstream-062021
  * Install PackageReference dependencies for nunit2 tests.  Remove the previous DLL in favor nuget managed libs.  Upgrade the nunit2 version as well.
  * Merge branch 'feature/limit-unsafe-code' into develop
  * Don't enable unsafe code unless it's absolutely needed.  6 projects use it and that list could be further pared down with some careful rewrites of some code. But this is a start.
  * One more try. 5.0.*
  * cant use net48 so try 5.0
  * New Workflow to do CI for develop
  * Merge tag 'money-buy-fix' into develop
  * Merge branch 'hotfix/money-buy-fix' Fix for duplicate buy transactions in the DTL money module.
  * Don't install the Buy handler on MakeRootAgent.  it's already installed in OnNewClient and the second install creates duplicated buy transactions.
  * Merge branch 'feature/upstream-052021' into develop
  * Merge upstream changes through 5/2021
  * Merge branch 'feature/mysql-driver-update' into develop
  * Removed the AssembleInfo.cs files (again), They are generated from the soltution metadata.  Migrated the Addin info to an AddinInfo.cs file so its still includes in the assembly.
  * Merge branch 'develop' into feature/mysql-driver-update
  * Merge tag 'sasquatch-052021' into develop
  * Merge branch 'release/sasquatch-052021'
  * EconomyModule is case sensative.  Make gloebit get it from the right name so we initialize money modules correctly.
  * Pickup changes from DiscoveryGrid for Money Module.
  * Package reference incorrect from moving some modules around.  Rerun prebuild to pick this up.
  * Bump version number.  Add config files that got missed on initial checkin.
  * Addins.xml for CoreModules
  * Low hanging fruit.  Remove all the AssemblyInfo.cs files for those projects that don't use Mono.Addins.   We'll let the build system generate AssemblyInfo.cs from project metadata.
  * Merge branch 'feature/mysql-driver-update' of github.com:mdickson/OpenSim-Sasquatch into feature/mysql-driver-update
  * Put the AssemblyInfo.cs files back for now. The automatic generation in msbuild doesn't coexist nicely with Mono.Addins.  We can revisit when/if we make Addins go away.
  * Clean up all the module definitions. Make sure the assembly name reflects the module correctly.
  * Generate csproj files for the new Money Modules.
  * use mysql connect/net from nuget where its specified.
  * Cleanup wizard converted project files and make sure all the references resolve.
  * First pass changes to move to msbuild format
  * Core 0.9.2 (#18)
  * Merge branch 'feature/money-server' into develop
  * Reformat for C# conventions and remove unused imports.
  * The Currency module is a region module so move it there. Everything is building now. Testing remains.
  * Local changes from Discovery Grid to make enabling of the region module conditional on config settings to allow multiple modules to co-exist.
  * Renamed References to Grid service to use .Service instead of .Grid. Renamed references to data module as well to match shorter name.
  * Import DTLNSL Money module, massaged for our directory structure.  Local changes still needed to allow this and Gloebits (or any other money service) to co-exist in a grid.
  * Merge branch 'feature/regions-disallow-tags' into develop
  * Put the AssemblyInfo.cs files back for now. The automatic generation in msbuild doesn't coexist nicely with Mono.Addins.  We can revisit when/if we make Addins go away.
  * Clean up all the module definitions. Make sure the assembly name reflects the module correctly.
  * Remove prebuild so its not accidentally run
  * Merge branch 'develop' into feature/mysql-driver-update
  * Merge branch 'develop' into feature/regions-disallow-tags
  * Core 0.9.2 updates through 04/24/2021 (#14)
  * use mysql connect/net from nuget where its specified.
  * Cleanup wizard converted project files and make sure all the references resolve.
  * First pass changes to move to msbuild format
  * Fix perms on some helper scripts for Linux
  * Remove dead code and reformat constructors.
  * Remove old dead code.  Changed to support configuring DisallowResidents (only admins) and DisallowForeigners (only local grid users, no HG) enntry to the region.  Defaults for both is false.
  * Changed to support configuring DisallowResidents (only admins) and DisallowForeigners (only local grid users, no HG) enntry to the region.  Defaults for both is false.    Also commented out a section of code in RegionInfo that is no longer in use/referenced from any other file.  We could just delete it if the consensus is to remove it.
  * Merge remote-tracking branch 'upstream/master' into develop
  * Merge tag 'sasquatch-rel-03092021' into develop
  * Merge branch 'release/sasquatch-rel-03092021'
  * Bump version number.  Changes from core through 3/8/2021
  * Merge remote-tracking branch 'upstream/master' into develop
  * Merge remote-tracking branch 'upstream/master' into develop
  * Merge pull request #12 from OpenSim-NGC/master
  * Merge pull request #11 from OpenSim-NGC/release/sasquatch-rel-01292021
  * Bump version string rel-210129
  * Merge branch 'feature/upstream-012021' into develop
  * Merge remote-tracking branch 'upstream/master' into feature/upstream-012021
  * Merge branch 'develop', remote-tracking branch 'origin' into feature/upstream-012021
  * Merge pull request #10 from OpenSim-NGC/feature/net48
  * Make dotnet 4.8 the default build target.  Requires VS 2019 or a recent Mono to build.
  * Merge remote-tracking branch 'upstream/master' into feature/upstream-012021
  * Merge remote-tracking branch 'upstream/master' into feature/upstream-012021
  * Add Gloebit.ini.example
  * Merge remote-tracking branch 'upstream/master' into feature/upstream-012021
  * Merge pull request #8 from OpenSim-NGC/feature/addon-mutelist
  * Merge pull request #7 from OpenSim-NGC/feature/addon-search
  * Adding PHP backend database interface
  * Add Mutelist support
  * Adding CS modules for OpenSim Search
  * Merge pull request #6 from OpenSim-NGC/feature/addon-gloebit
  * Add the gloebit addon-module to the build.  This is configurable on or off in ini files and respects the settings in the files.  Includes latest upstream patches
  * Merge pull request #5 from OpenSim-NGC/feature/admin-tools
  * Some useful scripts. Used to generate Debug or Release builds using msbuild and to pack a build for release skipping some development specific files.
  * Merge pull request #4 from OpenSim-NGC/feature/upstream-122020
  * Merge remote-tracking branch 'upstream/master' into feature/upstream-122020
  * Merge remote-tracking branch 'origin/core-0.9.2' into feature/upstream-122020
  * Merge remote-tracking branch 'upstream/master' into core-0.9.2
  * Merge pull request #3 from mdickson/feature/core-0.9.2-dev
  * Merge remote-tracking branch 'upstream/master' into feature/core-0.9.2-dev
  * Update to use full path to upstream fork
  * Merge pull request #2 from mdickson/develop
  * Update pull.yml
  * Merge branch 'master' of github.com:mdickson/OpenSim-Sasquatch
  * Merge remote-tracking branch 'origin/master' into develop
  * Github automation
