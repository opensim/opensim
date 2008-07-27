#!/usr/bin/python
# -*- encoding: utf-8 -*-
import xmlrpclib

# XML-RPC URL (http_listener_port)
gridServerURL = 'http://127.0.0.1:9000'

# instantiate server object
gridServer = xmlrpclib.Server(gridServerURL)

# invoke admin_alert: requires password and message
print gridServer.get_grid_info({})
