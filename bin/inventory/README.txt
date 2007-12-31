README

Folders and items which will appear in the standard common library for all
avatars can be configured here.  The root folder (currently called OpenSim
Library) is hardcoded, but you can add your own configuration of folders and
items directly beneath this, in addition to (or instead of) the contents of the 
default OpenSim library.

To add a new library, edit Libraries.xml.  The entry in here needs to point to
two further xml files, one which details your library inventory folders and another
which details your library inventory items.  Each inventory item will need to be
associated with an asset.  Assets are configured separately in the bin/assets
directory.

If you are running in grid mode, any library you add must be present in both 
your grid servers installation and in
every region installation, otherwise library items will fail in the regions
where the inventory configuration is not present.  The reasons for this are historical
and will probably be lifted in a future revision.

Files in the attic directory are currently unused.
