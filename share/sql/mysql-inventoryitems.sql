-- PHP Version: 5.2.4-2

SET SQL_MODE="NO_AUTO_VALUE_ON_ZERO";

--
-- Database: `opensim`
--

-- --------------------------------------------------------

--
-- Table structure for table `inventoryitems`
--

CREATE TABLE IF NOT EXISTS `inventoryitems` (
  `inventoryID` varchar(36) NOT NULL default '',
  `assetID` varchar(36) default NULL,
  `type` int(11) default NULL,
  `parentFolderID` varchar(36) default NULL,
  `avatarID` varchar(36) default NULL,
  `inventoryName` varchar(64) default NULL,
  `inventoryDescription` varchar(64) default NULL,
  `inventoryNextPermissions` int(10) unsigned default NULL,
  `inventoryCurrentPermissions` int(10) unsigned default NULL,
  PRIMARY KEY  (`inventoryID`),
  KEY `owner` (`avatarID`),
  KEY `folder` (`parentFolderID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

--
-- Dumping data for table `inventoryitems`
--
