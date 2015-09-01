This folder is for modules that we intend to let users and system admins replace.

This folder should never end up a project.  Only subfolders should end up as a project.   The idea here is that each folder 
will produce a project and a separate .dll assembly for the module that will get picked up by the module loader.  
To replace the functionality, you simply replace the .dll with a different one.