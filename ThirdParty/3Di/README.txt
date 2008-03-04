INTRODUCTION

This folder contains code that implement:

1. Dynamic load balancing

OpenSim is allowing many regions to share a region server, but the optimal
number of regions on each server depends on the load of each region, something
which may change as time goes on. 3Di is working on a load balancer that
allows the current load to be monitored and regions to be reassigned without
requiring the servers to be restarted. To move a region, its state is
serialized, and a new clone is created on the target server using this
stream. The old region is then destroyed and the client viewer updated to use
the new region address.

2. Region splitting

Currently each region can hold only a small number of avatars.  To allow more
avatars in each region, 3Di has implemented region splitting, in which several
copies of a given region can be distributed across the region servers. Each
sub-region updates a fraction of the avatars, and sends state updates to the
other sub-regions.

IMPLEMENTATION

The code is organised as follows:

* LoadBalancer: communicates with other region servers and creates/destroys
regions on command
* RegionMonitor/MonitorGUI: provides a browser GUI, showing the state of the
grid, and provides buttons for controlling region movement, splitting, and
merging.
* RegionMonitor/ServerPlugin: this is a region server plugin which 
communicates with the load balancer GUI to provide information
on the identity and status of the regions on the grid
* RegionProxy: maps messages from a clients to the true location of a region.

USAGE

In order to use these additions the following lines have to be added to
OpenSim.ini:

proxy_offset = -1000
proxy_url = http://10.8.1.50:9001
serialize_dir = /mnt/temp/

If defined, proxy_offset defines how to calculate the true region port, e.g.
if the XML defines the port as 9000 the actual port is 8000 if proxy_offset 
is -1000. The RegionProxy module will open a port at 9000 which the clients
can connect to, and route all traffic from there to port 8000. This allows
the region proxy to run on region server together with regions without
blocking them by using the same port number.

The proxy location is defined in proxy_url. When splitting, the region state
is stored on a file in the folder specified in serialize_dir. This has to be
a shared folder which both region servers involved in the split have access to.

3. Monitor GUI

RegionMonitor/MonitorGUI is used to view status of all the managed Region
servers, and send "Move", "Split", "Merge" commands to a specified Regions
server.

MonitorGUI is a web-based application. You can access it through a web browser.
Its back-end is written in perl. (CGI script)

Pre-requierments (CentOS, Fedora)

RPM package "perl-XML-RPC" and relevant packages.

Installation

1. Install Apache
2. copy all the files undef "ThirdParty/3Di/RegionMonitor/MonitorGUI/htdocs" to
"$APACHE_ROOT/htdocs"
3. Configuration in "monitor.cgi"
 * 10th line, set the value to your "monitor.cgi"'s location.
 * 11th line, set the value to your Grid server.
 * 12th line, set your region proxy port number here.
   (ref. OpenSim.ini::NetWork::http_listener_port)
* The code also works fine with mod_perl.

