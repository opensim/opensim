require "webrick"

#
# Dummy asset server
#

class AssetServlet < WEBrick::HTTPServlet::AbstractServlet
  def do_GET(req, res)
    uuid = req.path.split("/")[2].downcase.gsub(/[^0-9a-f]+/, "")
    if uuid.length == 32
      # length is correct
      File.open("assets/#{uuid}/data") do |f|
        res.body = f.read
      end
    end
    # res["content-type"] = "text/plain" # or what do we set it to ?
  end
  def do_POST(req, res)
    uuid = req.path.split("/")[2].downcase.gsub(/[^0-9a-f]+/, "")
    if uuid.length == 32
      Dir.mkdir("assets/#{uuid}")
      File.open("assets/#{uuid}/data", "wb") do |f|
        f.write req.body
	STDERR.print "Written #{req.body.length} bytes for uuid #{uuid}\n\n"
      end
    end
  end
end


svr = WEBrick::HTTPServer.new(:Port=>8003)
svr.mount("/assets", AssetServlet, 5000000)
trap(:INT){ svr.shutdown }
svr.start

