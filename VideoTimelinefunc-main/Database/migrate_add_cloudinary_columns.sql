-- Script cap nhat them cot Cloudinary cho bang VideoSessions
-- Chay tren SQL Server VideoTimelineDb

USE [VideoTimelineDb];
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VideoSessions' AND COLUMN_NAME = 'VideoUrl')
BEGIN
    ALTER TABLE [dbo].[VideoSessions] ADD [VideoUrl] NVARCHAR(1000) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VideoSessions' AND COLUMN_NAME = 'PublicId')
BEGIN
    ALTER TABLE [dbo].[VideoSessions] ADD [PublicId] NVARCHAR(255) NULL;
END
GO

ALTER TABLE [dbo].[VideoSessions] ALTER COLUMN [StoredFileName] NVARCHAR(255) NULL;
GO
