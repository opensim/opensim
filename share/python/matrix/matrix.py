#!/usr/bin/python
# -*- encoding: utf-8 -*-
# Copyright (c) Contributors, http://opensimulator.org/
# See CONTRIBUTORS.TXT for a full list of copyright holders.
# 
# Redistribution and use in source and binary forms, with or without
# modification, are permitted provided that the following conditions are met:
#     * Redistributions of source code must retain the above copyright
#       notice, this list of conditions and the following disclaimer.
#     * Redistributions in binary form must reproduce the above copyright
#       notice, this list of conditions and the following disclaimer in the
#       documentation and/or other materials provided with the distribution.
#     * Neither the name of the OpenSim Project nor the
#       names of its contributors may be used to endorse or promote products
#       derived from this software without specific prior written permission.
# 
# THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY EXPRESS OR
# IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
# DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
# DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
# DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE
# GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
# INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER
# IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
# OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
# ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

import xml.etree.ElementTree as ET
import re
import urllib
import urllib2
import ConfigParser
import optparse
import os
import sys

def longHelp():
    print '''
matrix.py is a little launcher tool that knows about the GridInfo
protocol. it expects the grid coordinates to be passed as a
command line argument either as a "matrix:" or as an "opensim:" style
URI:

	matrix://osgrid.org:8002/

you can also provide region/X/Y/Z coordinates:

        matrix://osgrid.org:8002/Wright%20Plaza/128/50/75

and, it also understands avatar names and passwords:

        matrix://mr%20smart:secretpassword@osgrid.org:8002/Wright%20Plaza/128/50/75

when you run it the first time, it will complain about a missing
.matrixcfg file --- this is needed so that it can remember where your
secondlife client lives on your box. to generate that file, simply run 

        matrix.py --secondlife path-to-your-secondlife-client-executable

          '''

def ParseOptions():
    '''Parse the command line options and setup options.
       '''

    parser = optparse.OptionParser()
    parser.add_option('-c', '--config', dest = 'config', help = 'config file', metavar = 'CONFIG')
    parser.add_option('-s', '--secondlife', dest = 'client', help = 'location of secondlife client', metavar = 'SL-CLIENT')
    parser.add_option('-l', '--longhelp', action='store_true', dest = 'longhelp', help = 'longhelp')
    (options, args) = parser.parse_args()

    if options.longhelp:
        parser.print_help()
        longHelp()
        sys.exit(0)

    return options

def ParseConfig(options):
    '''Ensure configuration exists and parse it.
       '''
    # 
    # we are using ~/.matrixcfg to store the location of the
    # secondlife client, os.path.normpath and os.path.expanduser
    # should make sure that we are fine cross-platform
    #
    if not options.config:
        options.config = '~/.matrixcfg'

    cfgPath = os.path.normpath(os.path.expanduser(options.config))

    # 
    # if ~/.matrixcfg does not exist we are in trouble...
    #
    if not os.path.exists(cfgPath) and not options.client:
        print '''oops, i've no clue where your secondlife client lives around here.
                 i suggest you run me with the "--secondlife path-of-secondlife-client" argument.'''
        sys.exit(1)

    #
    # ok, either ~/.matrixcfg does exist or we are asked to create it
    #
    config = ConfigParser.ConfigParser()
    if  os.path.exists(cfgPath):
        config.readfp(open(cfgPath))

    if options.client:
        if config.has_option('secondlife', 'client'):
            config.remove_option('secondlife', 'client')
        if not config.has_section('secondlife'):
            config.add_section('secondlife')
        config.set('secondlife', 'client', options.client)
        
        cfg = open(cfgPath, mode = 'w+')
        config.write(cfg)
        cfg.close()

    return config.get('secondlife', 'client')


#
# regex: parse a URI
#
reURI = re.compile(r'''^(?P<scheme>[a-zA-Z0-9]+)://                         # scheme
                        ((?P<avatar>[^:@]+)(:(?P<password>[^@]+))?@)?       # avatar name and password (optional)
                        (?P<host>[^:/]+)(:(?P<port>\d+))?                   # host, port (optional)
                        (?P<path>/.*)                                       # path
                       $''', re.IGNORECASE | re.VERBOSE)

