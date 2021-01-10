<?php
//The description of the flags used in this file are being based on the
//DirFindFlags enum which is defined in OpenMetaverse/DirectoryManager.cs
//of the libopenmetaverse library.

include("databaseinfo.php");

// Attempt to connect to the database
try {
  $db = new PDO("mysql:host=$DB_HOST;dbname=$DB_NAME", $DB_USER, $DB_PASSWORD);
  $db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
}
catch(PDOException $e)
{
  echo "Error connecting to database\n";
  file_put_contents('PDOErrors.txt', $e->getMessage() . "\n-----\n", FILE_APPEND);
  exit;
}

#
#  Copyright (c)Melanie Thielker (http://opensimulator.org/)
#

###################### No user serviceable parts below #####################

//Join a series of terms together with optional parentheses around the result.
//This function is used in place of the simpler join to handle the cases where
//one or more of the supplied terms are an empty string. The parentheses can
//be added when mixing AND and OR clauses in a SQL query.
function join_terms($glue, $terms, $add_paren)
{
    if (count($terms) > 1)
    {
        $type = join($glue, $terms);
        if ($add_paren == True)
            $type = "(" . $type . ")";
    }
    else
    {
        if (count($terms) == 1)
            $type = $terms[0];
        else
            $type = "";
    }

    return $type;
}


function process_region_type_flags($flags)
{
    $terms = array();

    if ($flags & 16777216)  //IncludePG (1 << 24)
        $terms[] = "mature = 'PG'";
    if ($flags & 33554432)  //IncludeMature (1 << 25)
        $terms[] = "mature = 'Mature'";
    if ($flags & 67108864)  //IncludeAdult (1 << 26)
        $terms[] = "mature = 'Adult'";

    return join_terms(" OR ", $terms, True);
}


#
# The XMLRPC server object
#

$xmlrpc_server = xmlrpc_server_create();

#
# Places Query
#

xmlrpc_server_register_method($xmlrpc_server, "dir_places_query",
        "dir_places_query");

function dir_places_query($method_name, $params, $app_data)
{
    global $db;

    $req             = $params[0];

    $flags           = $req['flags'];
    $text            = $req['text'];
    $category        = $req['category'];
    $query_start     = $req['query_start'];

    $pieces = explode(" ", $text);
    $text = join("%", $pieces);

    if ($text != "%%%")
        $text = "%$text%";
    else
    {
        $response_xml = xmlrpc_encode(array(
                'success'      => False,
                'errorMessage' => "Invalid search terms"
        ));

        print $response_xml;

        return;
    }

    $terms = array();
    $sqldata = array();

    $type = process_region_type_flags($flags);
    if ($type != "")
        $type = " AND " . $type;

    if ($flags & 1024)
        $order = "dwell DESC,";

    if ($category <= 0)
        $cat_where = "";
    else
    {
        $cat_where = "searchcategory = :cat AND ";

        $sqldata['cat'] = $category;
    }

    $sqldata['text1'] = $text;
    $sqldata['text2'] = $text;

    //Prevent SQL injection by checking that $query_start is a number
    if (!is_int($query_start))
         $query_start = 0;

    $sql = "SELECT * FROM parcels WHERE $cat_where" .
           " (parcelname LIKE :text1" .
           " OR description LIKE :text2)" .
           $type . " ORDER BY $order parcelname" .
           " LIMIT $query_start,101";
    $query = $db->prepare($sql);
    $result = $query->execute($sqldata);

    $data = array();
    while ($row = $query->fetch(PDO::FETCH_ASSOC))
    {
        $data[] = array(
                "parcel_id" => $row["infouuid"],
                "name" => $row["parcelname"],
                "for_sale" => "False",
                "auction" => "False",
                "dwell" => $row["dwell"]);
    }
    $response_xml = xmlrpc_encode(array(
        'success'      => True,
        'errorMessage' => "",
        'data' => $data
    ));

    print $response_xml;
}

#
# Popular Places Query
#

xmlrpc_server_register_method($xmlrpc_server, "dir_popular_query",
        "dir_popular_query");

