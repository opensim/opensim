BEGIN TRANSACTION;

INSERT INTO UserAccounts (PrincipalID, ScopeID, FirstName, LastName, Email, ServiceURLs, Created) SELECT `UUID` AS PrincipalID, '00000000-0000-0000-0000-000000000000' AS ScopeID, username AS FirstName, lastname AS LastName, email as Email, CONCAT('AssetServerURI=', userAssetURI, ' InventoryServerURI=', userInventoryURI, ' GatewayURI= HomeURI=') AS ServiceURLs, created as Created FROM users;

COMMIT;
