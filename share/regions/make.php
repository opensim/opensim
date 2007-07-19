<?php

$i = 0;
for($x = 0; $x < 16; $x++)
{
	for($y = 0; $y < 16; $y++)
	{
		$ourFileName = "region_$i.xml";
		$ourFileHandle = fopen($ourFileName, 'w') or die("can't open file");
		fwrite($ourFileHandle , "<Root><Config sim_name=\"Test Sim #$i\" sim_location_x=\"" .  (1000 + $x) . "\" sim_location_y=\"" .  (1000 + $y) . "\" datastore=\"region_" . $i . "_datastore.yap\" internal_ip_address=\"0.0.0.0\" internal_ip_port=\"" .  (9000 + $i) . "\" external_host_name=\"127.0.0.1\" terrain_file=\"default.r32\" terrain_multiplier=\"60.0\" master_avatar_first=\"Test\" master_avatar_last=\"User\" master_avatar_pass=\"test\" /></Root>");
		fclose($ourFileHandle);
		$i++;
	}
}