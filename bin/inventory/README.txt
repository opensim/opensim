README

The standard common inventory library is configured here.  You can add new inventory 
folders to the standard library by editing OpenSimLibary/OpenSimLibraryFolders.xml
You can also add new inventory items to OpenSimLibrary/OpenSimLibrary.xml,
as long as they have a corresponding asset entry in bin/OpenSimAssetSet.xml.

The same set of folders and items must be present in the configuration of both
the grid servers and all the regions.  The reasons for this are historical - 
this restriction will probably be lifted in the future, at which point the
inventory items and folders will only need to be configured on the grid inventory
server (assuming you are running in grid mode rather than standalone)

Files in the attic directory are currently unused.