function dir_popular_query($method_name, $params, $app_data)
{
    global $db;

    $req         = $params[0];

    $text        = $req['text'];
    $flags       = $req['flags'];
    $query_start = $req['query_start'];

    $terms = array();
    $sqldata = array();

    if ($flags & 0x1000)    //PicturesOnly (1 << 12)
        $terms[] = "has_picture = 1";

    if ($flags & 0x0800)    //PgSimsOnly (1 << 11)
        $terms[] = "mature = 0";

    if ($text != "")
    {
        $terms[] = "(name LIKE :text)";

        $text = "%text%";
        $sqldata['text'] = $text;
    }

    if (count($terms) > 0)
        $where = " WHERE " . join_terms(" AND ", $terms, False);
    else
        $where = "";

    //Prevent SQL injection by checking that $query_start is a number
    if (!is_int($query_start))
         $query_start = 0;

    $query = $db->prepare("SELECT * FROM popularplaces" . $where .
                          " LIMIT $query_start,101");
    $result = $query->execute($sqldata);

    $data = array();
    while ($row = $query->fetch(PDO::FETCH_ASSOC))
    {
        $data[] = array(
                "parcel_id" => $row["infoUUID"],
                "name" => $row["name"],
                "dwell" => $row["dwell"]);
    }

    $response_xml = xmlrpc_encode(array(
            'success'      => True,
            'errorMessage' => "",
            'data' => $data));

    print $response_xml;
}

#
# Land Query
#

xmlrpc_server_register_method($xmlrpc_server, "dir_land_query",
        "dir_land_query");

function dir_land_query($method_name, $params, $app_data)
{
    global $db;

    $req            = $params[0];

    $flags          = $req['flags'];
    $type           = $req['type'];
    $price          = $req['price'];
    $area           = $req['area'];
    $query_start    = $req['query_start'];

    $terms = array();
    $sqldata = array();

    if ($type != 4294967295)    //Include all types of land?
    {
        //Do this check first so we can bail out quickly on Auction search
        if (($type & 26) == 2)  // Auction (from SearchTypeFlags enum)
        {
            $response_xml = xmlrpc_encode(array(
                    'success' => False,
                    'errorMessage' => "No auctions listed"));

            print $response_xml;

            return;
        }

        if (($type & 24) == 8)  //Mainland (24=0x18 [bits 3 & 4])
            $terms[] = "parentestate = 1";
        if (($type & 24) == 16) //Estate (24=0x18 [bits 3 & 4])
            $terms[] = "parentestate <> 1";
    }

    $s = process_region_type_flags($flags);
    if ($s != "")
        $terms[] = $s;

    if ($flags & 0x100000)  //LimitByPrice (1 << 20)
    {
        $terms[] = "saleprice <= :price";

        $sqldata['price'] = $price;
    }
    if ($flags & 0x200000)  //LimitByArea (1 << 21)
    {
        $terms[] = "area >= :area";

        $sqldata['area'] = $area;
    }

    //The PerMeterSort flag is always passed from a map item query.
    //It doesn't hurt to have this as the default search order.
    $order = "lsq";     //PerMeterSort (1 << 17)

    if ($flags & 0x80000)   //NameSort (1 << 19)
        $order = "parcelname";
    if ($flags & 0x10000)   //PriceSort (1 << 16)
        $order = "saleprice";
    if ($flags & 0x40000)   //AreaSort (1 << 18)
        $order = "area";
    if (!($flags & 0x8000)) //SortAsc (1 << 15)
        $order .= " DESC";

    if (count($terms) > 0)
        $where = " WHERE " . join_terms(" AND ", $terms, False);
    else
        $where = "";

    //Prevent SQL injection by checking that $query_start is a number
    if (!is_int($query_start))
         $query_start = 0;

    $sql = "SELECT *,saleprice/area AS lsq FROM parcelsales" . $where .
           " ORDER BY " . $order . " LIMIT $query_start,101";
    $query = $db->prepare($sql);
    $result = $query->execute($sqldata);

    $data = array();
    while ($row = $query->fetch(PDO::FETCH_ASSOC))
    {
        $data[] = array(
                "parcel_id" => $row["infoUUID"],
                "name" => $row["parcelname"],
                "auction" => "false",
                "for_sale" => "true",
                "sale_price" => $row["saleprice"],
                "landing_point" => $row["landingpoint"],
                "region_UUID" => $row["regionUUID"],
                "area" => $row["area"]);
    }

    $response_xml = xmlrpc_encode(array(
            'success'      => True,
            'errorMessage' => "",
            'data' => $data));

    print $response_xml;
}

#
# Events Query
#

xmlrpc_server_register_method($xmlrpc_server, "dir_events_query",
        "dir_events_query");

