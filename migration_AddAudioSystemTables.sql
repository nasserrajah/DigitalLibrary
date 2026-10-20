CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;

CREATE TABLE "AspNetRoles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetRoles" PRIMARY KEY,
    "Name" TEXT NULL,
    "NormalizedName" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL
);

CREATE TABLE "AspNetUsers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetUsers" PRIMARY KEY,
    "FullName" TEXT NOT NULL,
    "ProfileImage" TEXT NULL,
    "Provider" INTEGER NOT NULL,
    "ExternalProviderId" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    "IsActive" INTEGER NOT NULL,
    "StorageUsed" INTEGER NOT NULL,
    "StorageLimit" INTEGER NOT NULL,
    "UserName" TEXT NULL,
    "NormalizedUserName" TEXT NULL,
    "Email" TEXT NULL,
    "NormalizedEmail" TEXT NULL,
    "EmailConfirmed" INTEGER NOT NULL,
    "PasswordHash" TEXT NULL,
    "SecurityStamp" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL,
    "PhoneNumber" TEXT NULL,
    "PhoneNumberConfirmed" INTEGER NOT NULL,
    "TwoFactorEnabled" INTEGER NOT NULL,
    "LockoutEnd" TEXT NULL,
    "LockoutEnabled" INTEGER NOT NULL,
    "AccessFailedCount" INTEGER NOT NULL
);

CREATE TABLE "Categories" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Categories" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "Image" TEXT NULL,
    "IsActive" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL
);

CREATE TABLE "AspNetRoleClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY AUTOINCREMENT,
    "RoleId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserLogins" (
    "LoginProvider" TEXT NOT NULL,
    "ProviderKey" TEXT NOT NULL,
    "ProviderDisplayName" TEXT NULL,
    "UserId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
    CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserRoles" (
    "UserId" TEXT NOT NULL,
    "RoleId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserTokens" (
    "UserId" TEXT NOT NULL,
    "LoginProvider" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Value" TEXT NULL,
    CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
    CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Notifications" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Notifications" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "Type" INTEGER NOT NULL,
    "IsRead" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "ReadAt" TEXT NULL,
    "RelatedBookId" TEXT NULL,
    CONSTRAINT "FK_Notifications_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "RefreshTokens" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_RefreshTokens" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "Token" TEXT NOT NULL,
    "ExpiresAt" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "RevokedAt" TEXT NULL,
    "ReplacedByToken" TEXT NULL,
    CONSTRAINT "FK_RefreshTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Books" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Books" PRIMARY KEY,
    "Title" TEXT NOT NULL,
    "Description" TEXT NULL,
    "Author" TEXT NOT NULL,
    "Publisher" TEXT NULL,
    "PublicationDate" TEXT NULL,
    "ISBN" TEXT NULL,
    "Language" TEXT NOT NULL,
    "CoverImage" TEXT NULL,
    "FileName" TEXT NULL,
    "FilePath" TEXT NULL,
    "FileSize" INTEGER NOT NULL,
    "Format" INTEGER NOT NULL,
    "IsPublished" INTEGER NOT NULL,
    "CreatedBy" TEXT NULL,
    "CategoryId" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    CONSTRAINT "FK_Books_Categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Favorites" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Favorites" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "BookId" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Favorites_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Favorites_Books_BookId" FOREIGN KEY ("BookId") REFERENCES "Books" ("Id") ON DELETE CASCADE
);

CREATE TABLE "UserLibrary" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_UserLibrary" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "BookId" TEXT NOT NULL,
    "FileSize" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "IsImported" INTEGER NOT NULL,
    "LocalStorageKey" TEXT NULL,
    "OriginalFileName" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    CONSTRAINT "FK_UserLibrary_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_UserLibrary_Books_BookId" FOREIGN KEY ("BookId") REFERENCES "Books" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");

CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");

CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");

CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");

CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");

CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");

CREATE UNIQUE INDEX "IX_AspNetUsers_Email" ON "AspNetUsers" ("Email");

CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");

CREATE INDEX "IX_Books_Author" ON "Books" ("Author");

CREATE INDEX "IX_Books_CategoryId" ON "Books" ("CategoryId");

CREATE UNIQUE INDEX "IX_Books_ISBN" ON "Books" ("ISBN") WHERE [ISBN] IS NOT NULL;

CREATE INDEX "IX_Books_Title" ON "Books" ("Title");

CREATE UNIQUE INDEX "IX_Categories_Name" ON "Categories" ("Name");

CREATE INDEX "IX_Favorites_BookId" ON "Favorites" ("BookId");

CREATE UNIQUE INDEX "IX_Favorites_UserId_BookId" ON "Favorites" ("UserId", "BookId");

CREATE INDEX "IX_Notifications_UserId_IsRead" ON "Notifications" ("UserId", "IsRead");

CREATE UNIQUE INDEX "IX_RefreshTokens_Token" ON "RefreshTokens" ("Token");

CREATE INDEX "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");

CREATE INDEX "IX_UserLibrary_BookId" ON "UserLibrary" ("BookId");

CREATE UNIQUE INDEX "IX_UserLibrary_UserId_BookId" ON "UserLibrary" ("UserId", "BookId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260929230150_InitialCreate', '8.0.10');

COMMIT;

BEGIN TRANSACTION;

ALTER TABLE "Books" ADD "DownloadUrl" TEXT NULL;

ALTER TABLE "Books" ADD "GutenbergId" INTEGER NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015144417_AddGutenbergFields', '8.0.10');

COMMIT;

BEGIN TRANSACTION;

CREATE TABLE "AudioSegments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AudioSegments" PRIMARY KEY,
    "BookId" TEXT NOT NULL,
    "PageNumber" INTEGER NOT NULL,
    "SegmentIndex" INTEGER NOT NULL,
    "Text" TEXT NOT NULL,
    "Language" TEXT NOT NULL,
    "VoiceName" TEXT NULL,
    "AudioFileName" TEXT NULL,
    "DurationSeconds" REAL NULL,
    "Status" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    CONSTRAINT "FK_AudioSegments_Books_BookId" FOREIGN KEY ("BookId") REFERENCES "Books" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ListeningProgresses" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ListeningProgresses" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "BookId" TEXT NOT NULL,
    "LastSegmentIndex" INTEGER NOT NULL,
    "LastPositionSeconds" REAL NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    CONSTRAINT "FK_ListeningProgresses_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ListeningProgresses_Books_BookId" FOREIGN KEY ("BookId") REFERENCES "Books" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_AudioSegments_BookId_SegmentIndex" ON "AudioSegments" ("BookId", "SegmentIndex");

CREATE INDEX "IX_ListeningProgresses_BookId" ON "ListeningProgresses" ("BookId");

CREATE UNIQUE INDEX "IX_ListeningProgresses_UserId_BookId" ON "ListeningProgresses" ("UserId", "BookId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261020141414_AddAudioSystemTables', '8.0.10');

COMMIT;

