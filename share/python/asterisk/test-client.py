#!/usr/bin/python
# -*- encoding: utf-8 -*-

import xmlrpclib

# XML-RPC URL (http_listener_port)
asteriskServerURL = 'http://127.0.0.1:53263'

# instantiate server object
asteriskServer = xmlrpclib.Server(asteriskServerURL)

try:
    # invoke admin_alert: requires password and message
    res = asteriskServer.region_update({
            'admin_password': 'c00lstuff',
            'region'        : '941ae087-a7da-43b4-900b-9fe48387ae57@secondlife.zurich.ibm.com'
            })
    print res
            
    res = asteriskServer.account_update({
        'admin_password': 'c00lstuff',
        'username'      : '0780d90b-1939-4152-a283-8d1261fb1b68@secondlife.zurich.ibm.com',
        'password'      : '$1$dd02c7c2232759874e1c205587017bed'
    })
    print res
except Exception, e:
    print e

