-- Açıklayıcı yorum: Divisima uygulama DB kullanıcısı - EN AZ YETKİ (least privilege).
-- Uygulama yalnız veri okur/yazar; şema değiştiremez (DDL yok), tablo düşüremez, kullanıcı yönetemez.
-- DB sızsa/uygulama ele geçirilse bile hasar sınırlı kalır.

USE DivisimaDb;
GO

-- Uygulama için ayrı login + user (sa/admin ASLA uygulamada kullanılmaz)
CREATE LOGIN divisima_app WITH PASSWORD = 'CHANGE_ME_STRONG_PASSWORD';
GO
CREATE USER divisima_app FOR LOGIN divisima_app;
GO

-- Açıklayıcı yorum: Yalnız CRUD - SELECT/INSERT/UPDATE/DELETE (DELETE soft-delete için gerekli değilse kaldırılabilir)
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO divisima_app;
GO

-- Açıklayıcı yorum: EF migration'ları AYRI bir yetkili kullanıcıyla çalıştırılır (deploy zamanı), uygulama runtime'ında değil
-- Uygulama kullanıcısına ASLA verilmez: ALTER, CREATE, DROP, EXECUTE (sp_), CONTROL, db_owner

-- Açıklayıcı yorum: Tehlikeli sistem prosedürlerine erişimi engelle
DENY EXECUTE ON SCHEMA::sys TO divisima_app;
GO

-- Açıklayıcı yorum: Yedekleme/geri yükleme yetkisi yok
-- xp_cmdshell zaten kapalı olmalı (OS komut çalıştırma engeli):
-- EXEC sp_configure 'xp_cmdshell', 0; RECONFIGURE;

PRINT 'divisima_app en az yetkiyle oluşturuldu (yalnız CRUD, DDL yok)';
GO
