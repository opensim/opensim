USE %(database)s;
REPLACE INTO ast_sipfriends (port,context,disallow,allow,type,secret,host,name) VALUES ('5060','avatare','all','ulaw','friend','%(password)s','dynamic','%(username)s');
REPLACE INTO `extensions_table` (context,exten,priority,app,appdata) VALUES ('avatare', '%(username)s', 1, 'Answer', '');
REPLACE INTO `extensions_table` (context,exten,priority,app,appdata) VALUES ('avatare', '%(username)s', 2, 'Wait', '1');
REPLACE INTO `extensions_table` (context,exten,priority,app,appdata) VALUES ('avatare', '%(username)s', 3, 'Dial', 'SIP/%(username)s,60');