function dir_events_query($method_name, $params, $app_data)
{
    global $db;

    $req            = $params[0];

    $text           = $req['text'];
    $flags          = $req['flags'];
    $query_start    = $req['query_start'];

    if ($text == "%%%")
    {
        $response_xml = xmlrpc_encode(array(
                'success'      => False,
                'errorMessage' => "Invalid search terms"
        ));

        print $response_xml;

        return;
    }

    $pieces = explode("|", $text);

    $day        =    $pieces[0];
    $category   =    $pieces[1];
    if (count($pieces) < 3)
        $search_text = "";
    else
        $search_text = $pieces[2];

    $terms = array();
    $sqldata = array();

    //Event times are in UTC so we need to get the current time in UTC.
    $now = time();

    if ($day == "u")    //Searching for current or ongoing events?
    {
        //This condition will include upcoming and in-progress events
        $terms[] = "dateUTC+duration*60 >= " . $now;
    }
    else
    {
        //For events in a given day we need to determine the days start time
        $now -= idate("Z");     //Adjust for timezone
        $now -= ($now % 86400); //Adjust to start of day

        //Is $day a number of days before or after current date?
        if ($day != 0)
            $now += $day * 86400;

        $then = $now + 86400;   //Time for end of day

        //This condition will include any in-progress events
        $terms[] = "(dateUTC+duration*60 >= $now AND dateUTC < $then)";
    }

    if ($category > 0)
    {
        $terms[] = "category = :category";

        $sqldata['category'] = $category;
    }

    $type = array();
    if ($flags & 16777216)  //IncludePG (1 << 24)
        $type[] = "eventflags = 0";
    if ($flags & 33554432)  //IncludeMature (1 << 25)
        $type[] = "eventflags = 1";
    if ($flags & 67108864)  //IncludeAdult (1 << 26)
        $type[] = "eventflags = 2";

    //Was there at least one PG, Mature, or Adult flag?
    if (count($type) > 0)
        $terms[] = join_terms(" OR ", $type, True);

    if ($search_text != "")
    {
        $terms[] = "(name LIKE :text1 OR " .
                    "description LIKE :text2)";

        $search_text = "%$search_text%";
        $sqldata['text1'] = $search_text;
        $sqldata['text2'] = $search_text;
    }

    if (count($terms) > 0)
        $where = " WHERE " . join_terms(" AND ", $terms, False);
    else
        $where = "";

    //Prevent SQL injection by checking that $query_start is a number
    if (!is_int($query_start))
         $query_start = 0;

    $sql = "SELECT owneruuid,name,eventid,dateUTC,eventflags,globalPos" .
           " FROM events". $where. " LIMIT $query_start,101";
    $query = $db->prepare($sql);
    $result = $query->execute($sqldata);

    $data = array();
    while ($row = $query->fetch(PDO::FETCH_ASSOC))
    {
        $date = strftime("%m/%d %I:%M %p", $row["dateUTC"]);

        //The landing point is only needed when this event query is
        //called to allow placement of event markers on the world map.
        $data[] = array(
                "owner_id" => $row["owneruuid"],
                "name" => $row["name"],
                "event_id" => $row["eventid"],
                "date" => $date,
                "unix_time" => $row["dateUTC"],
                "event_flags" => $row["eventflags"],
                "landing_point" => $row["globalPos"]);
    }

    $response_xml = xmlrpc_encode(array(
            'success'      => True,
            'errorMessage' => "",
            'data' => $data));

    print $response_xml;
}

#
# Classifieds Query
#

xmlrpc_server_register_method($xmlrpc_server, "dir_classified_query",
        "dir_classified_query");

function dir_classified_query ($method_name, $params, $app_data)
{
    global $db;

    $req            = $params[0];

    $text           = $req['text'];
    $flags          = $req['flags'];
    $category       = $req['category'];
    $query_start    = $req['query_start'];

    if ($text == "%%%")
    {
        $response_xml = xmlrpc_encode(array(
                'success'      => False,
                'errorMessage' => "Invalid search terms"
        ));

        print $response_xml;

        return;
    }

    $terms = array();
    $sqldata = array();

    //Renew Weekly flag is bit 5 (32) in $flags.
    $f = array();
    if ($flags & 4)     //PG (1 << 2)
        $f[] = "classifiedflags & 4 = 4";
    if ($flags & 8)     //Mature (1 << 3)
        $f[] = "classifiedflags & 8 = 8";
    if ($flags & 64)    //Adult (1 << 6)
        $f[] = "classifiedflags & 64 = 64";

    //Was there at least one PG, Mature, or Adult flag?
    if (count($f) > 0)
        $terms[] = join_terms(" OR ", $f, True);

    //Only restrict results based on category if it is not 0 (Any Category)
    if ($category > 0)
    {
        $terms[] = "category = :category";

        $sqldata['category'] = $category;
    }

    if ($text != "")
    {
        $terms[] = "(name LIKE :text1" .
                   " OR description LIKE :text2)";

        $text = "%$text%";
        $sqldata['text1'] = $text;
        $sqldata['text2'] = $text;
    }

    //Was there at least condition for the search?
    if (count($terms) > 0)
        $where = " WHERE " . join_terms(" AND ", $terms, False);
    else
        $where = "";

    //Prevent SQL injection by checking that $query_start is a number
    if (!is_int($query_start))
         $query_start = 0;

    $sql = "SELECT * FROM classifieds" . $where .
           " ORDER BY priceforlisting DESC" .
           " LIMIT $query_start,101";
    $query = $db->prepare($sql);

    $result = $query->execute($sqldata);

    $data = array();
    while ($row = $query->fetch(PDO::FETCH_ASSOC))
    {
        $data[] = array(
                "classifiedid" => $row["classifieduuid"],
                "name" => $row["name"],
                "classifiedflags" => $row["classifiedflags"],
                "creation_date" => $row["creationdate"],
                "expiration_date" => $row["expirationdate"],
                "priceforlisting" => $row["priceforlisting"]);
    }

    $response_xml = xmlrpc_encode(array(
            'success'      => True,
            'errorMessage' => "",
            'data' => $data));

    print $response_xml;
}

