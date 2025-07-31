#! /bin/bash
# Script to add the number from the VERSION file to the .csproj of the sub-projects
# Run this just after running runprebuild.sh to add versions to the .csproj files.

cd $(dirname -- "$0")
BASE=$(pwd)

VERSION=$(cat VERSION)

for proj in Janus WebRtcVoice WebRtcVoiceRegionModule WebRtcVoiceServiceModule ; do
    cd "$BASE"
    cd "$proj"
    hasVersion=$(grep '<Version>' *.csproj)
    if [[ -z "$hasVersion" ]] ; then
        # There is no version in the file. Add it.
        sed -i "/TargetFramework/a <Version>$VERSION</Version>" *.csproj
    else
        # Version is already in the file. Update it.
        sed -i "s=<Version>.*</Version>=<Version>$VERSION</Version>=" *.csproj
    fi
done
