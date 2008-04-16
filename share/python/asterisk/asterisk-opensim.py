#!/usr/bin/python
# -*- encoding: utf-8 -*-

from __future__ import with_statement

import base64
import ConfigParser
import optparse
import MySQLdb
import re
import SimpleXMLRPCServer
import socket
import sys
import uuid

class AsteriskOpenSimServerException(Exception):
    pass

class AsteriskOpenSimServer(SimpleXMLRPCServer.SimpleXMLRPCServer):
    '''Subclassed SimpleXMLRPCServer to be able to muck around with the socket options.
       '''

    def __init__(self, config):
        baseURL = config.get('xmlrpc', 'baseurl')
        match = reURLParser.match(baseURL)
        if not match:
            raise AsteriskOpenSimServerException('baseURL "%s" is not a well-formed URL' % (baseURL))

        host = 'localhost'
        port = 80
        path = None
        if match.group('host'):
            host = match.group('host')
            port = int(match.group('port'))
        else:
            host = match.group('hostonly')
            
        self.__host = host
        self.__port = port

        SimpleXMLRPCServer.SimpleXMLRPCServer.__init__(self,(host, port))
    
    def host(self):
        return self.__host

    def port(self):
        return self.__port
        
    def server_bind(self):
        self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        SimpleXMLRPCServer.SimpleXMLRPCServer.server_bind(self)


class AsteriskFrontend(object):
    '''AsteriskFrontend serves as an XmlRpc function dispatcher.
       '''

    def __init__(self, config, db):
        '''Constructor to take note of the AsteriskDB object.
           '''
        self.__db = db
        try:
            self.__debug = config.getboolean('dispatcher', 'debug')
        except (ConfigParser.NoOptionError, ConfigParser.NoSectionError):
            self.__debug = False

    def account_update(self, request):
        '''update (or create) the SIP account data in the Asterisk RealTime DB.

           OpenSim's AsteriskVoiceModule will call this method each
           time it receives a ProvisionVoiceAccount request.
           '''
        print '[asterisk-opensim] account_update: new request'

        for p in ['admin_password', 'username', 'password']:
            if p not in request: 
                print '[asterisk-opensim] account_update: failed: missing password'
                return { 'success': 'false', 'error': 'missing parameter "%s"' % (p)}

        # turn base64 binary UUID into proper UUID
        user = request['username'].partition('@')[0]
        user = user.lstrip('x').replace('-','+').replace('_','/')
        user = uuid.UUID(bytes = base64.standard_b64decode(user))

        if self.__debug: print '[asterisk-opensim]: account_update: user %s' % user

        error = self.__db.AccountUpdate(user = user, password = request['password'])
        if error: 
            print '[asterisk-opensim]: DB.AccountUpdate failed'
            return { 'success': 'false', 'error': error}

        print '[asterisk-opensim] account_update: done'
        return { 'success': 'true'}


    def region_update(self, request):
        '''update (or create) a VoIP conference call for a region.

           OpenSim's AsteriskVoiceModule will call this method each time it
           receives a ParcelVoiceInfo request.
           '''
        print '[asterisk-opensim] region_update: new request'

        for p in ['admin_password', 'region']:
            if p not in request: 
                print '[asterisk-opensim] region_update: failed: missing password'
                return { 'success': 'false', 'error': 'missing parameter "%s"' % (p)}

        region = request['region'].partition('@')[0]
        if self.__debug: print '[asterisk-opensim]: region_update: region %s' % user

        error = self.__db.RegionUpdate(region = region)
        if error: 
            print '[asterisk-opensim]: DB.RegionUpdate failed'
            return { 'success': 'false', 'error': error}

        print '[asterisk-opensim] region_update: done'
        return { 'success': 'true' }

class AsteriskDBException(Exception):
    pass