#
# Events Info Query
#

xmlrpc_server_register_method($xmlrpc_server, "event_info_query",
        "event_info_query");

function event_info_query($method_name, $params, $app_data)
{
    global $db;

    $req        = $params[0];

    $eventID    = $req['eventID'];

    $query = $db->prepare("SELECT * FROM events WHERE eventID = ?");
    $result = $query->execute( array($eventID) );

    $data = array();
    while ($row = $query->fetch(PDO::FETCH_ASSOC))
    {
        $date = strftime("%G-%m-%d %H:%M:%S", $row["dateUTC"]);

        $category = "*Unspecified*";
        if ($row['category'] == 18)    $category = "Discussion";
        if ($row['category'] == 19)    $category = "Sports";
        if ($row['category'] == 20)    $category = "Live Music";
        if ($row['category'] == 22)    $category = "Commercial";
        if ($row['category'] == 23)    $category = "Nightlife/Entertainment";
        if ($row['category'] == 24)    $category = "Games/Contests";
        if ($row['category'] == 25)    $category = "Pageants";
        if ($row['category'] == 26)    $category = "Education";
        if ($row['category'] == 27)    $category = "Arts and Culture";
        if ($row['category'] == 28)    $category = "Charity/Support Groups";
        if ($row['category'] == 29)    $category = "Miscellaneous";

        $data[] = array(
                "event_id" => $row["eventid"],
                "creator" => $row["creatoruuid"],
                "name" => $row["name"],
                "category" => $category,
                "description" => $row["description"],
                "date" => $date,
                "dateUTC" => $row["dateUTC"],
                "duration" => $row["duration"],
                "covercharge" => $row["covercharge"],
                "coveramount" => $row["coveramount"],
                "simname" => $row["simname"],
                "globalposition" => $row["globalPos"],
                "eventflags" => $row["eventflags"]);
    }

    $response_xml = xmlrpc_encode(array(
            'success'      => True,
            'errorMessage' => "",
            'data' => $data));

    print $response_xml;
}

#
# Classifieds Info Query
#

xmlrpc_server_register_method($xmlrpc_server, "classifieds_info_query",
        "classifieds_info_query");

function classifieds_info_query($method_name, $params, $app_data)
{
    global $db;

    $req            = $params[0];

    $classifiedID   = $req['classifiedID'];

    $query = $db->prepare("SELECT * FROM classifieds WHERE classifieduuid = ?");
    $result = $query->execute( array($classifiedID) );

    $data = array();
    while ($row = $query->fetch(PDO::FETCH_ASSOC))
    {
        $data[] = array(
                "classifieduuid" => $row["classifieduuid"],
                "creatoruuid" => $row["creatoruuid"],
                "creationdate" => $row["creationdate"],
                "expirationdate" => $row["expirationdate"],
                "category" => $row["category"],
                "name" => $row["name"],
                "description" => $row["description"],
                "parceluuid" => $row["parceluuid"],
                "parentestate" => $row["parentestate"],
                "snapshotuuid" => $row["snapshotuuid"],
                "simname" => $row["simname"],
                "posglobal" => $row["posglobal"],
                "parcelname" => $row["parcelname"],
                "classifiedflags" => $row["classifiedflags"],
                "priceforlisting" => $row["priceforlisting"]);
    }

    $response_xml = xmlrpc_encode(array(
            'success'      => True,
            'errorMessage' => "",
            'data' => $data));

    print $response_xml;
}

#
# Process the request
#

$request_xml = file_get_contents("php://input");

xmlrpc_server_call_method($xmlrpc_server, $request_xml, '');
xmlrpc_server_destroy($xmlrpc_server);

$db = NULL;
?>
