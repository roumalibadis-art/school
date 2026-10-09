-- A minimal "production-like" dataset created BEFORE the CommunityClassification migration:
-- one full academic branch and two documents classified by staff, one of them published.
SET @now = UTC_TIMESTAMP(6);
INSERT INTO Universities (Id, Country, CreatedAt, UpdatedAt, Name, Slug, IsActive, IsDeleted)
  VALUES ('00000000-0000-0000-0000-000000000001', 'Algeria', @now, @now, 'Legacy University', 'legacy-university', 1, 0);
INSERT INTO Faculties (Id, UniversityId, CreatedAt, UpdatedAt, Name, Slug, IsActive, IsDeleted)
  VALUES ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', @now, @now, 'Legacy Faculty', 'legacy-faculty', 1, 0);
INSERT INTO Departments (Id, FacultyId, CreatedAt, UpdatedAt, Name, Slug, IsActive, IsDeleted)
  VALUES ('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000002', @now, @now, 'Legacy Department', 'legacy-department', 1, 0);
INSERT INTO Specialties (Id, DepartmentId, CreatedAt, UpdatedAt, Name, Slug, IsActive, IsDeleted)
  VALUES ('00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000003', @now, @now, 'Legacy Specialty', 'legacy-specialty', 1, 0);
INSERT INTO Levels (Id, ShortName, Cycle, `Order`, SpecialtyId, CreatedAt, UpdatedAt, Name, Slug, IsActive, IsDeleted)
  VALUES ('00000000-0000-0000-0000-000000000005', 'L1', 1, 1, '00000000-0000-0000-0000-000000000004', @now, @now, 'Legacy L1', 'legacy-l1', 1, 0);
INSERT INTO Semesters (Id, ShortName, `Order`, LevelId, CreatedAt, UpdatedAt, Name, Slug, IsActive, IsDeleted)
  VALUES ('00000000-0000-0000-0000-000000000006', 'S1', 1, '00000000-0000-0000-0000-000000000005', @now, @now, 'Legacy S1', 'legacy-s1', 1, 0);
INSERT INTO Modules (Id, Coefficient, Credits, SemesterId, SpecialtyId, CreatedAt, UpdatedAt, Name, Slug, IsActive, IsDeleted)
  VALUES ('00000000-0000-0000-0000-000000000007', 2.0, 6, '00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000004', @now, @now, 'Legacy Module', 'legacy-module', 1, 0);
INSERT INTO Documents (Id, Title, Slug, Type, Status, ModuleId, FileStorageKey, FileName, FileSize, MimeType, FileHashSha256, IsPremium, RightsStatus, ViewCount, DownloadCount, IsDeleted, CreatedAt, UpdatedAt)
  VALUES ('00000000-0000-0000-0000-0000000000d1', 'Legacy published exam', 'legacy-published-exam', 4, 3, '00000000-0000-0000-0000-000000000007', 'k/1.pdf', '1.pdf', 100, 'application/pdf', 'AA', 0, 0, 12, 3, 0, @now, @now),
         ('00000000-0000-0000-0000-0000000000d2', 'Legacy draft course', 'legacy-draft-course', 1, 1, '00000000-0000-0000-0000-000000000007', 'k/2.pdf', '2.pdf', 200, 'application/pdf', 'BB', 1, 0, 0, 0, 0, @now, @now);
