README

OpenSim comes with a default asset set contained in the OpenSimAssetSet
directory.  You can also load up your own asset set to OpenSim on startup by
making a file entry in AssetSets.xml.  This file should point towards an XML
file which details the assets in your asset set.  The 
OpenSimAssetSet/OpenSimAssetSet.xml is a good template for the information 
required.

If you want your assets to show up in the standard inventory library for an
avatar, you will also need to add separate entries to the xml files in the
bin/inventory configuration directory.
