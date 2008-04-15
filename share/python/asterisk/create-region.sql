USE %(database)s;
REPLACE INTO `extensions_table` (context,exten,priority,app,appdata) VALUES ('avatare', '%(regionname)s', 1, 'Answer', '');
REPLACE INTO `extensions_table` (context,exten,priority,app,appdata) VALUES ('avatare', '%(regionname)s', 2, 'Wait', '1');
REPLACE INTO `extensions_table` (context,exten,priority,app,appdata) VALUES ('avatare', '%(regionname)s', 3, 'Meetme', '%(regionname)s|Acdi');