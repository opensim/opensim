# OpenSim Gloebit Money Module
This is a plugin (addon) to enable the Gloebit currency service on an OpenSim grid.  It also serves as an example which can be referenced or ported to integrate the Gloebit service with other platforms.

<kbd>![Gloebit Money Module for OpenSim](http://dev.gloebit.com/blog/images/OpenSim-Gloebit-Money-Module-Addon-Beta.png)</kbd>


# How to use this with OpenSim
1. Download or Build the DLL
  * Download\
    If you don't want to build yourself, you can download the most recent release of the plugin [here](http://dev.gloebit.com/opensim/downloads/).  Download the DLL built for or as close to your version of OpenSim as possible.  If you run into any linking errors, then either the version of OpenSim or your build environment are incompatible with the prebuilt DLLs and you'll need to build it directly against your repository.
  * or Build\
    For the latest features and to ensure compatibility with your system, we recommend building the DLL yourself.
    1. Clone or Download this repository
    2. Copy the Gloebit directory into the addon-modules directory in your OpenSim repository
    3. Install mono and mono-devel version 5.12 or higher
    4. Run the OpenSim runprebuild script eg:`. runprebuild.sh`
    5. Build OpenSim eb:`msbuild` or `nant`
    6. Check build result should not return error or excessive amounts of warnings(3-5 normally)
2. Configure the plugin
  * Follow the instructions [here](http://dev.gloebit.com/opensim/configuration-instructions/).

# Understanding, Contributing to, and Porting this Plugin

## Code Organization - The breakdown of the functional layers

<kbd>![GMM Architecture Slide](http://dev.gloebit.com/images/GMM-Architecture.png)</kbd>
<kbd>![GMM Architecture Files Slide](http://dev.gloebit.com/images/GMM-Architecture-Files.png)</kbd>

Starting with the foundation...

### The REST / Web API Layer
* GloebitAPI.cs

This layer provides a C# interface for connecting with Gloebit's web service apis through defined endpoints.

This layer is platform agnostic and shouldn't need modification for porting to another C# platform.  However, it does use an older asyncronous C# pattern compatible with earlier versions of mono.  If another C# platform did not have those compatibility issues, an alternate version could be written to take advantage of the newer Async and Await calls, though it is unknown if there are any performance gains that would be worth this effort.  It also serves as example code for anyone who needs to build a web api for connecting to gloebit in a language other than C#.

Modifications to this layer should really be intended to be universal improvements.  Some such improvements on our list are to...
* convert OSDMaps to Dictionaries and UUIDs to GUIDs so we can remove the requirement for OpenMetaverse libraries to make this more generic.
* implement new endpoints as Gloebit makes them available
* separate GloebitAPI.cs into a true web API (using forms rather than object classes) wrapped in an object API which provides an object interface and converts to and from the form interface of the web api.
* implement a better error reporting system making use of the exception classes used to create the errors by Gloebit.

### The Object Layer
* GloebitUser.cs
* GloebitTransaction.cs
* GloebitSubscription.cs

This layer provides C# classes for more easily managing the data passed to and from the Gloebit API and used throughout the module.

This layer is platform agnostic and shouldn't need modification for porting to another C# platform.

Modifications to this layer would be necessary if there was additional data you wanted to package with the objects or store in database tables.  Some such improvements on our list are to...
* convert UUIDs to GUIDs or strings to remove the requirement for the OpenMetaverse library.
* remove the parameters specific to a BuyObject transaction in OpenSim from GloebitTransaction and move them either to an object sale asset class with permanent storage or to an object sale asset map in local memory.  Will need to keep an asset type and asset id field for asset retrieval and possibly for enacting simple assets which don't need to store more data requiring an asset class or map.
* add asset classes or maps for other transaction types as necessary which are currently making use of the BuyObject fields in GloebitTransaction and cannot be handled generically.
* add the description string to GloebitTransaction
* add data to GloebitTransaction as requested by some customers such as the region ID and region name, or if particular to OpenSim, perhaps store them in a separate object/table.
* add user name to the GloebitUser object so that it doesn't have to be passed separately into functions that might call authorize, such as GetAgentBalance.  Would likely require some new api endpoints to create and update an AppUser and a flow change to the GMM so that AppUsers are created (with extra info such as name and image, etc) prior to attempting to call functions on a user.

### The Database Interface Layer
* GloebitUserData.cs
* GloebitTransactionData.cs
* GloebitSubscriptionData.cs
* Resources/GloebitUsers*.migrations
* Resources/GloebitTransactions*.migrations
* Resources/GloebitSubscriptions*.migrations

This layer handles the storage and retrieval of objects to and from a database.

This layer likely needs modification for porting to another platform.  The Data classes are built upon GenericTableHandlers provided by OpenSim and the migration format may be specific to OpenSim.  These classes should be modified to work with the database interface of the intended platform so that the storage and retrieval calls from the object classes work as expected.

Integrating a database other than MySql, PGSQL or SQLite would be done here by adding the implementation to each data class and adding a migration for each class to the resources.

### The Functional Helper Layer
* GloebitAPIWrapper.cs

This layer wraps the API in a more useful functional layer, converts platform specific information into formats expected by the API and vice versa, handles callbacks from the api, http callbacks from the Gloebit service, and errors, and manages as much processing logic as should be generic to all platforms.  This layer should drastically simplify integration, and will likely need to evolve as new platforms integrate the plugin.  It defines some interfaces which the platform glue layer must implement.  Some of these may at some point be converted to events that can be registered for.

This layer is where some editing will be necessary when porting to another platform.  The signatures of the http callbacks may be specific to the platform and therefor may need adjustment.  In simplifying the returns from the API layer, it is possible we've elimiated information a platform may need, in which case new interface functions may be necessary.  We recommend keeping the platform logic out of this layer in these cases and requesting that we adopt new interface functions or argument passing into the root plugin.

### The Platform Glue Layer
* GloebitMoneyModule.cs

This layer contains all the connections to the larger platform.  It reads configuration, creates the API, registers the http callbacks, defines the platform specific transactions, implements all interfaces necessary for the APIWrapper, and controls the full API flow.  This contains the platform specific logic.  Currently, this file is both the glue between OpenSim and Gloebit and also a lot of strictly OpenSim logic which could be elsewhere.  Ideally we'll better separate this eventually by splitting this class and file up.

This layer is where most of the editing will be necessary when porting to another platform.  As much as possible, the GloebitAPIWrapper interface method signatures should be maintained, but the bodies will likely have to be modified.


## Adding a new commerce flow in OpenSim

1. Add a TransactionType enum.
  * Define a new TransationType for the flow which should be supplied by triggering event.
  * Add this TransactionType to the switch statement of a receiving event for processing this flow.
2. Handle TransactionPrecheckFailures
  * Define any newly necessary TransactionPrecheckFailure enums not already created.
  * Call alertUsersTransactionPreparationFailure() as necessary from the interface function handling processing.
  * Edit alertUsersTransactionPrepartionFailure() as neccessary to provide specific messaging for this new TransactionType.
3. Compile info to be supplied in user's transaction history on gloebit.com 
  * Call buildOpenSimTransactionDescMap
    * If more info is needed, either create a new override for buildBaseTransactionDescMap, or call GloebitAPIWrapper.addDescMapEntry to add elements one at a time to the base map.
  * Create a description string to be displayed as the primary transaction description
4. Build the Transaction
  * Supply the proper information to buildTransaction().
  * If you will need additional information when processing this transaction which you can not store in the default transaction parameters, then you will need to create a new dictionary to map the transaction UUID to the asset information you'll require.
  * If this is a subscription (auto-debit) transaction, you'll also need to create or retrieve the subscription authorization.
5. Submit the Transaction to Gloebit
  * Call GloebitAPIWrapper SubmitTransaction() or SubmitSyncTransaction() and supply the transaction, description and descMap for this transaction type.
6. Implement delivery of asset related to payment (see region GMM IAssetCallback Interface)
  * Add this TransactionType to processAssetEnactHold(), processAssetConsumeHold(), processAssetCancelHold() to handle the particulars of asset delivery for this TransactionType
    * These will be triggered by Gloebit during processing of this transaction
7. Implement any platform specific functional requirements of transacton process stages not handled in asset enact/consume/cancel via the GloebitAPIWrapper ITransactionAlert interface AlertTransaction functions
8. Provide messaging to user throughout transaction (see region GMM User Messaging)
  * Add this TransactionType to alertUsersTransactionBegun()
  * As necessary, add this TransactionType to alertUsersTransactionStageCompleted(), alertUsersTransactionFailed() and alertUsersTransactionSucceeded() to supply transaction specific messaging.


## Integrating into a new platform

### Required Libraries
* Until we convert the OSDMaps used throughout the API to Dictionaries, OpenMetaverse.StructuredData will be required.
* The Database interface layer will need modification or various OpenSim data libraries will be required.

### Configuration
There must be a system for an application to declare the configuration for connecting to the Gloebit service.  At a bare minimum, the app needs to define the OAuth key and secret which will identify it to Gloebit and whatever database connection parameters are necessary for the platforms database interface layer.

### Initialization
* A GloebitAPIWrapper must be created.  This creates a GloebitAPI as well.
  * If this has been modified to not initialize the db connections for GloebitUserData, GloebitTransactionData and GloebitSubscriptionData those must be initialized as well.
* http (or ideally https) must be registered to a base URI with the following paths on that base URI launching the following functions.  These functions will need to be modified if your http handler signature is different.
  * /gloebit/auth_complete -> m_apiW.authComplete_func
  * /gloebit/transaction -> m_apiW.transactionState_func
  * /gloebit/buy_complete -> m_apiW.buyComplete_func

### Required Interface Implementation
The following interfaces must be defined and passed to the constructor for the GloebitAPIWrapper

* Interfaces which impact the Gloebit API
  * GloebitAPIWrapper.IUriLoader Interface (note: this doesn't impact the API, but is vital to operation)
  * GloebitAPIWrapper.IPlatformAccessor Interface
  * GloebitTransaction.IAssetCallback Interface
    * Interface for handling state progression of assets (any local app-specific part of a transaction).
    * ENACT -> Funds have been transfered.  Process local asset (generally deliver a product).
    * CONSUME -> Funds have been released to recipient.  Finalize anything necessary.
    * CANCEL -> Transaction has been canceled.  Undo anything necessary.
* Interfaces which don't impact the Gloebit API and could be turned into events in the future
  * GloebitAPIWrapper.IUserAlert Interface
  * GloebitAPIWrapper.ITransactionAlert Interface
    * This is primarily present to enable the triggering of user messaging throughout a transaction as desired by the user or platform.
  * GloebitAPIWrapper.ISubscriptionAlert Interface

### Using the system

* App
  * Every thing in the API happens within an app.  We haven't yet created a GloebitApp class, but this is effectively the configuration passed to the GloebitAPIWrapper during initialization.
* GloebitUser (aka AppUser)
  * An AppUser is the representation of a single local account (often referred to as an agent) for a single App configuration.
  * An AppUser must authorize the app from a Gloebit account.  This allows many of the GloebitAPI methods to be called by the app for this AppUser and it also allows the user to privately/confidentially link this AppUser to his/her Gloebit account.
    * GloebitUser.Get() should be called to create or retrieve an AppUser
      * There is not one central GloebitAPIWrapper function for starting a new user session but it may later be centralized.  See GMM.SendNewSessionMessaging() func for closest thing to a StartUserSession().
      * GloebitUser.Cleanup() should be called to free up memory when a User is no longer active
  * The platform must determine when to create and cleanup GloebitUsers and when to request authorization (eg. at signup, at first transaction)
    * calling GetUserBalance() func with forceAuthoOnInvalidToken=true is simplest way to ensure user is authorized and ready to proceed with transactions.
    * Authorize can also be called directly on a GloebitUser or an local agent ID if the GloebitUser hasn't been retrieved yet.
    * The platform can determine how to present authorization to the user via the LoadAuthorizationUri interface function

* GloebitTransaction
  * A transaction requires an App and an authorized AppUser who is the payer in the transaction.
  * A standard transaction should be triggered by the payer and the platform should do any necessary local validation (for instance, is there inventory left?  Is this actually for sale? Is this the correct price? etc) prior to generating a GloebitTransaction.
  * If the platform has multiple commerce flows which require different actions during processing, then the platform should define a transaction type for each which is stored in the transaction and can be retrieved during processing.
  * Some fields are available in a default GloebitTransaction for storing information necessary during processing, but if addtional information is necessary, the platform should create a local dictionary or db table for that information and store the key as one of the extra UUIDs in the GloebitTransacion (most likely the PartID.  Possible key could be the transactionID depending upon order of creation)
  * Additional transaction information which should be stored in a user's transaction history but is not part of the default transaction should be compiled via the BuildBaseTransactionDescMap and AddDescMapEntry methods.
    * As much information as possible should be provided about the platform (or app), the location, and the transaction itself.
  * Call GloebitTransaction.Create() to generate a local GloebitTransaction
  * Send the transaction to Gloebit for processing (along with a description and the transaction description map) via SubmitTransaction() or SubmitSyncTransaction()
  * Implement delivery of assets (any local transaction part processing) via the IAssetCallback interface.
  * Provide messaging to users and handle any other necessary platform functionality through the ITransactionAlert interface

* GloebitSubscription
  * Subscriptions are required when a transaction is not actively triggered by the payer at the time of the transaction request.
  * The platform must register the subscription with Gloebit.
  * The platform must ask the user to authorize that subscription.
  * Any transaction related to that subscription must be marked as a subscription and provide the subscription and subscription authorization ids.
  * Similar to an app authorization, a user can revoke a subscription authorization from the Gloebit website, so a platform integration must be prepared to handle transaction failures on a subscription where it believed it had an authorization.
  
