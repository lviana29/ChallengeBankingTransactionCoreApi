CREATE TABLE IF NOT EXISTS `DailyConsolidatedBalance` (
    `Id` CHAR(36) NOT NULL,
    `AccountId` CHAR(36) NOT NULL,
    `AccountNumber` VARCHAR(20) NOT NULL,
    `BalanceDate` DATE NOT NULL,
    `TotalCredits` DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    `TotalDebits` DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    `ClosingBalance` DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    `TransactionCount` INT NOT NULL DEFAULT 0,
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `UQ_DailyBalance_Account_Date` (`AccountId`, `BalanceDate`),
    INDEX `IX_DailyBalance_AccountNumber_Date` (`AccountNumber`, `BalanceDate`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `ProcessedEvent` (
    `EventId` CHAR(36) NOT NULL,
    `EventType` VARCHAR(100) NOT NULL,
    `ProcessedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`EventId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
