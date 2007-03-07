<?
// All the asset server specific stuff lives here

// The asset server's relative URL to the root of the webserver
// If you place this at http://servername/assets then this would be set to /assets/index.php
// wikipedia style URLs need to be implemented - and will be (en.wikipedia.org/wiki/blabla rather than en.wikipedia.org/wiki/index.php?something=bla or en.wikipedia.org/wiki.php/bla)
$asset_home = "/ogs/assetserver/";

// The key we expect from sims
$sim_recvkey = "1234";

// The path where the asset repository is stored, this should be readable by the webserver but NOT in the document root
// The default below is BAD for production use and intended to be simply generic - change it or risk copyright theft 
// greater than you could ever imagine, alternatively use .htaccess or other mechanisms.
$asset_repos = "/usr/local/sites/osgrid.org/web/ogs/assetserver/assets";
?>
