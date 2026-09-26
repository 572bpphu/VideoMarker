-- Script tao Database va du lieu mau cho VideoTimelineDb
-- Chay script nay tren bat ky phien ban SQL Server nao (2012, 2016, 2017, 2019, 2022 hoac LocalDB)

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'VideoTimelineDb')
BEGIN
    CREATE DATABASE [VideoTimelineDb];
END
GO

USE [VideoTimelineDb];
GO

-- 1. Tao bang VideoSessions
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[VideoSessions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[VideoSessions] (
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [Title] [nvarchar](255) NOT NULL,
        [OriginalFileName] [nvarchar](255) NOT NULL,
        [StoredFileName] [nvarchar](255) NOT NULL,
        [Duration] [float] NOT NULL,
        [FileSize] [bigint] NOT NULL,
        [UploadedAt] [datetime] NOT NULL,
        CONSTRAINT [PK_dbo.VideoSessions] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- 2. Tao bang Segments
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Segments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Segments] (
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [VideoSessionId] [int] NOT NULL,
        [Label] [nvarchar](100) NULL,
        [StartTime] [float] NOT NULL,
        [EndTime] [float] NOT NULL,
        [Type] [nvarchar](20) NULL,
        [Color] [nvarchar](20) NULL,
        [CreatedAt] [datetime] NOT NULL,
        CONSTRAINT [PK_dbo.Segments] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_dbo.Segments_dbo.VideoSessions_VideoSessionId] FOREIGN KEY([VideoSessionId])
            REFERENCES [dbo].[VideoSessions] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_VideoSessionId] ON [dbo].[Segments]([VideoSessionId] ASC);
END
GO
