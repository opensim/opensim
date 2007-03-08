-- phpMyAdmin SQL Dump
-- version 2.6.3-pl1
-- http://www.phpmyadmin.net
-- 
-- Host: 127.0.0.1
-- Generation Time: Feb 16, 2007 at 09:54 PM
-- Server version: 4.0.23
-- PHP Version: 4.4.0
-- 
-- Database: `OGS`
-- 

-- --------------------------------------------------------

-- 
-- Table structure for table `Grid_settings`
-- 

DROP TABLE IF EXISTS `Grid_settings`;
CREATE TABLE IF NOT EXISTS `Grid_settings` (
  `Setting` text NOT NULL,
  `value` text NOT NULL
) TYPE=MyISAM;

-- 
-- Dumping data for table `Grid_settings`
-- 

INSERT INTO `Grid_settings` (`Setting`, `value`) VALUES ('highest_LLUUID', '51AEFF430000000000000000000002fd');

-- --------------------------------------------------------

-- 
-- Table structure for table `foreign_profiles`
-- 

DROP TABLE IF EXISTS `foreign_profiles`;
CREATE TABLE IF NOT EXISTS `foreign_profiles` (
  `userprofile_LLUUID` varchar(32) NOT NULL default '',
  `foreigngrid` text NOT NULL,
  `profile_firstname` text NOT NULL,
  `profile_lastname` text NOT NULL,
  `profile_passwdmd5` text NOT NULL,
  `homesim_ip` text NOT NULL,
  `homesim_port` int(11) NOT NULL default '0',
  `homeasset_url` text NOT NULL,
  `homeuser_url` text NOT NULL,
  `look_at` text NOT NULL,
  `region_handle` text NOT NULL,
  `position` text NOT NULL,
  PRIMARY KEY  (`userprofile_LLUUID`)
) TYPE=MyISAM;

-- 
-- Dumping data for table `foreign_profiles`
-- 


-- --------------------------------------------------------

-- 
-- Table structure for table `local_user_profiles`
-- 

DROP TABLE IF EXISTS `local_user_profiles`;
CREATE TABLE IF NOT EXISTS `local_user_profiles` (
  `userprofile_LLUUID` varchar(32) NOT NULL default '',
  `profile_firstname` text NOT NULL,
  `profile_lastname` text NOT NULL,
  `profile_passwdmd5` text NOT NULL,
  `homesim_ip` text NOT NULL,
  `homesim_port` int(11) NOT NULL default '0',
  `homeasset_url` text NOT NULL,
  `look_at` text NOT NULL,
  `region_handle` text NOT NULL,
  `position` text NOT NULL,
  PRIMARY KEY  (`userprofile_LLUUID`)
) TYPE=MyISAM;

-- 
-- Dumping data for table `local_user_profiles`
-- 

INSERT INTO `local_user_profiles` (`userprofile_LLUUID`, `profile_firstname`, `profile_lastname`, `profile_passwdmd5`, `homesim_ip`, `homesim_port`, `homeasset_url`, `look_at`, `region_handle`, `position`) VALUES ('51AEFF43000000000000000000000100', 'Test', 'User', '$1$098f6bcd4621d373cade4e832627b4f6', '127.0.0.1', 1000, 'http://dummyassetserver.net/', 'r-0.57343, r-0.819255,r0', 'r255232,254976', 'r41.6589, r100.8374, r22.5072');

-- --------------------------------------------------------

-- 
-- Table structure for table `region_profiles`
-- 

DROP TABLE IF EXISTS `region_profiles`;
CREATE TABLE IF NOT EXISTS `region_profiles` (
  `RegionID` varchar(32) NOT NULL default '',
  `Name` text NOT NULL,
  `GridLocX` bigint(20) NOT NULL default '0',
  `GridLocY` bigint(20) NOT NULL default '0',
  `region_handle` text NOT NULL,
  `ip_addr` text NOT NULL,
  `port` text NOT NULL,
  PRIMARY KEY  (`RegionID`)
) TYPE=MyISAM;

-- 
-- Dumping data for table `region_profiles`
-- 

INSERT INTO `region_profiles` (`RegionID`, `Name`, `GridLocX`, `GridLocY`, `region_handle`, `ip_addr`, `port`) VALUES ('51AEFF43000000000000000000000200', 'Test sandbox', 997, 996, 'r255232,254976', '127.0.0.1', '1000');

-- --------------------------------------------------------

-- 
-- Table structure for table `sessions`
-- 

DROP TABLE IF EXISTS `sessions`;
CREATE TABLE IF NOT EXISTS `sessions` (
  `session_id` varchar(32) NOT NULL default '',
  `secure_session_id` text NOT NULL,
  `agent_id` text NOT NULL,
  `session_start` datetime NOT NULL default '0000-00-00 00:00:00',
  `session_end` datetime NOT NULL default '0000-00-00 00:00:00',
  `session_active` tinyint(4) NOT NULL default '0',
  `current_location` text NOT NULL,
  `remote_ip` text NOT NULL,
  `circuit_code` int(11) NOT NULL default '0',
  PRIMARY KEY  (`session_id`)
) TYPE=MyISAM;

-- 
-- Dumping data for table `sessions`
-- 

INSERT INTO `sessions` (`session_id`, `secure_session_id`, `agent_id`, `session_start`, `session_end`, `session_active`, `current_location`, `remote_ip`, `circuit_code`) VALUES ('51AEFF430000000000000000000002fc', '51AEFF430000000000000000000002fd', '51AEFF43000000000000000000000100', '2007-02-16 21:13:19', '0000-00-00 00:00:00', 1, 'r255232,254976', '81.174.255.70', 0);
