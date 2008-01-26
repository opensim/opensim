require "webrick"
require "xmlrpc/server"
require 'xmlrpc/client'
require 'pp'
require 'config.rb'

#
# Dummy grid server
#
#

class SimServlet < WEBrick::HTTPServlet::AbstractServlet
  # does actually nothing
  def do_POST(req, res)
    STDERR.print "----\n"
  end
end

$SimUUID = ""

s = XMLRPC::WEBrickServlet.new
s.add_handler("map_block") do |param|
  # does just enough to login.. if you try using "map" you will cause the exception
  # and hang the client
  responseData = Hash.new
  responseData["sim-profiles"] =  [ ]
  responseData
end

s.add_handler("simulator_login") do |param|
  sc = SimConfig.new
  responseData = Hash.new
  STDERR.print "simulator login: " + param.inspect + "\n"
  $SimUUID = param["UUID"]

  responseData["UUID"] = param["UUID"]
  responseData["region_locx"] = sc.cfgSimX
  responseData["region_locy"] = sc.cfgSimY
  responseData["regionname"] = "DalienLand"
  responseData["estate_id"] = "1"
  responseData["neighbours"] = [ ]
  responseData["sim_ip"] = sc.cfgSimIP
  responseData["sim_port"] = sc.cfgSimPort
  responseData["asset_url"] = sc.cfgAssetServerUrl
  responseData["asset_sendkey"] = ""
  responseData["asset_recvkey"] = ""
  responseData["user_url"] = sc.cfgUserServerUrl
  responseData["user_sendkey"] = ""
  responseData["user_recvkey"] = ""
  responseData["authkey"] = ""

  responseData

end

s.set_default_handler do |name, *args|
  STDERR.print "Unknown method #{name}, #{args.inspect}\n\n"
  raise XMLRPC::FaultException.new(-99, "Method #{name} missing" +
                                   " or wrong number of parameters!")
end

httpserver = WEBrick::HTTPServer.new(:Port => 8001)    
httpserver.mount("/", s)
httpserver.mount("/sims", SimServlet)

trap(:INT) { httpserver.shutdown }   # use 1 instead of "HUP" on Windows
httpserver.start