# 
# regex: parse path as location 
#
reLOC = re.compile(r'''^/(?P<region>[^/]+)/          # region name
                         (?P<x>\d+)/                 # X position
                         (?P<y>\d+)/                 # Y position
                         (?P<z>\d+)                  # Z position
                    ''', re.IGNORECASE | re.VERBOSE)

def ParseUri(uri):
    '''Parse a URI and return its constituent parts.
       '''

    match = reURI.match(uri)
    if not match or not match.group('scheme') or not match.group('host'):
        print 'hmm... cannot parse URI %s, giving up' % uri
        sys.exit(1)

    scheme = match.group('scheme')
    host = match.group('host')
    port = match.group('port')
    avatar = match.group('avatar')
    password = match.group('password')
    path = match.group('path')

    return (scheme, host, port, avatar, password, path)

def ParsePath(path):
    '''Try and parse path as /region/X/Y/Z.
       '''

    loc = None
    match = reLOC.match(path)
    if match:
        loc = 'secondlife:///%s/%d/%d/%d' % (match.group('region'),
                                             int(match.group('x')), 
                                             int(match.group('y')),
                                             int(match.group('z')))
    return loc


def GetGridInfo(host, port):
    '''Invoke /get_grid_info on target grid and obtain additional parameters
       '''

    gridname = None
    gridnick = None
    login = None
    welcome = None
    economy = None

    #
    # construct GridInfo URL
    #
    if port:
        gridInfoURI = 'http://%s:%d/get_grid_info' % (host, int(port))
    else:
        gridInfoURI = 'http://%s/get_grid_info' % (host)

    #
    # try to retrieve GridInfo 
    #
    try:
        gridInfoXml = ET.parse(urllib2.urlopen(gridInfoURI))
        
        gridname = gridInfoXml.findtext('/gridname')
        gridnick = gridInfoXml.findtext('/gridnick')
        login = gridInfoXml.findtext('/login')
        welcome = gridInfoXml.findtext('/welcome')
        economy = gridInfoXml.findtext('/economy')
        authenticator = gridInfoXml.findtext('/authenticator')

    except urllib2.URLError:
        print 'oops, failed to retrieve grid info, proceeding with guestimates...'

    return (gridname, gridnick, login, welcome, economy)


def StartClient(client, nick, login, welcome, economy, avatar, password, location):
    clientArgs = [ client ]
    clientArgs += ['-loginuri', login]

    if welcome: clientArgs += ['-loginpage', welcome]
    if economy: clientArgs += ['-helperuri', economy]

    if avatar and password:
        clientArgs += ['-login']
        clientArgs += urllib.unquote(avatar).split()
        clientArgs += [password]

    if location:
        clientArgs += [location]

    # 
    # all systems go
    #
    os.execv(client, clientArgs)


if __name__ == '__main__':
    #
    # parse command line options and deal with help requests
    #
    options = ParseOptions()
    client = ParseConfig(options)

    #
    # sanity check: URI supplied?
    #
    if not sys.argv:
        print 'missing opensim/matrix URI'
        sys.exit(1)

    # 
    # parse URI and extract scheme. host, port?, avatar?, password?
    #
    uri = sys.argv.pop()
    (scheme, host, port, avatar, password, path) = ParseUri(uri)

    #
    # sanity check: matrix: or opensim: scheme?
    #
    if scheme != 'matrix' and scheme != 'opensim':
        print 'hmm...unknown scheme %s, calling it a day' % scheme

    #
    # get grid info from OpenSim server
    #
    (gridname, gridnick, login, welcome, economy) = GetGridInfo(host, port)

    #
    # fallback: use supplied uri in case GridInfo drew a blank
    #
    if not login: login = uri

    # 
    # take a closer look at path: if it's a /region/X/Y/Z pattern, use
    # it as the "SLURL
    #
    location = ParsePath(path)
    StartClient(client, gridnick, login, welcome, economy, avatar, password, location)

