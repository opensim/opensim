#!/bin/sh

# this script parses a template to replace @@ tokens

cat $1 | sed s/@@VERSION/$OPENSIMMAJOR.$OPENSIMMINOR/g | sed s/@@BUILD/`date +%s`/g | sed s/@@SVNREV/`svnversion`/g
