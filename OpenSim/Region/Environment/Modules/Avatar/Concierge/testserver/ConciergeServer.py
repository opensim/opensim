#!/usr/bin/env python
# -*- encoding: utf-8 -*-

import logging
import BaseHTTPServer

class ConciergeHandler(BaseHTTPServer.BaseHTTPRequestHandler):
    def do_HEAD(req):
        logging.info('[Concierge] %(command)s request: %(host)s:%(port)d --- %(path)s',
                     dict(command = self.command,
                          host = self.client_address[0],
                          port = self.client_address[1],
                          path = self.path))
        
        req.send_response(200)
        req.send_header('Content-type', 'text/html')
        req.send_headers()

        logging.info('[Concierge] %(command)s returned 200', dict(command = self.command))

if __name__ == '__main__':

    httpServer = BaseHTTPServer.HTTPServer(('', 8080), ConciergeHandler)
    logging.info('[ConciergeServer] starting')

    try:
        httpServer.serve_forever()
    except KeyboardInterrupt:
        logging.info('[ConciergeServer] terminating')

    httpServer.server_close()
