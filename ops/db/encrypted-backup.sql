-- Açıklayıcı yorum: Şifreli yedekleme - Transparent Data Encryption (TDE) + yedek şifreleme.
-- Yedek dosyası çalınsa bile anahtarsız okunamaz (at-rest encryption).

-- 1) Master key + sertifika (bir kez, güvenli saklanmalı)
USE master;
GO
CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'CHANGE_ME_MASTER_KEY_PASSWORD';
GO
CREATE CERTIFICATE DivisimaTDECert WITH SUBJECT = 'Divisima TDE Certificate';
GO
-- ÖNEMLİ: Sertifikayı + private key'i yedekle ve GÜVENLİ sakla (kaybolursa veri kurtarılamaz!)
BACKUP CERTIFICATE DivisimaTDECert
    TO FILE = '/var/opt/mssql/backup/DivisimaTDECert.cer'
    WITH PRIVATE KEY (
        FILE = '/var/opt/mssql/backup/DivisimaTDECert.pvk',
        ENCRYPTION BY PASSWORD = 'CHANGE_ME_CERT_KEY_PASSWORD'
    );
GO

-- 2) TDE'yi veritabanında etkinleştir (at-rest şifreleme)
USE DivisimaDb;
GO
CREATE DATABASE ENCRYPTION KEY
    WITH ALGORITHM = AES_256
    ENCRYPTION BY SERVER CERTIFICATE DivisimaTDECert;
GO
ALTER DATABASE DivisimaDb SET ENCRYPTION ON;
GO

-- 3) Şifreli yedek alma (yedek dosyası da şifreli)
BACKUP DATABASE DivisimaDb
    TO DISK = '/var/opt/mssql/backup/DivisimaDb_encrypted.bak'
    WITH ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = DivisimaTDECert),
    COMPRESSION, CHECKSUM;
GO

PRINT 'TDE aktif + şifreli yedek alındı (AES-256)';
GO
