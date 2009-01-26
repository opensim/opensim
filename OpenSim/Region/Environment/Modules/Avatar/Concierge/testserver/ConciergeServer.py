#!/usr/bin/env python
# -*- encoding: utf-8 -*-

import logging
import BaseHTTPServer

# enable debug level logging
logging.basicConfig(level = logging.DEBUG,
                    format='%(asctime)s %(levelname)s %(message)s')

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

    def do_POST(self):
        self.logRequest()
        hdrs = {}
        for hdr in self.headers.headers:
            logging.debug('[ConciergeHandler] POST: header:  %s', hdr.rstrip())

        length = int(self.headers.getheader('Content-Length'))
        content = self.rfile.read(length)
        self.rfile.close()
        
        logging.debug('[ConciergeHandler] POST: content: %s', content)
            
        self.send_response(200)
        self.send_header('Content-type', 'text/html')
        self.end_headers()

        self.logResponse(200)

    def log_request(code, size):
        pass

if __name__ == '__main__':

    httpServer = BaseHTTPServer.HTTPServer(('', 8080), ConciergeHandler)
    logging.info('[ConciergeServer] starting')

    try:
        httpServer.serve_forever()
    except KeyboardInterrupt:
        logging.info('[ConciergeServer] terminating')

    httpServer.server_close()
