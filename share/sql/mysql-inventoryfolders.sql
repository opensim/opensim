-- phpMyAdmin SQL Dump
-- version 2.11.2.2deb1
-- http://www.phpmyadmin.net
--
-- Host: localhost
-- Generation Time: Nov 23, 2007 at 10:19 PM
-- Server version: 5.0.45
-- PHP Version: 5.2.4-2

SET SQL_MODE="NO_AUTO_VALUE_ON_ZERO";

--
-- Database: `opensim`
--

-- --------------------------------------------------------

--
-- Table structure for table `inventoryfolders`
--

CREATE TABLE IF NOT EXISTS `inventoryfolders` (
  `folderID` varchar(36) NOT NULL default '',
  `agentID` varchar(36) default NULL,
  `parentFolderID` varchar(36) default NULL,
  `folderName` varchar(64) default NULL,
  PRIMARY KEY  (`folderID`),
  KEY `owner` (`agentID`),
  KEY `parent` (`parentFolderID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

--
-- Dumping data for table `inventoryfolders`
--
