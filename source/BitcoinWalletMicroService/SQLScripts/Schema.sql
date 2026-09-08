PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Wallets (
    Id                       TEXT           NOT NULL PRIMARY KEY,
    Label                    TEXT           NOT NULL,
    Network                  TEXT           NOT NULL,
    EncryptedMnemonic        BLOB           NOT NULL,
    MnemonicFingerprint      TEXT           NOT NULL,
    AccountExtendedPublicKey TEXT           NOT NULL,
    AccountDerivationPath    TEXT           NOT NULL,
    CreatedUtc               TEXT           NOT NULL      
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_Wallets_Fingerprint
    ON Wallets (MnemonicFingerprint);

CREATE TABLE IF NOT EXISTS Derivedkeys (
    Id             INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT,
    WalletId       TEXT     NOT NULL,
    IsChange       INTEGER  NOT NULL, 
    AddressIndex   INTEGER  NOT NULL,
    DerivationPath TEXT     NOT NULL,
    PublicKeyHex   TEXT     NOT NULL,
    Address        TEXT     NOT NULL,
    CreatedUtc     TEXT     NOT NULL,
    CONSTRAINT  FK_DerivedKeys_Wallets
        FOREIGN KEY (WalletId) REFERENCES Wallets (Id) ON DELETE CASCADE
);                                                              

CREATE UNIQUE INDEX IF NOT EXISTS UX_DerivedKeys_Path
    ON DerivedKeys (WalletId, IsChange, AddressIndex);

CREATE INDEX IF NOT EXISTS IX_DerivedKeys_Address
    ON DerivedKeys (Address);

CREATE TABLE IF NOT EXISTS SentTransactions (
    Id                INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT,
    TxId              TEXT     NOT NULL,
    WalletId          TEXT     NOT NULL,
    IdempotencyKey    TEXT     NOT NULL,
    ToAddress         TEXT     NOT NULL,
    AmountSats        INTEGER  NOT NULL,
    FeeSats           INTEGER  NOT NULL,
    VirtualSizeBytes  INTEGER  NOT NULL,
    InputCount        INTEGER  NOT NULL,
    ChangeAddress     TEXT     NULL,
    ChangeSats        INTEGER  NOT NULL,
    RawTransactionHex TEXT     NOT NULL,
    BroadcastUtc      TEXT     NOT NULL,
    CONSTRAINT FK_SentTransactions_Wallets
        FOREIGN KEY (WalletId) REFERENCES Wallets (Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_SentTransactions_Indempotency
    ON SentTransactions (WalletId, IdempotencyKey);

CREATE UNIQUE INDEX IF NOT EXISTS UX_SentTransactions_TxId
    ON SentTransactions (TxId);

CREATE INDEX IF NOT EXISTS IX_SentTransactions_Wallet
    ON SentTransactions (WalletId, BroadcastUTC DESC);

CREATE TABLE IF NOT EXISTS Deposits (
    Id            INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT,
    DepositId     TEXT     NOT NULL,
    WalletId      TEXT     NOT NULL,
    Address       TEXT     NOT NULL,
    AddressIndex  INTEGER  NOT NULL,
    IsChange      INTEGER  NOT NULL,
    ExpectedSats  INTEGER  NULL,
    Label         TEXT     NULL,
    ReceivedSats  INTEGER  NOT NULL DEFAULT 0,
    Status        TEXT     NOT NULL,
    TxId          TEXT     NULL,
    CreatedUtc    TEXT     NOT NULL,
    ExpiresUtc    TEXT     NOT NULL,
    ConfirmedUtc  TEXT     NULL,
    CONSTRAINT FK_Deposits_Wallets
        FOREIGN KEY (WalletId) REFERENCES Wallets (Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_Deposits_DepositId
    ON Deposits (DepositId);

CREATE UNIQUE INDEX IF NOT EXISTS UX_Deposits_Address
    ON Deposits (Address);

CREATE INDEX IF NOT EXISTS IX_Deposits_Wallet
    ON Deposits (WalletId, CreatedUtc DESC);

CREATE INDEX IF NOT EXISTS IX_Deposits_Status
    ON Deposits (Status, ExpiresUtc);
