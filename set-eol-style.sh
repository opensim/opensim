#!/bin/sh

set_eol_style()
{
    IFS=$'\n'
    for file in `find . -iname \*\.$1`; do
        eolstyle=`svn propget svn:eol-style $file`
        if [ -z "${eolstyle}" -o "${eolstyle}" != "native" ]; then
            svn propset svn:eol-style native $file
        fi
    done
}

EXTENSIONS="cs ini example txt sql xml"

for ext in ${EXTENSIONS}; do
    set_eol_style $ext
done
