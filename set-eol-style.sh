#!/bin/sh

set_eol_style()
{
    for file in $*; do
        svn_status=`svn propget svn:eol-style $file`
        if [ -z "${svn_status}" -o "${svn_status}" != "native" ]; then
            svn propset svn:eol-style native $file
        fi
    done
}

for file in `find OpenSim -name \*\.cs`; do
    set_eol_style $file
done
