# Build a tarball for the build, excluding various development directories.  This command should be run in the top 
# level of the source tree and passed a name to be used for the release. This name is used to generate the tar file
# and is currently placed in the directory above where the command is run.

releasename=$1

tar --exclude='./.git' --exclude='./.nant' --exclude='./.vs' --exclude='./.vscode' --exclude='bin/ScriptEngines'  -czvf ../${releasename}.tar.gz .
