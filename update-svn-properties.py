#!/usr/bin/env python

import os, os.path, popen2, re, string, sys

def text(file):
    return {
        "svn:eol-style" : "native"
    }

def script(file):
    return {
        "svn:eol-style" : "native",
        "svn:executable" : "*"
    }

def executable(file):
    return {
        "svn:executable" : "*",
        "svn:mime-type" : "application/octet-stream"
    }

def binary(file):
    return {
        "svn:mime-type" : "application/octet-stream"
    }

def is_binary(file):
    f = open(file)
    data = f.read()
    f.close()

    for c in data:
        if c not in string.printable:
            return True
    return False

def binary_or_text(file):
    if is_binary(file):
        return binary(file)
    else:
        return text(file)

property_map = {
    ".bat" : script,
    ".build" : text,
    ".cfg" : text,
    ".cgi" : text,
    ".config" : text,
    ".cs" : text,
    ".csproj" : text,
    ".dat" : binary_or_text,
    ".dll" : binary,
    ".dylib" : binary,
    ".example" : text,
    ".exe" : executable,
    ".fxcop" : text,
    ".hgignore" : text,
    ".ico" : binary,
    ".include" : text,
    ".ini" : text,
    ".j2c" : binary,
    ".jp2" : binary,
    ".lsl" : text,
    ".mdp" : text,
    ".mds" : text,
    ".nsi" : text,
    ".pdb" : binary,
    ".php" : script,
    ".pidb" : binary,
    ".pl" : script,
    ".plist" : text,
    ".pm" : text,
    ".png" : binary,
    ".py" : script,
    ".rb" : script,
    ".resx" : text,
    ".settings" : text,
    ".stetic" : text,
    ".sh" : script,
    ".snk" : binary,
    ".so" : binary,
    ".sql" : text,
    ".txt" : text,
    ".user" : text,
    ".userprefs" : text,
    ".usertasks" : text,
    ".xml" : text,
    ".xsd" : text
}

def propset(file, property, value):
    os.system('svn propset %s "%s" "%s"' % (property, value, file))

def propdel(file, property):
    os.system('svn propdel %s "%s"' % (property, file))

def propget(file, property):
    output, input, error = popen2.popen3('svn propget %s "%s"' % (property, file))

    err = error.read()
    if err != "":
        output.close()
        error.close()
        input.close()
        return ""

    result = output.read()
    output.close()
    error.close()
    input.close()
    return result.strip()

def proplist(file):
    output, input, error = popen2.popen3('svn proplist "%s"' % file)

    err = error.read()
    if err != "":
        output.close()
        error.close()
        input.close()
        return None

    result = output.readlines()
    output.close()
    error.close()
    input.close()
    if len(result) > 0 and re.match("^Properties on .*:$", result[0]) is not None:
        return [r.strip() for r in result[1:]]
    else:
        return ""

def update_file(file, properties, ignorelist):
    current_props = proplist(file)

    if current_props is None:
        # svn error occurred -- probably an unversioned file
        return

    for p in current_props:
        if p not in ignorelist and not properties.has_key(p):
            propdel(file, p)
            
    for p in properties:
        if p not in current_props or propget(file, p) != properties[p]:
            propset(file, p, properties[p])

def update(dir, ignorelist):
    for f in os.listdir(dir):
        fullpath = os.path.join(dir, f)
        if os.path.isdir(fullpath):
            if not os.path.islink(fullpath):
                update(fullpath, ignorelist)
        else:
            extension = os.path.splitext(fullpath)[1].lower()
            if property_map.has_key(extension):
                update_file(fullpath, property_map[extension](fullpath), ignorelist)
            elif extension != "" and proplist(fullpath) is not None:
                print "Warning: No properties defined for %s files (%s)" % (extension, fullpath)

def main(argv = None):
    if argv is None:
        argv = sys.argv

    ignorelist = ("svn:keywords",)

    update(".", ignorelist)

if __name__ == "__main__":
    sys.exit(main())
