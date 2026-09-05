CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902104428_P0T02_Initial') THEN

    ALTER DATABASE CHARACTER SET utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902104428_P0T02_Initial') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902104428_P0T02_Initial', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902111457_P0T03_AddIdempotencyRecord') THEN

    CREATE TABLE `IdempotencyRecord` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Key` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Method` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Path` varchar(2048) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `StatusCode` int NULL,
        `ContentType` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `ResponseBody` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_IdempotencyRecord` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902111457_P0T03_AddIdempotencyRecord') THEN

    CREATE INDEX `IX_IdempotencyRecord_CreatedAtUtc` ON `IdempotencyRecord` (`CreatedAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902111457_P0T03_AddIdempotencyRecord') THEN

    CREATE UNIQUE INDEX `IX_IdempotencyRecord_Key` ON `IdempotencyRecord` (`Key`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902111457_P0T03_AddIdempotencyRecord') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902111457_P0T03_AddIdempotencyRecord', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902113306_P0T04_AddUsersAndRefreshTokens') THEN

    CREATE TABLE `User` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Email` varchar(256) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Mobile` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `PasswordHash` varchar(512) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `RoleId` bigint NULL,
        `DepartmentId` bigint NULL,
        `IsActive` bit(1) NOT NULL,
        `AccessFailedCount` int NOT NULL,
        `LockoutEndUtc` datetime(6) NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_User` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902113306_P0T04_AddUsersAndRefreshTokens') THEN

    CREATE TABLE `RefreshToken` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `UserId` bigint NOT NULL,
        `TokenHash` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `FamilyId` char(36) COLLATE ascii_general_ci NOT NULL,
        `ExpiresAtUtc` datetime(6) NOT NULL,
        `ConsumedAtUtc` datetime(6) NULL,
        `RevokedAtUtc` datetime(6) NULL,
        `ReplacedByTokenHash` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_RefreshToken` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_RefreshToken_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902113306_P0T04_AddUsersAndRefreshTokens') THEN

    CREATE INDEX `IX_RefreshToken_FamilyId` ON `RefreshToken` (`FamilyId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902113306_P0T04_AddUsersAndRefreshTokens') THEN

    CREATE UNIQUE INDEX `IX_RefreshToken_TokenHash` ON `RefreshToken` (`TokenHash`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902113306_P0T04_AddUsersAndRefreshTokens') THEN

    CREATE INDEX `IX_RefreshToken_UserId` ON `RefreshToken` (`UserId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902113306_P0T04_AddUsersAndRefreshTokens') THEN

    CREATE INDEX `IX_User_DepartmentId` ON `User` (`DepartmentId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902113306_P0T04_AddUsersAndRefreshTokens') THEN

    CREATE UNIQUE INDEX `IX_User_Email` ON `User` (`Email`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902113306_P0T04_AddUsersAndRefreshTokens') THEN

    CREATE INDEX `IX_User_RoleId` ON `User` (`RoleId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902113306_P0T04_AddUsersAndRefreshTokens') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902113306_P0T04_AddUsersAndRefreshTokens', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902115929_P0T05_AddRbac') THEN

    CREATE TABLE `Permission` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Key` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Module` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Action` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Description` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Permission` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902115929_P0T05_AddRbac') THEN

    CREATE TABLE `Role` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Description` varchar(400) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `IsActive` bit(1) NOT NULL,
        `IsSystem` bit(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Role` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902115929_P0T05_AddRbac') THEN

    CREATE TABLE `UserProjectAccess` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `UserId` bigint NOT NULL,
        `ProjectId` bigint NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_UserProjectAccess` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_UserProjectAccess_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902115929_P0T05_AddRbac') THEN

    CREATE TABLE `RolePermission` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `RoleId` bigint NOT NULL,
        `PermissionId` bigint NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_RolePermission` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_RolePermission_Permission_PermissionId` FOREIGN KEY (`PermissionId`) REFERENCES `Permission` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_RolePermission_Role_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `Role` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902115929_P0T05_AddRbac') THEN

    CREATE UNIQUE INDEX `IX_Permission_Key` ON `Permission` (`Key`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902115929_P0T05_AddRbac') THEN

    CREATE UNIQUE INDEX `IX_Role_Name` ON `Role` (`Name`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902115929_P0T05_AddRbac') THEN

    CREATE INDEX `IX_RolePermission_PermissionId` ON `RolePermission` (`PermissionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902115929_P0T05_AddRbac') THEN

    CREATE UNIQUE INDEX `IX_RolePermission_RoleId_PermissionId` ON `RolePermission` (`RoleId`, `PermissionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902115929_P0T05_AddRbac') THEN

    CREATE INDEX `IX_UserProjectAccess_ProjectId` ON `UserProjectAccess` (`ProjectId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902115929_P0T05_AddRbac') THEN

    CREATE UNIQUE INDEX `IX_UserProjectAccess_UserId_ProjectId` ON `UserProjectAccess` (`UserId`, `ProjectId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902115929_P0T05_AddRbac') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902115929_P0T05_AddRbac', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902121045_P0T06_AddAuditLogAndLedgerEntry') THEN

    CREATE TABLE `AuditLog` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `UserId` bigint NULL,
        `TimestampUtc` datetime(6) NOT NULL,
        `Module` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Action` varchar(40) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `EntityType` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `RecordId` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `OldValues` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `NewValues` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Details` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `ReconciliationStatus` varchar(40) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `ReconciliationUserId` bigint NULL,
        `ReconciliationDate` datetime(6) NULL,
        `ReversalHistory` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_AuditLog` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902121045_P0T06_AddAuditLogAndLedgerEntry') THEN

    CREATE TABLE `LedgerEntry` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `EntryDate` date NOT NULL,
        `ProjectId` bigint NULL,
        `AccountId` bigint NULL,
        `PartyId` bigint NULL,
        `CategoryId` bigint NOT NULL,
        `Debit` decimal(18,2) NOT NULL,
        `Credit` decimal(18,2) NOT NULL,
        `SourceType` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `SourceId` bigint NOT NULL,
        `IsReversal` bit(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_LedgerEntry` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902121045_P0T06_AddAuditLogAndLedgerEntry') THEN

    CREATE INDEX `IX_AuditLog_Module_Action` ON `AuditLog` (`Module`, `Action`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902121045_P0T06_AddAuditLogAndLedgerEntry') THEN

    CREATE INDEX `IX_AuditLog_RecordId` ON `AuditLog` (`RecordId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902121045_P0T06_AddAuditLogAndLedgerEntry') THEN

    CREATE INDEX `IX_AuditLog_TimestampUtc` ON `AuditLog` (`TimestampUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902121045_P0T06_AddAuditLogAndLedgerEntry') THEN

    CREATE INDEX `IX_AuditLog_UserId` ON `AuditLog` (`UserId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902121045_P0T06_AddAuditLogAndLedgerEntry') THEN

    CREATE INDEX `IX_LedgerEntry_PartyId_EntryDate` ON `LedgerEntry` (`PartyId`, `EntryDate`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902121045_P0T06_AddAuditLogAndLedgerEntry') THEN

    CREATE INDEX `IX_LedgerEntry_ProjectId_EntryDate` ON `LedgerEntry` (`ProjectId`, `EntryDate`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902121045_P0T06_AddAuditLogAndLedgerEntry') THEN

    CREATE INDEX `IX_LedgerEntry_SourceType_SourceId` ON `LedgerEntry` (`SourceType`, `SourceId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902121045_P0T06_AddAuditLogAndLedgerEntry') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902121045_P0T06_AddAuditLogAndLedgerEntry', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902132646_P1T01_AddProject') THEN

    CREATE TABLE `Project` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Code` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `ClientId` bigint NULL,
        `SiteAddress` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `ContactDetails` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `StartDate` date NOT NULL,
        `ExpectedEndDate` date NULL,
        `ActualEndDate` date NULL,
        `ContractValue` decimal(18,2) NOT NULL,
        `EstimatedCost` decimal(18,2) NOT NULL,
        `ExpectedProfit` decimal(18,2) NULL,
        `Status` tinyint NOT NULL,
        `ManagerId` bigint NULL,
        `Notes` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `IsActive` bit(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Project` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Project_User_ManagerId` FOREIGN KEY (`ManagerId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902132646_P1T01_AddProject') THEN

    CREATE INDEX `IX_Project_ClientId` ON `Project` (`ClientId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902132646_P1T01_AddProject') THEN

    CREATE UNIQUE INDEX `IX_Project_Code` ON `Project` (`Code`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902132646_P1T01_AddProject') THEN

    CREATE INDEX `IX_Project_ManagerId` ON `Project` (`ManagerId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902132646_P1T01_AddProject') THEN

    CREATE INDEX `IX_Project_StartDate` ON `Project` (`StartDate`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902132646_P1T01_AddProject') THEN

    CREATE INDEX `IX_Project_Status` ON `Project` (`Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902132646_P1T01_AddProject') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902132646_P1T01_AddProject', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902135753_P1T02_AddParty') THEN

    CREATE TABLE `Party` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `NormalisedName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Types` int NOT NULL,
        `Category` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `ContactPerson` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Phone` varchar(40) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Email` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Address` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `GstNumber` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `BankDetails` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `PaymentTerms` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `DepartmentId` bigint NULL,
        `IsActive` bit(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Party` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902135753_P1T02_AddParty') THEN

    CREATE INDEX `IX_Party_DepartmentId` ON `Party` (`DepartmentId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902135753_P1T02_AddParty') THEN

    CREATE UNIQUE INDEX `IX_Party_NormalisedName` ON `Party` (`NormalisedName`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902135753_P1T02_AddParty') THEN

    ALTER TABLE `Project` ADD CONSTRAINT `FK_Project_Party_ClientId` FOREIGN KEY (`ClientId`) REFERENCES `Party` (`Id`) ON DELETE RESTRICT;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902135753_P1T02_AddParty') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902135753_P1T02_AddParty', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902154642_P1T03_AddItemMaster') THEN

    CREATE TABLE `ItemCategory` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `NormalisedName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `IsActive` bit(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_ItemCategory` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902154642_P1T03_AddItemMaster') THEN

    CREATE TABLE `Unit` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Code` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `NormalisedCode` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `SortOrder` int NOT NULL,
        `IsActive` bit(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Unit` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902154642_P1T03_AddItemMaster') THEN

    CREATE TABLE `Item` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `NormalisedName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `CategoryId` bigint NULL,
        `Unit` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `DefaultRate` decimal(18,2) NOT NULL,
        `TaxRate` decimal(18,2) NOT NULL,
        `IsActive` bit(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Item` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Item_ItemCategory_CategoryId` FOREIGN KEY (`CategoryId`) REFERENCES `ItemCategory` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902154642_P1T03_AddItemMaster') THEN

    CREATE INDEX `IX_Item_CategoryId` ON `Item` (`CategoryId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902154642_P1T03_AddItemMaster') THEN

    CREATE UNIQUE INDEX `IX_Item_NormalisedName` ON `Item` (`NormalisedName`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902154642_P1T03_AddItemMaster') THEN

    CREATE UNIQUE INDEX `IX_ItemCategory_NormalisedName` ON `ItemCategory` (`NormalisedName`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902154642_P1T03_AddItemMaster') THEN

    CREATE UNIQUE INDEX `IX_Unit_NormalisedCode` ON `Unit` (`NormalisedCode`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902154642_P1T03_AddItemMaster') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902154642_P1T03_AddItemMaster', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902160119_P1T04_AddDepartment') THEN

    CREATE TABLE `Department` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Name` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `NormalisedName` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `IsActive` bit(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Department` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902160119_P1T04_AddDepartment') THEN

    CREATE UNIQUE INDEX `IX_Department_NormalisedName` ON `Department` (`NormalisedName`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902160119_P1T04_AddDepartment') THEN

    ALTER TABLE `Party` ADD CONSTRAINT `FK_Party_Department_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Department` (`Id`) ON DELETE RESTRICT;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902160119_P1T04_AddDepartment') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902160119_P1T04_AddDepartment', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902161515_P1T05_AddPaymentMode') THEN

    CREATE TABLE `PaymentMode` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Name` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `NormalisedName` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `RequiresAccount` bit(1) NOT NULL,
        `RequiresReference` bit(1) NOT NULL,
        `SortOrder` int NOT NULL,
        `IsActive` bit(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_PaymentMode` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902161515_P1T05_AddPaymentMode') THEN

    CREATE UNIQUE INDEX `IX_PaymentMode_NormalisedName` ON `PaymentMode` (`NormalisedName`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902161515_P1T05_AddPaymentMode') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902161515_P1T05_AddPaymentMode', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902162625_P1T06_AddAccount') THEN

    CREATE TABLE `Account` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Name` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `NormalisedName` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Type` tinyint NOT NULL,
        `BankName` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `AccountNumber` varchar(40) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Ifsc` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `OpeningBalance` decimal(18,2) NOT NULL,
        `OpeningBalanceDate` date NOT NULL,
        `IsActive` bit(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Account` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902162625_P1T06_AddAccount') THEN

    CREATE UNIQUE INDEX `IX_Account_NormalisedName` ON `Account` (`NormalisedName`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902162625_P1T06_AddAccount') THEN

    CREATE INDEX `IX_Account_Type` ON `Account` (`Type`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902162625_P1T06_AddAccount') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902162625_P1T06_AddAccount', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902165415_P1T07_AddProjectDonation') THEN

    CREATE TABLE `ProjectDonation` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `ProjectId` bigint NOT NULL,
        `Basis` tinyint NOT NULL,
        `Percentage` decimal(18,2) NULL,
        `FixedAmount` decimal(18,2) NULL,
        `ContractValueSnapshot` decimal(18,2) NOT NULL,
        `DonationAmount` decimal(18,2) NOT NULL,
        `PaidAmount` decimal(18,2) NOT NULL,
        `NeedsReview` bit(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_ProjectDonation` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ProjectDonation_Project_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Project` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902165415_P1T07_AddProjectDonation') THEN

    CREATE TABLE `ProjectDonationTemple` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `ProjectDonationId` bigint NOT NULL,
        `TempleId` bigint NOT NULL,
        `Amount` decimal(18,2) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_ProjectDonationTemple` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ProjectDonationTemple_Party_TempleId` FOREIGN KEY (`TempleId`) REFERENCES `Party` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_ProjectDonationTemple_ProjectDonation_ProjectDonationId` FOREIGN KEY (`ProjectDonationId`) REFERENCES `ProjectDonation` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902165415_P1T07_AddProjectDonation') THEN

    CREATE UNIQUE INDEX `IX_ProjectDonation_ProjectId` ON `ProjectDonation` (`ProjectId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902165415_P1T07_AddProjectDonation') THEN

    CREATE INDEX `IX_ProjectDonationTemple_ProjectDonationId` ON `ProjectDonationTemple` (`ProjectDonationId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902165415_P1T07_AddProjectDonation') THEN

    CREATE UNIQUE INDEX `IX_ProjectDonationTemple_ProjectDonationId_TempleId` ON `ProjectDonationTemple` (`ProjectDonationId`, `TempleId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902165415_P1T07_AddProjectDonation') THEN

    CREATE INDEX `IX_ProjectDonationTemple_TempleId` ON `ProjectDonationTemple` (`TempleId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902165415_P1T07_AddProjectDonation') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902165415_P1T07_AddProjectDonation', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902174723_P2T01_AddExpenseCategory') THEN

    CREATE TABLE `ExpenseCategory` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Name` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Slug` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Bucket` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `IsCost` bit(1) NOT NULL,
        `IsSystem` bit(1) NOT NULL,
        `IsActive` bit(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_ExpenseCategory` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902174723_P2T01_AddExpenseCategory') THEN

    CREATE UNIQUE INDEX `IX_ExpenseCategory_Slug` ON `ExpenseCategory` (`Slug`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902174723_P2T01_AddExpenseCategory') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902174723_P2T01_AddExpenseCategory', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902175152_P2T02_AddSettlement') THEN

    CREATE TABLE `Settlement` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Direction` tinyint NOT NULL,
        `ProjectId` bigint NOT NULL,
        `PartyId` bigint NULL,
        `IncomeType` tinyint NULL,
        `Date` date NOT NULL,
        `Amount` decimal(18,2) NOT NULL,
        `PaymentModeId` bigint NOT NULL,
        `AccountId` bigint NULL,
        `ReferenceNo` varchar(80) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Description` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Status` tinyint NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Settlement` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Settlement_Account_AccountId` FOREIGN KEY (`AccountId`) REFERENCES `Account` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Settlement_Party_PartyId` FOREIGN KEY (`PartyId`) REFERENCES `Party` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Settlement_PaymentMode_PaymentModeId` FOREIGN KEY (`PaymentModeId`) REFERENCES `PaymentMode` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Settlement_Project_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Project` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902175152_P2T02_AddSettlement') THEN

    CREATE INDEX `IX_Settlement_AccountId` ON `Settlement` (`AccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902175152_P2T02_AddSettlement') THEN

    CREATE INDEX `IX_Settlement_Direction` ON `Settlement` (`Direction`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902175152_P2T02_AddSettlement') THEN

    CREATE INDEX `IX_Settlement_PartyId_Date` ON `Settlement` (`PartyId`, `Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902175152_P2T02_AddSettlement') THEN

    CREATE INDEX `IX_Settlement_PaymentModeId` ON `Settlement` (`PaymentModeId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902175152_P2T02_AddSettlement') THEN

    CREATE INDEX `IX_Settlement_ProjectId_Date` ON `Settlement` (`ProjectId`, `Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902175152_P2T02_AddSettlement') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902175152_P2T02_AddSettlement', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902180243_P2T03_AddObligation') THEN

    ALTER TABLE `Settlement` ADD `ObligationId` bigint NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902180243_P2T03_AddObligation') THEN

    CREATE TABLE `Obligation` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Type` tinyint NOT NULL,
        `ProjectId` bigint NOT NULL,
        `PartyId` bigint NULL,
        `DepartmentId` bigint NULL,
        `Date` date NOT NULL,
        `Amount` decimal(18,2) NOT NULL,
        `EstimatedAmount` decimal(18,2) NULL,
        `Reference` varchar(80) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Description` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `CategoryId` bigint NOT NULL,
        `Status` tinyint NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Obligation` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Obligation_Department_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Department` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Obligation_Party_PartyId` FOREIGN KEY (`PartyId`) REFERENCES `Party` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Obligation_Project_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Project` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902180243_P2T03_AddObligation') THEN

    CREATE TABLE `ObligationLine` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `ObligationId` bigint NOT NULL,
        `ItemId` bigint NULL,
        `ItemName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Quantity` decimal(18,2) NOT NULL,
        `Unit` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Rate` decimal(18,2) NOT NULL,
        `TaxAmount` decimal(18,2) NOT NULL,
        `LineTotal` decimal(18,2) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_ObligationLine` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ObligationLine_Obligation_ObligationId` FOREIGN KEY (`ObligationId`) REFERENCES `Obligation` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902180243_P2T03_AddObligation') THEN

    CREATE INDEX `IX_Settlement_ObligationId` ON `Settlement` (`ObligationId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902180243_P2T03_AddObligation') THEN

    CREATE INDEX `IX_Obligation_DepartmentId` ON `Obligation` (`DepartmentId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902180243_P2T03_AddObligation') THEN

    CREATE INDEX `IX_Obligation_PartyId_Date` ON `Obligation` (`PartyId`, `Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902180243_P2T03_AddObligation') THEN

    CREATE INDEX `IX_Obligation_ProjectId_Date` ON `Obligation` (`ProjectId`, `Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902180243_P2T03_AddObligation') THEN

    CREATE INDEX `IX_Obligation_Type` ON `Obligation` (`Type`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902180243_P2T03_AddObligation') THEN

    CREATE INDEX `IX_ObligationLine_ObligationId` ON `ObligationLine` (`ObligationId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902180243_P2T03_AddObligation') THEN

    ALTER TABLE `Settlement` ADD CONSTRAINT `FK_Settlement_Obligation_ObligationId` FOREIGN KEY (`ObligationId`) REFERENCES `Obligation` (`Id`) ON DELETE RESTRICT;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902180243_P2T03_AddObligation') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902180243_P2T03_AddObligation', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902181901_P2T05_AddSettlementFrequency') THEN

    ALTER TABLE `Settlement` ADD `Frequency` tinyint NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902181901_P2T05_AddSettlementFrequency') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902181901_P2T05_AddSettlementFrequency', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902184105_P2T08_AddAttachment') THEN

    CREATE TABLE `Attachment` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `OwnerType` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `OwnerId` bigint NOT NULL,
        `OriginalFileName` varchar(260) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `StoredPath` varchar(400) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `ContentType` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `SizeBytes` bigint NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Attachment` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902184105_P2T08_AddAttachment') THEN

    CREATE INDEX `IX_Attachment_OwnerType_OwnerId` ON `Attachment` (`OwnerType`, `OwnerId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902184105_P2T08_AddAttachment') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902184105_P2T08_AddAttachment', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902193145_P3T03_AddAllocation') THEN

    ALTER TABLE `Settlement` MODIFY COLUMN `ProjectId` bigint NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902193145_P3T03_AddAllocation') THEN

    CREATE TABLE `Allocation` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `SettlementId` bigint NOT NULL,
        `ObligationId` bigint NULL,
        `ProjectId` bigint NOT NULL,
        `PartyId` bigint NOT NULL,
        `Amount` decimal(18,2) NOT NULL,
        `Method` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Allocation` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Allocation_Obligation_ObligationId` FOREIGN KEY (`ObligationId`) REFERENCES `Obligation` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Allocation_Party_PartyId` FOREIGN KEY (`PartyId`) REFERENCES `Party` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Allocation_Project_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Project` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Allocation_Settlement_SettlementId` FOREIGN KEY (`SettlementId`) REFERENCES `Settlement` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902193145_P3T03_AddAllocation') THEN

    CREATE INDEX `IX_Allocation_ObligationId` ON `Allocation` (`ObligationId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902193145_P3T03_AddAllocation') THEN

    CREATE INDEX `IX_Allocation_PartyId_ProjectId` ON `Allocation` (`PartyId`, `ProjectId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902193145_P3T03_AddAllocation') THEN

    CREATE INDEX `IX_Allocation_ProjectId` ON `Allocation` (`ProjectId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902193145_P3T03_AddAllocation') THEN

    CREATE INDEX `IX_Allocation_SettlementId` ON `Allocation` (`SettlementId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260902193145_P3T03_AddAllocation') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260902193145_P3T03_AddAllocation', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903063511_P3T05_AllocationAdvanceColumns') THEN

    ALTER TABLE `Allocation` MODIFY COLUMN `SettlementId` bigint NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903063511_P3T05_AllocationAdvanceColumns') THEN

    ALTER TABLE `Allocation` MODIFY COLUMN `ProjectId` bigint NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903063511_P3T05_AllocationAdvanceColumns') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903063511_P3T05_AllocationAdvanceColumns', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE TABLE `ImportBatch` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `AccountId` bigint NOT NULL,
        `FileName` varchar(260) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Status` tinyint NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_ImportBatch` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ImportBatch_Account_AccountId` FOREIGN KEY (`AccountId`) REFERENCES `Account` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE TABLE `BankTransaction` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `ImportBatchId` bigint NOT NULL,
        `AccountId` bigint NOT NULL,
        `ValueDate` date NOT NULL,
        `Narration` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `NormalisedNarration` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Debit` decimal(18,2) NOT NULL,
        `Credit` decimal(18,2) NOT NULL,
        `RunningBalance` decimal(18,2) NULL,
        `BankReference` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `RowHash` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `OccurrenceIndex` int NOT NULL,
        `Status` tinyint NOT NULL,
        `ExclusionReason` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `ReconciledByUserId` bigint NULL,
        `ReconciledAtUtc` datetime(6) NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_BankTransaction` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_BankTransaction_Account_AccountId` FOREIGN KEY (`AccountId`) REFERENCES `Account` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_BankTransaction_ImportBatch_ImportBatchId` FOREIGN KEY (`ImportBatchId`) REFERENCES `ImportBatch` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE TABLE `StagedBankRow` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `ImportBatchId` bigint NOT NULL,
        `SourceLineNo` int NOT NULL,
        `ValueDate` date NULL,
        `Narration` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `NormalisedNarration` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Debit` decimal(18,2) NOT NULL,
        `Credit` decimal(18,2) NOT NULL,
        `Balance` decimal(18,2) NULL,
        `BankReference` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `ParseState` tinyint NOT NULL,
        `ParseError` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `RawLine` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `DuplicateOfBankTransactionId` bigint NULL,
        `IsRemoved` bit(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_StagedBankRow` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_StagedBankRow_ImportBatch_ImportBatchId` FOREIGN KEY (`ImportBatchId`) REFERENCES `ImportBatch` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE TABLE `BankTransactionProjectHint` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `BankTransactionId` bigint NOT NULL,
        `ProjectId` bigint NOT NULL,
        `Amount` decimal(18,2) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_BankTransactionProjectHint` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_BankTransactionProjectHint_BankTransaction_BankTransactionId` FOREIGN KEY (`BankTransactionId`) REFERENCES `BankTransaction` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_BankTransactionProjectHint_Project_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Project` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE TABLE `StagedBankRowAllocation` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `StagedBankRowId` bigint NOT NULL,
        `ProjectId` bigint NOT NULL,
        `Amount` decimal(18,2) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_StagedBankRowAllocation` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_StagedBankRowAllocation_Project_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Project` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_StagedBankRowAllocation_StagedBankRow_StagedBankRowId` FOREIGN KEY (`StagedBankRowId`) REFERENCES `StagedBankRow` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE INDEX `IX_BankTransaction_AccountId_Status` ON `BankTransaction` (`AccountId`, `Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE INDEX `IX_BankTransaction_AccountId_ValueDate` ON `BankTransaction` (`AccountId`, `ValueDate`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE INDEX `IX_BankTransaction_ImportBatchId` ON `BankTransaction` (`ImportBatchId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE UNIQUE INDEX `IX_BankTransaction_RowHash` ON `BankTransaction` (`RowHash`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE INDEX `IX_BankTransactionProjectHint_BankTransactionId` ON `BankTransactionProjectHint` (`BankTransactionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE INDEX `IX_BankTransactionProjectHint_ProjectId` ON `BankTransactionProjectHint` (`ProjectId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE INDEX `IX_ImportBatch_AccountId_Status` ON `ImportBatch` (`AccountId`, `Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE INDEX `IX_StagedBankRow_ImportBatchId` ON `StagedBankRow` (`ImportBatchId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE INDEX `IX_StagedBankRowAllocation_ProjectId` ON `StagedBankRowAllocation` (`ProjectId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    CREATE INDEX `IX_StagedBankRowAllocation_StagedBankRowId` ON `StagedBankRowAllocation` (`StagedBankRowId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903090141_P4T01_AddBankImport') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903090141_P4T01_AddBankImport', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903093204_P4T02_AddBankStatementProfile') THEN

    CREATE TABLE `BankStatementProfile` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `AccountId` bigint NOT NULL,
        `Name` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `HeaderRowIndex` int NOT NULL,
        `Delimiter` varchar(1) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `DateColumn` int NOT NULL,
        `NarrationColumn` int NOT NULL,
        `ReferenceColumn` int NULL,
        `BalanceColumn` int NULL,
        `SingleAmountColumn` bit(1) NOT NULL,
        `AmountColumn` int NULL,
        `DebitColumn` int NULL,
        `CreditColumn` int NULL,
        `DebitSign` tinyint NOT NULL,
        `DateFormats` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_BankStatementProfile` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_BankStatementProfile_Account_AccountId` FOREIGN KEY (`AccountId`) REFERENCES `Account` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903093204_P4T02_AddBankStatementProfile') THEN

    CREATE INDEX `IX_BankStatementProfile_AccountId` ON `BankStatementProfile` (`AccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903093204_P4T02_AddBankStatementProfile') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903093204_P4T02_AddBankStatementProfile', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903104850_P4T04_AddReconciliation') THEN

    CREATE TABLE `InternalTransfer` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `FromTransactionId` bigint NOT NULL,
        `ToTransactionId` bigint NOT NULL,
        `FromAccountId` bigint NOT NULL,
        `ToAccountId` bigint NOT NULL,
        `Amount` decimal(18,2) NOT NULL,
        `Date` date NOT NULL,
        `UnpairedAtUtc` datetime(6) NULL,
        `UnpairedByUserId` bigint NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_InternalTransfer` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_InternalTransfer_Account_FromAccountId` FOREIGN KEY (`FromAccountId`) REFERENCES `Account` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_InternalTransfer_Account_ToAccountId` FOREIGN KEY (`ToAccountId`) REFERENCES `Account` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_InternalTransfer_BankTransaction_FromTransactionId` FOREIGN KEY (`FromTransactionId`) REFERENCES `BankTransaction` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_InternalTransfer_BankTransaction_ToTransactionId` FOREIGN KEY (`ToTransactionId`) REFERENCES `BankTransaction` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903104850_P4T04_AddReconciliation') THEN

    CREATE TABLE `PartyAlias` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `PartyId` bigint NOT NULL,
        `Alias` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `NormalisedAlias` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_PartyAlias` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_PartyAlias_Party_PartyId` FOREIGN KEY (`PartyId`) REFERENCES `Party` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903104850_P4T04_AddReconciliation') THEN

    CREATE TABLE `ReconciliationLink` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `BankTransactionId` bigint NOT NULL,
        `SettlementId` bigint NOT NULL,
        `MatchConfidence` int NOT NULL,
        `MatchMethod` tinyint NOT NULL,
        `SettlementCreatedByReconciliation` bit(1) NOT NULL,
        `UnlinkedAtUtc` datetime(6) NULL,
        `UnlinkedByUserId` bigint NULL,
        `UnlinkReason` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_ReconciliationLink` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ReconciliationLink_BankTransaction_BankTransactionId` FOREIGN KEY (`BankTransactionId`) REFERENCES `BankTransaction` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_ReconciliationLink_Settlement_SettlementId` FOREIGN KEY (`SettlementId`) REFERENCES `Settlement` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903104850_P4T04_AddReconciliation') THEN

    CREATE INDEX `IX_InternalTransfer_FromAccountId` ON `InternalTransfer` (`FromAccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903104850_P4T04_AddReconciliation') THEN

    CREATE INDEX `IX_InternalTransfer_FromTransactionId` ON `InternalTransfer` (`FromTransactionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903104850_P4T04_AddReconciliation') THEN

    CREATE INDEX `IX_InternalTransfer_ToAccountId` ON `InternalTransfer` (`ToAccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903104850_P4T04_AddReconciliation') THEN

    CREATE INDEX `IX_InternalTransfer_ToTransactionId` ON `InternalTransfer` (`ToTransactionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903104850_P4T04_AddReconciliation') THEN

    CREATE UNIQUE INDEX `IX_PartyAlias_PartyId_NormalisedAlias` ON `PartyAlias` (`PartyId`, `NormalisedAlias`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903104850_P4T04_AddReconciliation') THEN

    CREATE INDEX `IX_ReconciliationLink_BankTransactionId` ON `ReconciliationLink` (`BankTransactionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903104850_P4T04_AddReconciliation') THEN

    CREATE INDEX `IX_ReconciliationLink_SettlementId` ON `ReconciliationLink` (`SettlementId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903104850_P4T04_AddReconciliation') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903104850_P4T04_AddReconciliation', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903123656_P5T01_AddProjectBudget') THEN

    CREATE TABLE `ProjectBudgetRevision` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `ProjectId` bigint NOT NULL,
        `RevisionNumber` int NOT NULL,
        `ApproachingThresholdPercent` decimal(18,2) NOT NULL,
        `Note` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_ProjectBudgetRevision` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ProjectBudgetRevision_Project_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Project` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903123656_P5T01_AddProjectBudget') THEN

    CREATE TABLE `ProjectBudgetLine` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `RevisionId` bigint NOT NULL,
        `CategoryId` bigint NOT NULL,
        `Amount` decimal(18,2) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_ProjectBudgetLine` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ProjectBudgetLine_ProjectBudgetRevision_RevisionId` FOREIGN KEY (`RevisionId`) REFERENCES `ProjectBudgetRevision` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903123656_P5T01_AddProjectBudget') THEN

    CREATE INDEX `IX_ProjectBudgetLine_RevisionId` ON `ProjectBudgetLine` (`RevisionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903123656_P5T01_AddProjectBudget') THEN

    CREATE UNIQUE INDEX `IX_ProjectBudgetRevision_ProjectId_RevisionNumber` ON `ProjectBudgetRevision` (`ProjectId`, `RevisionNumber`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903123656_P5T01_AddProjectBudget') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903123656_P5T01_AddProjectBudget', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903132413_P6T01_AddCommonExpense') THEN

    CREATE TABLE `CommonExpense` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Type` tinyint NOT NULL,
        `SubCategory` varchar(80) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Date` date NOT NULL,
        `Amount` decimal(18,2) NOT NULL,
        `PaymentModeId` bigint NOT NULL,
        `AccountId` bigint NULL,
        `ReferenceNo` varchar(80) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Description` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Status` tinyint NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_CommonExpense` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_CommonExpense_Account_AccountId` FOREIGN KEY (`AccountId`) REFERENCES `Account` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_CommonExpense_PaymentMode_PaymentModeId` FOREIGN KEY (`PaymentModeId`) REFERENCES `PaymentMode` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903132413_P6T01_AddCommonExpense') THEN

    CREATE INDEX `IX_CommonExpense_AccountId` ON `CommonExpense` (`AccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903132413_P6T01_AddCommonExpense') THEN

    CREATE INDEX `IX_CommonExpense_PaymentModeId` ON `CommonExpense` (`PaymentModeId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903132413_P6T01_AddCommonExpense') THEN

    CREATE INDEX `IX_CommonExpense_Type_Date` ON `CommonExpense` (`Type`, `Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903132413_P6T01_AddCommonExpense') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903132413_P6T01_AddCommonExpense', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903134409_P6T02_AddCommonExpenseAllocation') THEN

    ALTER TABLE `CommonExpense` ADD `AllocationRunId` bigint NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903134409_P6T02_AddCommonExpenseAllocation') THEN

    CREATE TABLE `CommonExpenseAllocationRun` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `PeriodFrom` date NOT NULL,
        `PeriodTo` date NOT NULL,
        `Types` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Method` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `PoolAmount` decimal(18,2) NOT NULL,
        `Status` tinyint NOT NULL,
        `Note` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_CommonExpenseAllocationRun` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903134409_P6T02_AddCommonExpenseAllocation') THEN

    CREATE TABLE `CommonExpenseAllocationLine` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `RunId` bigint NOT NULL,
        `ProjectId` bigint NOT NULL,
        `Amount` decimal(18,2) NOT NULL,
        `Percent` decimal(18,2) NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_CommonExpenseAllocationLine` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_CommonExpenseAllocationLine_CommonExpenseAllocationRun_RunId` FOREIGN KEY (`RunId`) REFERENCES `CommonExpenseAllocationRun` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_CommonExpenseAllocationLine_Project_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Project` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903134409_P6T02_AddCommonExpenseAllocation') THEN

    CREATE INDEX `IX_CommonExpense_AllocationRunId` ON `CommonExpense` (`AllocationRunId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903134409_P6T02_AddCommonExpenseAllocation') THEN

    CREATE INDEX `IX_CommonExpenseAllocationLine_ProjectId` ON `CommonExpenseAllocationLine` (`ProjectId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903134409_P6T02_AddCommonExpenseAllocation') THEN

    CREATE INDEX `IX_CommonExpenseAllocationLine_RunId` ON `CommonExpenseAllocationLine` (`RunId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903134409_P6T02_AddCommonExpenseAllocation') THEN

    CREATE INDEX `IX_CommonExpenseAllocationRun_PeriodFrom_PeriodTo` ON `CommonExpenseAllocationRun` (`PeriodFrom`, `PeriodTo`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903134409_P6T02_AddCommonExpenseAllocation') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903134409_P6T02_AddCommonExpenseAllocation', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903141343_P7T01_AddLoan') THEN

    CREATE TABLE `Loan` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `ProjectId` bigint NULL,
        `LenderId` bigint NOT NULL,
        `PrincipalAmount` decimal(18,2) NOT NULL,
        `AnnualInterestRatePercent` decimal(18,2) NOT NULL,
        `StartDate` date NOT NULL,
        `TenureMonths` int NOT NULL,
        `EmiAmount` decimal(18,2) NULL,
        `EmiStartDate` date NOT NULL,
        `EmiEndDate` date NULL,
        `DisbursementAccountId` bigint NOT NULL,
        `DisbursementDate` date NOT NULL,
        `Reference` varchar(80) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Notes` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Status` tinyint NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Loan` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Loan_Account_DisbursementAccountId` FOREIGN KEY (`DisbursementAccountId`) REFERENCES `Account` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Loan_Party_LenderId` FOREIGN KEY (`LenderId`) REFERENCES `Party` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Loan_Project_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Project` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903141343_P7T01_AddLoan') THEN

    CREATE INDEX `IX_Loan_DisbursementAccountId` ON `Loan` (`DisbursementAccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903141343_P7T01_AddLoan') THEN

    CREATE INDEX `IX_Loan_LenderId` ON `Loan` (`LenderId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903141343_P7T01_AddLoan') THEN

    CREATE INDEX `IX_Loan_ProjectId` ON `Loan` (`ProjectId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903141343_P7T01_AddLoan') THEN

    CREATE INDEX `IX_Loan_Status` ON `Loan` (`Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903141343_P7T01_AddLoan') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903141343_P7T01_AddLoan', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903142347_P7T02_AddLoanEmiSchedule') THEN

    CREATE TABLE `LoanEmiInstalment` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `LoanId` bigint NOT NULL,
        `InstalmentNo` int NOT NULL,
        `DueDate` date NOT NULL,
        `OpeningPrincipal` decimal(18,2) NOT NULL,
        `EmiAmount` decimal(18,2) NOT NULL,
        `PrincipalComponent` decimal(18,2) NOT NULL,
        `InterestComponent` decimal(18,2) NOT NULL,
        `ClosingPrincipal` decimal(18,2) NOT NULL,
        `Status` tinyint NOT NULL,
        `PaidAmount` decimal(18,2) NOT NULL,
        `PaidDate` date NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_LoanEmiInstalment` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_LoanEmiInstalment_Loan_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loan` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903142347_P7T02_AddLoanEmiSchedule') THEN

    CREATE INDEX `IX_LoanEmiInstalment_DueDate` ON `LoanEmiInstalment` (`DueDate`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903142347_P7T02_AddLoanEmiSchedule') THEN

    CREATE UNIQUE INDEX `IX_LoanEmiInstalment_LoanId_InstalmentNo` ON `LoanEmiInstalment` (`LoanId`, `InstalmentNo`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903142347_P7T02_AddLoanEmiSchedule') THEN

    CREATE INDEX `IX_LoanEmiInstalment_Status` ON `LoanEmiInstalment` (`Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903142347_P7T02_AddLoanEmiSchedule') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903142347_P7T02_AddLoanEmiSchedule', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903142945_P7T03_AddLoanEmiPayment') THEN

    CREATE TABLE `LoanEmiPayment` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `LoanId` bigint NOT NULL,
        `InstalmentId` bigint NULL,
        `Date` date NOT NULL,
        `Amount` decimal(18,2) NOT NULL,
        `PrincipalPaid` decimal(18,2) NOT NULL,
        `InterestPaid` decimal(18,2) NOT NULL,
        `PaymentModeId` bigint NOT NULL,
        `AccountId` bigint NULL,
        `ReferenceNo` varchar(80) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `IsPrepayment` bit(1) NOT NULL,
        `Status` tinyint NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_LoanEmiPayment` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_LoanEmiPayment_Account_AccountId` FOREIGN KEY (`AccountId`) REFERENCES `Account` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_LoanEmiPayment_LoanEmiInstalment_InstalmentId` FOREIGN KEY (`InstalmentId`) REFERENCES `LoanEmiInstalment` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_LoanEmiPayment_Loan_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loan` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_LoanEmiPayment_PaymentMode_PaymentModeId` FOREIGN KEY (`PaymentModeId`) REFERENCES `PaymentMode` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903142945_P7T03_AddLoanEmiPayment') THEN

    CREATE INDEX `IX_LoanEmiPayment_AccountId` ON `LoanEmiPayment` (`AccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903142945_P7T03_AddLoanEmiPayment') THEN

    CREATE INDEX `IX_LoanEmiPayment_InstalmentId` ON `LoanEmiPayment` (`InstalmentId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903142945_P7T03_AddLoanEmiPayment') THEN

    CREATE INDEX `IX_LoanEmiPayment_LoanId` ON `LoanEmiPayment` (`LoanId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903142945_P7T03_AddLoanEmiPayment') THEN

    CREATE INDEX `IX_LoanEmiPayment_PaymentModeId` ON `LoanEmiPayment` (`PaymentModeId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903142945_P7T03_AddLoanEmiPayment') THEN

    CREATE INDEX `IX_LoanEmiPayment_Status` ON `LoanEmiPayment` (`Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903142945_P7T03_AddLoanEmiPayment') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903142945_P7T03_AddLoanEmiPayment', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903143352_P7T04_AddLoanAlert') THEN

    CREATE TABLE `LoanAlert` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `LoanId` bigint NOT NULL,
        `InstalmentId` bigint NOT NULL,
        `Kind` tinyint NOT NULL,
        `DueDate` date NOT NULL,
        `Amount` decimal(18,2) NOT NULL,
        `RaisedOn` date NOT NULL,
        `ResolvedOn` date NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_LoanAlert` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_LoanAlert_LoanEmiInstalment_InstalmentId` FOREIGN KEY (`InstalmentId`) REFERENCES `LoanEmiInstalment` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_LoanAlert_Loan_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loan` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903143352_P7T04_AddLoanAlert') THEN

    CREATE UNIQUE INDEX `IX_LoanAlert_InstalmentId_Kind` ON `LoanAlert` (`InstalmentId`, `Kind`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903143352_P7T04_AddLoanAlert') THEN

    CREATE INDEX `IX_LoanAlert_LoanId` ON `LoanAlert` (`LoanId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903143352_P7T04_AddLoanAlert') THEN

    CREATE INDEX `IX_LoanAlert_ResolvedOn` ON `LoanAlert` (`ResolvedOn`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903143352_P7T04_AddLoanAlert') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903143352_P7T04_AddLoanAlert', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160038_P9T01_AddNotifications') THEN

    CREATE TABLE `Notification` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Trigger` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `DedupeKey` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Title` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Body` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Severity` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `ProjectId` bigint NULL,
        `EntityType` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `EntityId` bigint NULL,
        `EmailAttempts` int NOT NULL,
        `EmailSentAtUtc` datetime(6) NULL,
        `EmailError` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_Notification` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160038_P9T01_AddNotifications') THEN

    CREATE TABLE `NotificationChannelConfig` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `RoleId` bigint NOT NULL,
        `Trigger` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Channel` tinyint NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_NotificationChannelConfig` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160038_P9T01_AddNotifications') THEN

    CREATE TABLE `NotificationMute` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `UserId` bigint NOT NULL,
        `Trigger` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_NotificationMute` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160038_P9T01_AddNotifications') THEN

    CREATE TABLE `NotificationRead` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `NotificationId` bigint NOT NULL,
        `UserId` bigint NOT NULL,
        `ReadAtUtc` datetime(6) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_NotificationRead` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_NotificationRead_Notification_NotificationId` FOREIGN KEY (`NotificationId`) REFERENCES `Notification` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160038_P9T01_AddNotifications') THEN

    CREATE UNIQUE INDEX `IX_Notification_DedupeKey` ON `Notification` (`DedupeKey`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160038_P9T01_AddNotifications') THEN

    CREATE INDEX `IX_Notification_Trigger` ON `Notification` (`Trigger`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160038_P9T01_AddNotifications') THEN

    CREATE UNIQUE INDEX `IX_NotificationChannelConfig_RoleId_Trigger` ON `NotificationChannelConfig` (`RoleId`, `Trigger`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160038_P9T01_AddNotifications') THEN

    CREATE UNIQUE INDEX `IX_NotificationMute_UserId_Trigger` ON `NotificationMute` (`UserId`, `Trigger`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160038_P9T01_AddNotifications') THEN

    CREATE UNIQUE INDEX `IX_NotificationRead_NotificationId_UserId` ON `NotificationRead` (`NotificationId`, `UserId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160038_P9T01_AddNotifications') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903160038_P9T01_AddNotifications', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160555_P9T02_PerfIndexes') THEN

    CREATE INDEX `IX_LedgerEntry_AccountId_EntryDate` ON `LedgerEntry` (`AccountId`, `EntryDate`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160555_P9T02_PerfIndexes') THEN

    CREATE INDEX `IX_LedgerEntry_CategoryId` ON `LedgerEntry` (`CategoryId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160555_P9T02_PerfIndexes') THEN

    CREATE INDEX `IX_BankTransaction_Status_ValueDate` ON `BankTransaction` (`Status`, `ValueDate`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160555_P9T02_PerfIndexes') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903160555_P9T02_PerfIndexes', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904085311_P10T01_AddPurchaseOrder') THEN

    ALTER TABLE `Obligation` ADD `PurchaseOrderId` bigint NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904085311_P10T01_AddPurchaseOrder') THEN

    CREATE TABLE `PurchaseOrder` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `PoNumber` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `VendorId` bigint NOT NULL,
        `OrderDate` date NOT NULL,
        `Status` tinyint NOT NULL,
        `InvoiceNumber` varchar(80) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `SubmittedDate` date NULL,
        `Notes` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_PurchaseOrder` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_PurchaseOrder_Party_VendorId` FOREIGN KEY (`VendorId`) REFERENCES `Party` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904085311_P10T01_AddPurchaseOrder') THEN

    CREATE TABLE `PurchaseOrderLine` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `PurchaseOrderId` bigint NOT NULL,
        `ProjectId` bigint NOT NULL,
        `ItemId` bigint NULL,
        `ItemName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Quantity` decimal(18,2) NOT NULL,
        `Unit` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `Rate` decimal(18,2) NULL,
        `TaxAmount` decimal(18,2) NULL,
        `LineTotal` decimal(18,2) NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_PurchaseOrderLine` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_PurchaseOrderLine_Project_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Project` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_PurchaseOrderLine_PurchaseOrder_PurchaseOrderId` FOREIGN KEY (`PurchaseOrderId`) REFERENCES `PurchaseOrder` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904085311_P10T01_AddPurchaseOrder') THEN

    CREATE INDEX `IX_Obligation_PurchaseOrderId` ON `Obligation` (`PurchaseOrderId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904085311_P10T01_AddPurchaseOrder') THEN

    CREATE UNIQUE INDEX `IX_PurchaseOrder_PoNumber` ON `PurchaseOrder` (`PoNumber`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904085311_P10T01_AddPurchaseOrder') THEN

    CREATE INDEX `IX_PurchaseOrder_VendorId_Status` ON `PurchaseOrder` (`VendorId`, `Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904085311_P10T01_AddPurchaseOrder') THEN

    CREATE INDEX `IX_PurchaseOrderLine_ProjectId` ON `PurchaseOrderLine` (`ProjectId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904085311_P10T01_AddPurchaseOrder') THEN

    CREATE INDEX `IX_PurchaseOrderLine_PurchaseOrderId` ON `PurchaseOrderLine` (`PurchaseOrderId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904085311_P10T01_AddPurchaseOrder') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260904085311_P10T01_AddPurchaseOrder', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904093642_P10T02_ReconciliationLinkKind') THEN

    ALTER TABLE `ReconciliationLink` MODIFY COLUMN `SettlementId` bigint NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904093642_P10T02_ReconciliationLinkKind') THEN

    ALTER TABLE `ReconciliationLink` ADD `CommonExpenseId` bigint NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904093642_P10T02_ReconciliationLinkKind') THEN

    ALTER TABLE `ReconciliationLink` ADD `Kind` tinyint NOT NULL DEFAULT 1;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904093642_P10T02_ReconciliationLinkKind') THEN

    CREATE INDEX `IX_ReconciliationLink_CommonExpenseId` ON `ReconciliationLink` (`CommonExpenseId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904093642_P10T02_ReconciliationLinkKind') THEN

    ALTER TABLE `ReconciliationLink` ADD CONSTRAINT `FK_ReconciliationLink_CommonExpense_CommonExpenseId` FOREIGN KEY (`CommonExpenseId`) REFERENCES `CommonExpense` (`Id`) ON DELETE RESTRICT;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904093642_P10T02_ReconciliationLinkKind') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260904093642_P10T02_ReconciliationLinkKind', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904100206_P10T03_AddFieldOfficerExpense') THEN

    CREATE TABLE `FieldOfficerExpense` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `FieldOfficerId` bigint NOT NULL,
        `Type` tinyint NOT NULL,
        `Date` date NOT NULL,
        `Amount` decimal(18,2) NOT NULL,
        `ReferenceNo` varchar(80) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Description` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `Status` tinyint NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_FieldOfficerExpense` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_FieldOfficerExpense_Party_FieldOfficerId` FOREIGN KEY (`FieldOfficerId`) REFERENCES `Party` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904100206_P10T03_AddFieldOfficerExpense') THEN

    CREATE INDEX `IX_FieldOfficerExpense_FieldOfficerId_Date` ON `FieldOfficerExpense` (`FieldOfficerId`, `Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904100206_P10T03_AddFieldOfficerExpense') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260904100206_P10T03_AddFieldOfficerExpense', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904111806_P10T06_AddReconciliationLinkObligation') THEN

    ALTER TABLE `ReconciliationLink` ADD `ObligationId` bigint NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904111806_P10T06_AddReconciliationLinkObligation') THEN

    CREATE INDEX `IX_ReconciliationLink_ObligationId` ON `ReconciliationLink` (`ObligationId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904111806_P10T06_AddReconciliationLinkObligation') THEN

    ALTER TABLE `ReconciliationLink` ADD CONSTRAINT `FK_ReconciliationLink_Obligation_ObligationId` FOREIGN KEY (`ObligationId`) REFERENCES `Obligation` (`Id`) ON DELETE RESTRICT;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904111806_P10T06_AddReconciliationLinkObligation') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260904111806_P10T06_AddReconciliationLinkObligation', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `StagedBankRowAllocation` MODIFY COLUMN `Amount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `StagedBankRow` MODIFY COLUMN `Debit` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `StagedBankRow` MODIFY COLUMN `Credit` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `StagedBankRow` MODIFY COLUMN `Balance` decimal(18,3) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `Settlement` MODIFY COLUMN `Amount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `PurchaseOrderLine` MODIFY COLUMN `TaxAmount` decimal(18,3) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `PurchaseOrderLine` MODIFY COLUMN `Rate` decimal(18,3) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `PurchaseOrderLine` MODIFY COLUMN `Quantity` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `PurchaseOrderLine` MODIFY COLUMN `LineTotal` decimal(18,3) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `ProjectDonationTemple` MODIFY COLUMN `Amount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `ProjectDonation` MODIFY COLUMN `Percentage` decimal(18,3) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `ProjectDonation` MODIFY COLUMN `PaidAmount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `ProjectDonation` MODIFY COLUMN `FixedAmount` decimal(18,3) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `ProjectDonation` MODIFY COLUMN `DonationAmount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `ProjectDonation` MODIFY COLUMN `ContractValueSnapshot` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `ProjectBudgetRevision` MODIFY COLUMN `ApproachingThresholdPercent` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `ProjectBudgetLine` MODIFY COLUMN `Amount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `Project` MODIFY COLUMN `ExpectedProfit` decimal(18,3) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `Project` MODIFY COLUMN `EstimatedCost` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `Project` MODIFY COLUMN `ContractValue` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `ObligationLine` MODIFY COLUMN `TaxAmount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `ObligationLine` MODIFY COLUMN `Rate` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `ObligationLine` MODIFY COLUMN `Quantity` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `ObligationLine` MODIFY COLUMN `LineTotal` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `Obligation` MODIFY COLUMN `EstimatedAmount` decimal(18,3) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `Obligation` MODIFY COLUMN `Amount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `LoanEmiPayment` MODIFY COLUMN `PrincipalPaid` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `LoanEmiPayment` MODIFY COLUMN `InterestPaid` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `LoanEmiPayment` MODIFY COLUMN `Amount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `LoanEmiInstalment` MODIFY COLUMN `PrincipalComponent` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `LoanEmiInstalment` MODIFY COLUMN `PaidAmount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `LoanEmiInstalment` MODIFY COLUMN `OpeningPrincipal` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `LoanEmiInstalment` MODIFY COLUMN `InterestComponent` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `LoanEmiInstalment` MODIFY COLUMN `EmiAmount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `LoanEmiInstalment` MODIFY COLUMN `ClosingPrincipal` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `LoanAlert` MODIFY COLUMN `Amount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `Loan` MODIFY COLUMN `PrincipalAmount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `Loan` MODIFY COLUMN `EmiAmount` decimal(18,3) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `Loan` MODIFY COLUMN `AnnualInterestRatePercent` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `LedgerEntry` MODIFY COLUMN `Debit` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `LedgerEntry` MODIFY COLUMN `Credit` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `Item` MODIFY COLUMN `TaxRate` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `Item` MODIFY COLUMN `DefaultRate` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `InternalTransfer` MODIFY COLUMN `Amount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `FieldOfficerExpense` MODIFY COLUMN `Amount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `CommonExpenseAllocationRun` MODIFY COLUMN `PoolAmount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `CommonExpenseAllocationLine` MODIFY COLUMN `Percent` decimal(18,3) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `CommonExpenseAllocationLine` MODIFY COLUMN `Amount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `CommonExpense` MODIFY COLUMN `Amount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `BankTransactionProjectHint` MODIFY COLUMN `Amount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `BankTransaction` MODIFY COLUMN `RunningBalance` decimal(18,3) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `BankTransaction` MODIFY COLUMN `Debit` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `BankTransaction` MODIFY COLUMN `Credit` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `Allocation` MODIFY COLUMN `Amount` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    ALTER TABLE `Account` MODIFY COLUMN `OpeningBalance` decimal(18,3) NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904150126_P10T10_MoneyScaleTo3Decimals') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260904150126_P10T10_MoneyScaleTo3Decimals', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904162445_P10T16_AddSystemSettings') THEN

    CREATE TABLE `SystemSettings` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `CompanyName` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        `CompanyAddress` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `CompanyGstin` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `CompanyLogoUrl` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
        `VendorOutstandingAlertLimit` decimal(18,3) NOT NULL,
        `OverdueAlertDays` int NOT NULL,
        `ProfitFloorAlertPercent` decimal(18,3) NOT NULL,
        `LoanEmiReminderDaysAhead` int NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` bigint NULL,
        `UpdatedAtUtc` datetime(6) NULL,
        `UpdatedByUserId` bigint NULL,
        `ConcurrencyStamp` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        CONSTRAINT `PK_SystemSettings` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904162445_P10T16_AddSystemSettings') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260904162445_P10T16_AddSystemSettings', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904194602_P10T20_AddSystemSettingsLogoAttachment') THEN

    ALTER TABLE `SystemSettings` ADD `CompanyLogoAttachmentId` bigint NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904194602_P10T20_AddSystemSettingsLogoAttachment') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260904194602_P10T20_AddSystemSettingsLogoAttachment', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

