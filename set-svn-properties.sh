#!/bin/sh

set_eol_style()
{
    IFS=$'\n'
    for file in `find . -iname \*\.$1`; do
        prop=`svn propget svn:eol-style $file`
        if [ -z "${prop}" -o "${prop}" != "native" ]; then
            svn propset svn:eol-style native $file
        fi
    done
    IFS=$' \t\n'
}

remove_executable()
{
    IFS=$'\n'
    for file in `find . -iname \*\.$1`; do
        prop=`svn propget svn:executable $file`
        if [ -n "${prop}" ]; then
            svn propdel svn:executable $file
        fi
    done
    IFS=$' \t\n'
}

set_executable()
{
    IFS=$'\n'
    for file in `find . -iname \*\.$1`; do
        prop=`svn propget svn:executable $file`
        if [ -z "${prop}" ]; then
            svn propset svn:executable "*" $file
        fi
    done
    IFS=$' \t\n'
}

EOL_EXTENSIONS="cs ini example txt sql xml sh"
NO_EXE_EXTENSIONS="cs ini example txt sql xml"
EXE_EXTENSIONS="exe sh"

for ext in ${EOL_EXTENSIONS}; do
    set_eol_style $ext
done

for ext in ${NO_EXE_EXTENSIONS}; do
    remove_executable $ext
done

for ext in ${EXE_EXTENSIONS}; do
    set_executable $ext
done
