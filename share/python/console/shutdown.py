#!/usr/bin/python
# -*- encoding: utf-8 -*-

import ConfigParser
import xmlrpclib
import optparse
import os.path

if __name__ == '__main__':
    parser = optparse.OptionParser()
    parser.add_option('-c', '--config', dest = 'config', help = 'config file', metavar = 'CONFIG')
    parser.add_option('-s', '--server', dest = 'server', help = 'URI for the grid server', metavar = 'SERVER')
    parser.add_option('-p', '--password', dest = 'password', help = 'password for the grid server', metavar = 'PASSWD')
    (options, args) = parser.parse_args()

    configFile = options.config
    if not configFile:
        if os.path.isfile(os.path.expanduser('~/.opensim-console.rc')):
            configFile = os.path.expanduser('~/.opensim-console.rc')
    if not configFile:
        parser.error('missing option config')
        sys.exit(1)

    config = ConfigParser.ConfigParser()
    config.readfp(open(configFile))

    server = config.get('opensim', 'server')
    password = config.get('opensim', 'password')
    
    if options.server: server = options.server
    if options.password: password = options.password

    gridServer = xmlrpclib.Server(server)
    res = gridServer.admin_shutdown({'password': password})

    if res['success'] == 'true':
        print 'shutdown of %s initiated' % server
    else:
        print 'shutdown of %s failed' % server
