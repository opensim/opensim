#!/usr/bin/python
# -*- encoding: utf-8 -*-

import xml.etree.ElementTree as ET
import re
import urllib
import urllib2
import ConfigParser
import optparse
import os
import sys

reURI = re.compile(r'''^(?P<scheme>[a-zA-Z0-9]+)://                         # scheme
                        ((?P<avatar>[^:@]+)(:(?P<password>[^@]+))?@)?       # avatar name and password (optional)
                        (?P<host>[^:/]+)(:(?P<port>\d+))?                   # host, port (optional)
                        (?P<path>/.*)                                       # path
                       $''', re.IGNORECASE | re.VERBOSE)
reLOC = re.compile(r'''^/(?P<region>[^/]+)/          # region name
                         (?P<x>\d+)/                 # X position
                         (?P<y>\d+)/                 # Y position
                         (?P<z>\d+)                  # Z position
                    ''', re.IGNORECASE | re.VERBOSE)

if __name__ == '__main__':
    parser = optparse.OptionParser()
    parser.add_option('-c', '--config', dest = 'config', help = 'config file', metavar = 'CONFIG')
    parser.add_option('-s', '--secondlife', dest = 'client', help = 'location of secondlife client', metavar = 'SL-CLIENT')
    (options, args) = parser.parse_args()

    # 
    # we using ~/.matrixcfg to store the location of the secondlife client
    #
    if not options.config:
        options.config = '~/.matrixcfg'

    cfgPath = os.path.expanduser(options.config)

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

    client = config.get('secondlife', 'client')


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

    #
    # sanity check: matrix: or opensim: scheme?
    #
    if scheme != 'matrix' and scheme != 'opensim':
        print 'hmm...unknown scheme %s, calling it a day' % scheme

    #
    # get grid info from OpenSim server
    #
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

    except urllib2.URLError:
        print 'oops, failed to retrieve grid info, proceeding with guestimates...'

    #
    # fallback: use supplied uri in case GridInfo drew a blank
    #
    if not login: login = uri

    # 
    # ok, got everything, now construct the command line
    #
    clientArgs = ['matrix: %s' % gridnick]
    clientArgs += ['-loginuri', login]

    if welcome: clientArgs += ['-loginpage', welcome]
    if economy: clientArgs += ['-helperuri', economy]

    if avatar and password:
        clientArgs += ['-login']
        clientArgs += urllib.unquote(avatar).split()
        clientArgs += [password]

    # 
    # take a closer look at path: if it's a /region/X/Y/Z pattern, use
    # it as the "SLURL
    #
    match = reLOC.match(path)
    if match:
        loc = 'secondlife:///%s/%d/%d/%d' % (match.group('region'),
                                             int(match.group('x')), 
                                             int(match.group('y')),
                                             int(match.group('z')))
        clientArgs += [loc]

    # 
    # all systems go
    #
    os.execv(client, clientArgs)
