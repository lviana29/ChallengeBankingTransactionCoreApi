CREATE TABLE `Transaction`(
    `Id` CHAR(36) NOT NULL,
    `Description` VARCHAR(150) NOT NULL,
    `AccountId` CHAR(36) NOT NULL,
    `IdempotencyKey` CHAR(36) NOT NULL,
    `Amount` DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    `BalanceAfter` DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    `TransactionTypeId` CHAR(36) NOT NULL,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT `PK_Transaction` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Transaction_TransactionType_TransactionTypeId` FOREIGN KEY (`TransactionTypeId`) REFERENCES `TransactionType` (`Id`),
    CONSTRAINT `FK_Transaction_Account_AccountId` FOREIGN KEY (`AccountId`) REFERENCES `Account` (`Id`)
) ENGINE = InnoDB;
