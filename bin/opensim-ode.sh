#!/bin/sh
echo "Starting OpenSimulator with ODE.  If you get an error saying limit: Operation not permitted.  Then you will need to chmod 0600 /etc/limits"
ulimit -s 262144
sleep 5
mono OpenSim.exe -physics=OpenDynamicsEngine
