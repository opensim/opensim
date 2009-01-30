#!/usr/bin/env python
# -*- encoding: utf-8 -*-
# 
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
# THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
# EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
# DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
# DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
# (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
# LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
# ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
# (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
# SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
# 

import logging
import BaseHTTPServer
import optparse
import xml.etree.ElementTree as ET
import xml.parsers.expat


# enable debug level logging
logging.basicConfig(level = logging.DEBUG,
                    format='%(asctime)s %(levelname)s %(message)s')

options = None

# subclassed HTTPRequestHandler
class ConciergeHandler(BaseHTTPServer.BaseHTTPRequestHandler):
    def logRequest(self):
        logging.info('[ConciergeHandler] %(command)s request: %(host)s:%(port)d --- %(path)s',
                     dict(command = self.command,
                          host = self.client_address[0],
                          port = self.client_address[1],
                          path = self.path))

    def logResponse(self, status):
        logging.info('[ConciergeHandler] %(command)s returned %(status)d',
                     dict(command = self.command,
                          status = status))
        

    def do_HEAD(self):
        self.logRequest()
        
        self.send_response(200)
        self.send_header('Content-type', 'text/html')
        self.end_headers()

        self.logResponse(200)

    def dumpXml(self, xml):
        logging.debug('[ConciergeHandler] %s', xml.tag)
        for attr in xml.attrib:
            logging.debug('[ConciergeHandler] %s [%s] %s', xml.tag, attr, xml.attrib[attr])
        for kid in xml.getchildren():
            self.dumpXml(kid)

    def do_POST(self):
        self.logRequest()
        hdrs = {}
        for hdr in self.headers.headers:
            logging.debug('[ConciergeHandler] POST: header:  %s', hdr.rstrip())

        length = int(self.headers.getheader('Content-Length'))
        content = self.rfile.read(length)
        self.rfile.close()
        
        logging.debug('[ConciergeHandler] POST: content: %s', content)
        try:
            postXml = ET.fromstring(content)
            self.dumpXml(postXml)
        except xml.parsers.expat.ExpatError, xmlError:
            logging.error('[ConciergeHandler] POST illformed:%s', xmlError)
            self.send_response(500)
            return

        if not options.fail:
            self.send_response(200)
            self.send_header('Content-Type', 'text/html')
            self.send_header('Content-Length', len('<success/>'))
            self.end_headers()
            self.logResponse(200)
            self.wfile.write('<success/>')
            self.wfile.close()
        else:
            self.send_response(500)
            self.send_header('Content-Type', 'text/html')
            self.send_header('Content-Length', len('<error>gotcha!</error>'))
            self.end_headers()
            self.wfile.write('<error>gotcha!</error>')
            self.wfile.close()

            self.logResponse(500)

    def log_request(code, size):
        pass

if __name__ == '__main__':

    logging.info('[ConciergeServer] Concierge Broker Test Server starting')

    parser = optparse.OptionParser()
    parser.add_option('-p', '--port', dest = 'port', help = 'port to listen on', metavar = 'PORT')
    parser.add_option('-f', '--fail', dest = 'fail', action = 'store_true', help = 'always fail POST requests')

    (options, args) = parser.parse_args()

    httpServer = BaseHTTPServer.HTTPServer(('', 8080), ConciergeHandler)
    try:
        httpServer.serve_forever()
    except KeyboardInterrupt:
        logging.info('[ConciergeServer] terminating')

    httpServer.server_close()
