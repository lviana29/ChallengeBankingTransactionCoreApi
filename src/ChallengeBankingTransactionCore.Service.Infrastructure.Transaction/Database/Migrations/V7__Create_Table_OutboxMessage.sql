CREATE TABLE IF NOT EXISTS `OutboxMessage` (
    `Id` CHAR(36) NOT NULL,
    `EventType` VARCHAR(100) NOT NULL,
    `Payload` TEXT NOT NULL,
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `ProcessedAt` DATETIME(6) NULL,
    `RetryCount` INT NOT NULL DEFAULT 0,
    `LastError` VARCHAR(500) NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_OutboxMessage_Pending` (`ProcessedAt`, `RetryCount`, `CreatedAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
