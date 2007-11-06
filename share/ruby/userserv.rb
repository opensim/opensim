require "webrick"
require "xmlrpc/server"
require 'xmlrpc/client'
require 'config.rb'

class SessionServlet < WEBrick::HTTPServlet::AbstractServlet
  def do_DELETE(req, res)
    # does nothing, obviously
    STDERR.print "----\n"
  end
end

s = XMLRPC::WEBrickServlet.new
s.add_handler("login_to_simulator") do |param|
   sc = SimConfig.new
   #
   # Some stuff just grabbed from a sniff of the session with OGS
   #
   zSessionId = "133086b6-1270-78c6-66f7-c7f64865b16c"
   zSecureSessionId = "6ee4df6a-0ea9-4cf5-8ac7-9745acbacccc"
   zAgentId = "0f00ba47-42d1-498e-b010-aa585a81862e"
   zAgentId = UUID.new.to_dashed_s
   STDERR.print "AgentID: #{zAgentId}\n"
   zCircuitCode = rand(0x1000000)
   zRegionX = sc.cfgSimX
   zRegionY = sc.cfgSimY

   xxSimParams = Hash.new
   xxSimParams["session_id"] = zSessionId.gsub("-","")
   xxSimParams["secure_session_id"] = zSecureSessionId.gsub("-","")
   xxSimParams["firstname"] = param["first"];
   xxSimParams["lastname"] = param["last"];
   xxSimParams["agent_id"] = zAgentId.gsub("-", "")
   xxSimParams["circuit_code"] = zCircuitCode
   xxSimParams["startpos_x"] = 128
   xxSimParams["startpos_y"] = 128
   xxSimParams["startpos_z"] = 30
   xxSimParams["regionhandle"] = ((zRegionX << 40) + (zRegionY *256)).to_s
   STDERR.print "Region handle: #{xxSimParams["regionhandle"]}\n"



   server = XMLRPC::Client.new2("http://#{sc.cfgSimIP}:#{sc.cfgSimPort}/")
   # the dispatcher in OpenSim.exe did not get excited from specifying
   # the content-type in the request.. no XML was executed at all.
   # this "fixes" it.
   server.http_header_extra = { "Content-Type" => "text/xml" };
   result = server.call("expect_user", xxSimParams )
   # STDERR.print result.inspect

   STDERR.print "---- notified the sim ----\n"

   responseData = Hash.new

   xxGlobalT = Hash.new
   xxGlobalT["sun_texture_id"] = "cce0f112-878f-4586-a2e2-a8f104bba271";
   xxGlobalT["cloud_texture_id"] = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";
   xxGlobalT["moon_texture_id"] = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";

   xxLoginFlags = Hash.new
   xxLoginFlags["daylight_savings"] = "N"
   xxLoginFlags["stipend_since_login"] = "N"
   xxLoginFlags["gendered"] = "Y"
   xxLoginFlags["ever_logged_in"] = "Y"

   responseData["first_name"] = param["first"]
   responseData["last_name"] = param["last"]
   responseData["ui-config"] = [ { "allow_first_life" => "Y" } ]

   responseData["login-flags"] = [ xxLoginFlags ]
   responseData["global-textures"] = [ xxGlobalT ]
   # responseData["classified_categories"] = [ { category_name => "Generic", category_id => 1 } ]
   # responseData["event_categories"] = 
   responseData["inventory-skeleton"] = [
   	  { "folder_id" => "9846e02a-f41b-4199-860e-cde46cc25649",
	    "parent_id" => "00000000-0000-0000-0000-000000000000",
	    "name" => "My Inventory test",
	    "type_default" => 8,
	    "version" => 1 },
   	  { "folder_id" => "b846e02a-f41b-4199-860e-cde46cc25649",
   	    "parent_id" => "9846e02a-f41b-4199-860e-cde46cc25649",
	    "name" => "test",
	    "type_default" => 0,
	    "version" => 1 }
   	]
   responseData["inventory-skel-lib"] = [ 
   	  { "folder_id" => "a846e02a-f41b-4199-860e-cde46cc25649",
	    "parent_id" => "00000000-0000-0000-0000-000000000000",
	    "name" => "Lib Inventory",
	    "type_default" => 8,
	    "version" => 1 }
   	]
   responseData["inventory-root"] = [ { "folder_id" => "9846e02a-f41b-4199-860e-cde46cc25649" } ]
   # responseData["event_notifications"] = [ ]
   responseData["gestures"] = [ ]
   # responseData["inventory-lib-owner"] = 
   responseData["initial-outfit"] = [
   	{ "folder_name" => "Nightclub female", "gender" => "female" }
      	]
   responseData["seconds_since_epoch"] = Time.new.to_i
   responseData["start_location"] = "last";
   responseData["message"] = "Hello there!"
   responseData["circuit_code"] = zCircuitCode # random
   # responseData["look_at"] = 
   responseData["agent_id"] = zAgentId
   responseData["home"] = "\{'region_handle':[r#{zRegionX*256}.0,r#{zRegionY*256}.0], 'position':[r128.0,r128.0,r30.0], 'look_at':[r0.0,r0.0,r0.0]\}"
   # responseData["home"] = "\{'region_handle':[r255232,r254976], 'position':[r128,r128,r100], 'look_at':[r128,r128,r100]\}"

   responseData["region_x"] = zRegionX*256
   responseData["region_y"] = zRegionY*256
   responseData["sim_ip"] = "192.168.1.103" 
   responseData["sim_port"] = 9000
   # responseData["seed_capability"] 
   responseData["agent_access"] = "M";
   responseData["session_id"] = zSessionId
   responseData["secure_session_id"] = zSecureSessionId
   responseData["login"] = "true"

   # raise XMLRPC::FaultException.new(1, "just some exception")
   responseData
end

s.set_default_handler do |name, *args|
  STDERR.print "Unknown method #{name}, #{args.inspect}\n\n"
  raise XMLRPC::FaultException.new(-99, "Method #{name} missing" +
                                   " or wrong number of parameters!")
end

httpserver = WEBrick::HTTPServer.new(:Port => 8002)    
httpserver.mount("/", s)
httpserver.mount("/usersessions", SessionServlet);

trap(:INT) { httpserver.shutdown }  
httpserver.start

