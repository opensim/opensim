How to get this working on Linux/Apache:

Create a new directory /var/www/monitor and copy all files htdocs/* files there.

Include these lines in /etc/apache2/httpdocs
---
<Directory /var/www/monitor>
    Options +ExecCGI
</Directory>
AddHandler cgi-script .cgi
---

Restart Apache: sudo /etc/init.d/apache2 restart

Check that the perl XML-RPC modules is available  ("sudo apt-get install librcp-xml-perl" on Ubuntu)

Edit /var/www/monitor/monitor.cgi to update the IP addresses for the Grid server (TODO: improve this)

Start OpenSim in grid mode, use a browser to open http://localhost/monitor/monitor.cgi


