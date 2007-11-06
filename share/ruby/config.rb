# Various config data

class SimConfig
  attr_reader :cfgSimName, :cfgSimIP, :cfgSimPort, :cfgSimX, 
  		:cfgSimX, :cfgSimY, :cfgAssetServerUrl, :cfgUserServerUrl

  def initialize
    @cfgSimName = "DalienLand"
    @cfgSimIP = "192.168.1.103"
    @cfgSimPort = "9000"
    @cfgSimX = 997
    @cfgSimY = 996
    @cfgSimX = 1000
    @cfgSimY = 1000
    @cfgAssetServerUrl = "http://192.168.1.103:8003/"
    @cfgUserServerUrl = "http://192.168.1.103:8003/"
  end

end


class UUID
  def initialize
    @uuid = rand(1<<128)
  end
  def to_dashed_s
    part1 = @uuid & 0xFFFFFFFFFFFF
    part2 = (@uuid >> 48) && 0xFFFF
    part3 = (@uuid >> (48 + 16)) & 0xFFFF
    part4 = (@uuid >> (48 + 32)) & 0xFFFF
    part5 = @uuid >> (128-32)
    return sprintf "%08x-%04x-%04x-%04x-%012x", part5, part4, part3, part2, part1
  end
end

print UUID.new.to_dashed_s