class AsteriskDB(object):
    '''AsteriskDB maintains the connection to Asterisk's MySQL database.
       '''
    def __init__(self, config):
        # configure from config object
        self.__server   = config.get('mysql', 'server')
        self.__database = config.get('mysql', 'database')
        self.__user     = config.get('mysql', 'user')
        self.__password = config.get('mysql', 'password')

        try:
            self.__debug   = config.getboolean('mysql', 'debug')
            self.__debug_t = config.getboolean('mysql-templates', 'debug')
        except ConfigParser.NoOptionError:
            self.__debug = False

        self.__tablesTemplate = self.__loadTemplate(config, 'tables')
        self.__userTemplate   = self.__loadTemplate(config, 'user')
        self.__regionTemplate = self.__loadTemplate(config, 'region')

        self.__mysql = MySQLdb.connect(host = self.__server, db = self.__database,
                                       user = self.__user, passwd = self.__password)
        if self.__assertDBExists():
            raise AsteriskDBException('could not initialize DB')

    def __loadTemplate(self, config, templateName):
        template = config.get('mysql-templates', templateName)
        t = ''
        with open(template, 'r') as templateFile:
            for line in templateFile:
                line = line.rstrip('\n')
                t += line
        return t.split(';')
        

    def __assertDBExists(self):
        '''Assert that DB tables exist.
           '''
        try:
            cursor = self.__mysql.cursor()
            for sql in self.__tablesTemplate[:]:
                if not sql: continue
                sql = sql % { 'database': self.__database }
                if self.__debug: print 'AsteriskDB.__assertDBExists: %s' % sql
                cursor.execute(sql)
            cursor.fetchall()
            cursor.close()
        except MySQLdb.Error, e:
            if self.__debug: print 'AsteriskDB.__assertDBExists: Error %d: %s' % (e.args[0], e.args[1])
            return e.args[1]
        return None

    def AccountUpdate(self, user, password):
        print 'AsteriskDB.AccountUpdate: user %s' % (user)
        try: 
            cursor = self.__mysql.cursor()
            for sql in self.__userTemplate[:]:
                if not sql: continue
                sql = sql % { 'database': self.__database, 'username': user, 'password': password }
                if self.__debug_t: print 'AsteriskDB.AccountUpdate: sql: %s' % sql
                cursor.execute(sql)
            cursor.fetchall()
            cursor.close()
        except MySQLdb.Error, e:
            if self.__debug: print 'AsteriskDB.RegionUpdate: Error %d: %s' % (e.args[0], e.args[1])
            return e.args[1]
        return None

    def RegionUpdate(self, region):
        print 'AsteriskDB.RegionUpdate: region %s' % (region)
        try: 
            cursor = self.__mysql.cursor()
            for sql in self.__regionTemplate[:]:
                if not sql: continue
                sql = sql % { 'database': self.__database, 'regionname': region }
                if self.__debug_t: print 'AsteriskDB.RegionUpdate: sql: %s' % sql
                cursor.execute(sql)
                res = cursor.fetchall()
        except MySQLdb.Error, e:
            if self.__debug: print 'AsteriskDB.RegionUpdate: Error %d: %s' % (e.args[0], e.args[1])
            return e.args[1]
        return None


reURLParser = re.compile(r'^http://((?P<host>[^/]+):(?P<port>\d+)|(?P<hostonly>[^/]+))/', re.IGNORECASE)

# main
if __name__ == '__main__':

    parser = optparse.OptionParser()
    parser.add_option('-c', '--config', dest = 'config', help = 'config file', metavar = 'CONFIG')
    (options, args) = parser.parse_args()

    if not options.config:
        parser.error('missing option config')
        sys.exit(1)
        
    config = ConfigParser.ConfigParser()
    config.readfp(open(options.config))
    
    server = AsteriskOpenSimServer(config)
    server.register_introspection_functions()
    server.register_instance(AsteriskFrontend(config, AsteriskDB(config)))

    # get cracking
    print '[asterisk-opensim] server ready on %s:%d' % (server.host(), server.port())
    server.serve_forever()

    sys.exit(0)
