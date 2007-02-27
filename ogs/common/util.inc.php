<?
// Some generic utilities which may be used by any services

function inc_lluuid($lluuid)
{
    $partB = substr($lluuid, 15);
    $partB = (float)base_convert($partB,16,10)+1;
    $partB = sprintf('%016x', $partB);
    $partA = substr($lluuid, 0, 16);

    if(substr($lluuid,15,16)=='FFFFFFFFFFFFFFFE') {
	$partA = (float)base_convert($partA,16,10)+1;
	$partA = sprintf('%016x', $partA);
    }

    $returnval = sprintf('%s%s',$partA, $partB);
    
    return $returnval;
}

function format_lluuid($uuid)
{
    return strtolower(substr($uuid,0,8)."-".substr($uuid,8,4)."-".substr($uuid,12,4)."-".substr($uuid,16,4)."-".substr($uuid,20));
}

function output_xml_block($blockname, $data) {
	echo("<$blockname>\n");
	foreach($data as $name => $value) {
		echo(" <$name>$value</$name>\n");		
	}
	echo("</$blockname>\n");
}

function rand_uuid()
{
   return sprintf( '%04x%04x-%04x-%04x-%04x-%04x%04x%04x',
   mt_rand( 0, 0xffff ), mt_rand( 0, 0xffff ), mt_rand( 0, 0xffff ),
   mt_rand( 0, 0x0fff ) | 0x4000,
   mt_rand( 0, 0x3fff ) | 0x8000,
   mt_rand( 0, 0xffff ), mt_rand( 0, 0xffff ), mt_rand( 0, 0xffff ) );
}
?>
