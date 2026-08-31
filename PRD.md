# MASTER PROMPT — USTHB STUDY PLATFORM

## 1. ROLE

You are a Senior Software Architect, Senior Full-Stack Developer, UI/UX Designer, Database Architect, DevOps Engineer, Security Engineer and QA Engineer.

Your mission is to design and implement a production-ready academic platform called:

**USTHB Study**

The platform is initially designed for USTHB students in Algeria.

The goal is to create a centralized academic resource platform where students can easily find:

* Courses
* TDs
* TPs
* Exercises
* Exams
* Tests
* Retake exams
* Solutions/corrections
* Summaries
* Other educational resources

All resources must be organized by:

```text
University
→ Faculty
→ Department
→ Specialty
→ Level
→ Academic Year
→ Semester
→ Module
→ Resource Type
→ Document
```

The platform must also include a Premium subscription system because the business model is based on paid student accounts.

---

# 2. IMPORTANT DEVELOPMENT RULES

## Rule 1 — Do not build everything at once

Work incrementally.

Before implementing each major module:

1. Analyze requirements.
2. Inspect the existing code.
3. Define the architecture.
4. Implement the feature.
5. Run tests.
6. Fix all errors.
7. Verify the UI.
8. Verify API behavior.
9. Verify database behavior.
10. Only then move to the next module.

Never skip testing.

---

# 3. FIRST TASK — ANALYZE BEFORE CODING

Before writing code, inspect the project directory.

Determine:

* Existing files
* Existing architecture
* Existing dependencies
* Existing database configuration
* Existing frontend
* Existing backend
* Existing Docker configuration
* Existing authentication
* Existing migrations
* Existing environment variables

Do not destroy an existing implementation without understanding it.

If the project is empty, initialize the project using the architecture specified below.

At the beginning, create:

```text
/docs
    architecture.md
    database.md
    api.md
    security.md
    deployment.md
    roadmap.md
```

Keep these documents updated throughout development.

---

# 4. TECHNOLOGY STACK

Use the following stack unless there is a strong technical reason to change it.

## Backend

```text
ASP.NET Core Web API
.NET 8 or newer LTS version available in the environment
Entity Framework Core
ASP.NET Core Identity
MySQL
JWT authentication
FluentValidation
Serilog
Swagger / OpenAPI
```

Use clean architecture principles.

Recommended:

```text
src/
    USTHBStudy.API
    USTHBStudy.Application
    USTHBStudy.Domain
    USTHBStudy.Infrastructure

tests/
    USTHBStudy.UnitTests
    USTHBStudy.IntegrationTests
```

---

# 5. FRONTEND

Use:

```text
Next.js
TypeScript
Tailwind CSS
```

Use a modern component architecture.

The frontend must be:

* Responsive
* Mobile-first
* Fast
* Accessible
* SEO-friendly
* Professional
* Easy to maintain

Avoid unnecessary animations.

The interface should feel like a serious modern educational platform.

---

# 6. DATABASE

Use:

```text
MySQL
```

Entity Framework Core must manage the schema using migrations.

Never manually modify the production database schema unless absolutely necessary.

Every schema change must have an EF Core migration.

---

# 7. STORAGE

Do NOT store large PDF files directly inside MySQL.

Database:

```text
Document metadata
```

Object storage:

```text
PDF
Images
Thumbnails
Previews
```

Create an abstraction:

```text
IFileStorageService
```

so that the storage provider can be changed later.

The implementation should support S3-compatible storage.

For local development, provide a local storage implementation if useful.

---

# 8. PROJECT ARCHITECTURE

Use:

```text
Domain
    ↓
Application
    ↓
Infrastructure
    ↓
API
```

The domain layer must not depend on infrastructure.

The API should not contain business logic.

Use:

* DTOs
* Services
* Interfaces
* Validators
* Repositories only where justified
* Dependency Injection

Avoid overengineering.

---

# 9. MAIN DOMAIN MODEL

Create the following entities.

## User

Use ASP.NET Core Identity.

Additional properties:

```text
FirstName
LastName
StudentId (optional)
UniversityId
FacultyId
DepartmentId
SpecialtyId
LevelId
IsPremium
PremiumExpiresAt
CreatedAt
UpdatedAt
IsActive
```

Do not require StudentId unless there is a real business need.

---

# 10. ACADEMIC STRUCTURE

Create:

```text
University
Faculty
Department
Domain
Specialty
Level
Semester
AcademicYear
Module
```

Relationships:

```text
University
    └── Faculties
          └── Departments
                └── Specialties
                      └── Levels
                            └── Semesters
                                  └── Modules
```

Do not hard-code USTHB specialties in source code.

All academic data must be managed from the admin dashboard.

---

# 11. MODULE

Module fields:

```text
Id
Name
Slug
Code
Description
Coefficient
Credits
SemesterId
SpecialtyId
IsActive
CreatedAt
UpdatedAt
```

The module page must display all resources related to it.

---

# 12. DOCUMENT SYSTEM

Create a Document entity.

Fields:

```text
Id
Title
Slug
Description
DocumentType
ModuleId
AcademicYearId
SessionId
FileStorageKey
PreviewStorageKey
FileName
FileSize
PageCount
MimeType
IsPremium
Status
Source
UploadedById
CreatedAt
UpdatedAt
ViewCount
DownloadCount
```

Document statuses:

```text
Draft
PendingReview
Published
Rejected
Archived
```

Document types:

```text
Course
TD
TP
Exam
ExamSolution
Test
TestSolution
Exercise
ExerciseSolution
Summary
Other
```

Do not store document types as arbitrary strings throughout the application.

Use an enum or normalized database table depending on the architecture.

---

# 13. EXAM SYSTEM

Exams are particularly important.

Support:

```text
Normal exam
Retake
Test
Continuous assessment
Make-up exam
Other
```

An exam can optionally have a solution/correction.

Example:

```text
Exam:
    Algorithmique
    2025
    Normal

Solution:
    Algorithmique
    2025
    Correction
```

Create relationships allowing students to easily navigate:

```text
Exam
↔ Solution
```

---

# 14. SEARCH

Search is a core feature.

The user must be able to search:

```text
course
module
exam
TD
TP
solution
specialty
academic year
```

Examples:

```text
algo examen 2025
```

```text
base de données L2
```

```text
analyse rattrapage
```

Create a dedicated search service:

```text
ISearchService
```

Start with MySQL-compatible search if sufficient.

Design the architecture so Meilisearch or another search engine can be added later.

---

# 15. SEARCH FILTERS

Support:

```text
Faculty
Department
Specialty
Level
Semester
Module
Document Type
Academic Year
Session
Free/Premium
```

Results must show:

```text
Title
Type
Module
Year
Premium status
Preview
```

---

# 16. SEO

Every public academic page must have a clean URL.

Examples:

```text
/faculties/informatics
/specialties/computer-science
/modules/algorithmics
/exams/algorithmics-l2-2025
```

Generate:

* title
* description
* canonical URL
* Open Graph metadata
* Twitter/X metadata
* structured metadata where appropriate

Do not expose private user information through SEO pages.

---

# 17. PUBLIC WEBSITE

Create:

```text
/
 /about
 /pricing
 /faculties
 /specialties
 /modules
 /exams
 /courses
 /search
 /login
 /register
 /forgot-password
 /contact
 /terms
 /privacy
```

The homepage should clearly communicate:

> Courses, TDs, TPs, exams and solutions organized for USTHB students.

Primary CTA:

```text
Explore resources
```

Secondary CTA:

```text Go Premium
```

---

# 18. HOMEPAGE

Create a modern landing page.

Sections:

### Hero

```text
All your USTHB resources in one place.
```

Subtitle:

```text
Find courses, TDs, TPs, exams and solutions organized by specialty, level and module.
```

Search bar:

```text
Search for a module, course or exam...
```

### Quick navigation

```text
📚 Courses
📐 TD
🧪 TP
📝 Exams
✅ Solutions
```

### Popular modules

Display real database data.

### Recently added

Display recently published resources.

### Why USTHB Study?

Explain:

* Organized content
* Fast search
* Exam archive
* Solutions
* Mobile access

### Premium section

Explain benefits without misleading users.

---

# 19. STUDENT REGISTRATION

Registration fields:

```text
First name
Last name
Email
Password
Confirm password
University
Faculty
Department
Specialty
Level
```

After registration, ask the student to select their academic profile.

The academic profile must determine their personalized dashboard.

---

# 20. STUDENT DASHBOARD

Create:

```text
/dashboard
```

Display:

```text
Welcome, [First Name]

Your specialty
Your level
Your semester
```

Then:

```text
My modules
Recent resources
Popular exams
Favorites
Subscription status
```

Example:

```text
My Modules

Algorithmique
Bases de données
Architecture
Systèmes
```

---

# 21. PERSONALIZATION

The platform should prioritize resources related to the student's:

```text
Specialty
Level
Semester
Modules
```

Example:

An L2 Computer Science student should see L2 resources first.

Do not completely hide other resources.

Allow:

```text
Explore all resources
```

---

# 22. PREMIUM SYSTEM

Create:

```text
SubscriptionPlan
Subscription
Payment
```

Plans must be configurable from admin.

Example:

```text
1 month
3 months
6 months
Academic year
```

Do not hard-code prices.

Admin must be able to change:

```text
Name
Duration
Price
Description
Features
IsActive
DisplayOrder
```

---

# 23. ACCESS CONTROL

Resources can be:

```text
FREE
PREMIUM
```

Free users can access free resources.

Premium users can access Premium resources.

Create a centralized authorization service:

```text
IAccessControlService
```

Do not scatter:

```text
if (user.IsPremium)
```

throughout the codebase.

---

# 24. PREMIUM EXPIRATION

When:

```text
PremiumExpiresAt < current time
```

the user must lose Premium access.

Do not rely only on the frontend.

The backend must verify subscription status.

The frontend should display:

```text
Premium active until:
31/12/2026
```

or:

```text
Premium expired
```

---

# 25. PAYMENT SYSTEM

Design payment providers using an abstraction:

```text
IPaymentProvider
```

Possible statuses:

```text
Pending
Success
Failed
Cancelled
Refunded
```

For the MVP, implement manual payment verification if necessary.

Admin must be able to:

```text
Approve payment
Reject payment
Activate subscription
Extend subscription
```

Never activate Premium solely because the frontend says payment succeeded.

---

# 26. PAYMENT RECORD

Store:

```text
PaymentId
UserId
SubscriptionId
Amount
Currency
Provider
TransactionReference
Status
CreatedAt
PaidAt
AdminNote
```

Avoid storing sensitive payment information.

---

# 27. FAVORITES

Students can favorite:

* Modules
* Documents
* Exams

Create:

```text
Favorite
```

Prevent duplicate favorites.

---

# 28. HISTORY

Track:

```text
Recently viewed documents
Recently viewed modules
Downloads
```

Do not collect unnecessary personal data.

---

# 29. DOWNLOAD SYSTEM

Downloads must go through the backend authorization layer.

Do not expose permanent public storage URLs.

Preferred architecture:

```text
Student
 ↓
API
 ↓
Check authentication
 ↓
Check Premium access
 ↓
Generate temporary signed URL
 ↓
Storage
```

Increment download statistics only when appropriate.

---

# 30. PDF VIEWER

Create a professional PDF viewer.

Features:

```text
Pagination
Zoom
Fullscreen
Search
Download
```

For Premium content:

Verify access server-side.

For free users:

Display only the permitted preview if the document is Premium.

---

# 31. DOCUMENT PREVIEW

When possible, generate:

```text
thumbnail
first-page preview
```

Do not expose the complete Premium document through an unprotected public URL.

---

# 32. ADMIN DASHBOARD

Create:

```text
/admin
```

Dashboard statistics:

```text
Total users
Active users
Premium users
Total documents
Published documents
Pending documents
Total downloads
Total views
Revenue
Active subscriptions
```

Display charts:

```text
Users over time
Subscriptions over time
Revenue over time
Downloads over time
```

---

# 33. ADMIN — USER MANAGEMENT

Create:

```text
/admin/users
```

Features:

* Search
* Filter
* View
* Edit
* Activate/deactivate
* Change role
* View subscription
* Manually extend Premium

Never allow administrators to see passwords.

---

# 34. ADMIN — ACADEMIC MANAGEMENT

Create CRUD interfaces for:

```text
Universities
Faculties
Departments
Domains
Specialties
Levels
Semesters
Academic Years
Modules
Sessions
```

Use dependent dropdowns.

Example:

```text
Faculty
 ↓
Department
 ↓
Specialty
 ↓
Level
 ↓
Semester
 ↓
Module
```

---

# 35. ADMIN — DOCUMENT MANAGEMENT

Create:

```text
/admin/documents
```

Features:

* Upload
* Edit
* Delete
* Archive
* Publish
* Reject
* Change Premium status
* Change module
* Change academic year
* Change type
* Search
* Filter

Bulk actions are desirable.

---

# 36. DOCUMENT UPLOAD

The upload form should include:

```text
Title *
Type *
Faculty *
Department
Specialty *
Level *
Semester *
Module *
Academic Year *
Session
Description
Tags
File *
Free/Premium
Source
```

Validate:

* File extension
* MIME type
* File size
* File name
* Storage path

Do not trust client-provided MIME types.

---

# 37. CONTRIBUTION SYSTEM

Allow students to submit documents.

Route:

```text
/contribute
```

Form:

```text
Title
Document type
Module
Academic year
Description
File
```

Submission status:

```text
Pending
Approved
Rejected
```

Students cannot directly publish documents.

---

# 38. MODERATION

Create:

```text
/admin/contributions
```

Admin/moderator can:

```text
Approve
Reject
Edit metadata
Archive
```

When approved, create the document or publish it according to the chosen workflow.

---

# 39. REPORTING SYSTEM

Every document should have:

```text
Report this document
```

Reasons:

```text
Wrong module
Wrong year
Unreadable
Duplicate
Incorrect information
Copyright issue
Other
```

Create:

```text
DocumentReport
```

Admin can resolve reports.

---

# 40. COPYRIGHT / CONTENT RIGHTS

This platform must not assume that every document found online can legally be redistributed or sold.

Store metadata:

```text
Source
Uploader
RightsStatus
PermissionNotes
```

Possible values:

```text
Unknown
UserProvided
Authorized
PublicDomain
Official
Restricted
```

Provide a reporting mechanism for rights complaints.

Do not build a system whose purpose is to circumvent copyright protections.

---

# 41. NOTIFICATIONS

Create a notification system.

Notification types:

```text
New document
New exam
New solution
Subscription activated
Subscription expiring
Subscription expired
Contribution approved
Contribution rejected
```

Support in-app notifications first.

Email notifications can be added as a separate provider.

---

# 42. ADMIN AUDIT LOG

Create:

```text
AuditLog
```

Track important administrative operations:

```text
User created
User suspended
Document published
Document deleted
Premium activated
Payment approved
Payment rejected
Academic data modified
```

Store:

```text
UserId
Action
EntityType
EntityId
Timestamp
Metadata
```

Do not store passwords, tokens or sensitive credentials in logs.

---

# 43. ROLE SYSTEM

Create roles:

```text
Admin
Moderator
Student
```

Permissions should be explicit.

Example:

```text
Document.View
Document.Create
Document.Update
Document.Delete
Document.Publish

User.View
User.Update
User.Suspend

Subscription.View
Subscription.Manage

AcademicData.Manage
```

---

# 44. SECURITY

Implement:

* ASP.NET Identity
* Password hashing
* JWT
* Refresh token strategy if appropriate
* Authorization policies
* Rate limiting
* Input validation
* Anti-abuse mechanisms
* Secure headers
* HTTPS
* CORS configuration
* File upload validation
* SQL injection protection through EF Core
* XSS protection
* CSRF protection where applicable
* Secure cookies where cookies are used
* Error handling without leaking stack traces

Never expose:

```text
database credentials
JWT secrets
storage credentials
payment secrets
SMTP passwords
```

in source code.

Use:

```text
appsettings.Development.json
environment variables
secret management
```

---

# 45. ERROR HANDLING

Create centralized exception handling.

Return standardized API errors:

```json
{
  "success": false,
  "message": "A human-readable message",
  "errors": []
}
```

Do not expose internal exceptions to users.

Log technical details server-side.

---

# 46. API DESIGN

Use RESTful endpoints.

Examples:

```text
GET    /api/faculties
GET    /api/specialties
GET    /api/modules
GET    /api/modules/{slug}

GET    /api/documents
GET    /api/documents/{slug}

GET    /api/search

POST   /api/auth/register
POST   /api/auth/login
POST   /api/auth/refresh

GET    /api/me
PUT    /api/me

GET    /api/favorites
POST   /api/favorites
DELETE /api/favorites/{id}

GET    /api/subscriptions/plans
POST   /api/subscriptions

POST   /api/contributions
POST   /api/reports
```

Admin:

```text
GET    /api/admin/dashboard
GET    /api/admin/users
GET    /api/admin/documents
POST   /api/admin/documents
PUT    /api/admin/documents/{id}
DELETE /api/admin/documents/{id}

GET    /api/admin/payments
POST   /api/admin/payments/{id}/approve
POST   /api/admin/payments/{id}/reject
```

Use pagination.

Never return thousands of records by default.

---

# 47. API RESPONSE FORMAT

Use a consistent response structure where appropriate:

```json
{
  "data": {},
  "message": null,
  "errors": [],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "total": 100
  }
}
```

Do not force this structure on endpoints where standard HTTP responses are more appropriate.

---

# 48. FRONTEND DESIGN

Design language:

```text
Modern
Academic
Clean
Fast
Professional
Mobile-first
```

Avoid:

* excessive gradients
* excessive animations
* clutter
* tiny fonts
* complicated navigation

Use reusable components:

```text
Button
Card
Modal
Input
Select
Badge
Table
Pagination
Tabs
Dropdown
SearchBar
DocumentCard
ModuleCard
ExamCard
```

---

# 49. RESPONSIVE DESIGN

Test:

```text
Mobile
Tablet
Laptop
Desktop
Large screen
```

The mobile experience is a priority.

The student should be able to:

```text
Search
Open PDF
Read
Download
Favorite
Subscribe
```

comfortably from a phone.

---

# 50. PWA

Prepare the application to become a Progressive Web App.

Support:

```text
Install on home screen
Responsive layout
App icons
Manifest
```

Offline document caching should NOT be implemented for Premium content unless the authorization model explicitly supports it.

---

# 51. ADMIN UX

Admin pages must prioritize productivity.

Use:

```text
Sidebar
Topbar
Search
Filters
Tables
Bulk actions
Pagination
Modals
Confirmation dialogs
Toast notifications
```

Document management should be fast even when thousands of documents exist.

---

# 52. SEED DATA

Create development seed data.

Example:

```text
USTHB
Faculty
Department
Specialty
L1/L2/L3
Semesters
Modules
Academic years
Sample documents
Sample users
```

Do not use real students' personal information.

Create demo accounts such as:

```text
admin@example.local
student@example.local
moderator@example.local
```

Use clearly fake credentials and never reuse them in production.

---

# 53. DATABASE INDEXING

Analyze indexes for:

```text
ModuleId
SpecialtyId
LevelId
SemesterId
AcademicYearId
DocumentType
Status
IsPremium
CreatedAt
Slug
```

Create composite indexes for common filtering/search patterns.

Do not create indexes blindly.

---

# 54. SLUGS

Public entities should use stable slugs.

Example:

```text
algorithmique-3
bases-de-donnees
analyse-2
```

Slugs must be unique.

Handle accented characters safely.

---

# 55. PAGINATION

All potentially large collections must support:

```text
page
pageSize
```

Example:

```text
/api/documents?page=1&pageSize=20
```

Limit maximum page size.

---

# 56. CACHING

Cache relatively static information:

```text
faculties
specialties
modules
academic structure
```

Do not cache private subscription authorization incorrectly.

Use cache invalidation when academic data changes.

---

# 57. OBSERVABILITY

Implement:

```text
Structured logging
Request logging
Error logging
Health checks
```

Create:

```text
/health
```

Check:

```text
API
Database
Storage
```

where practical.

---

# 58. DOCKER

Create Docker configuration for:

```text
Frontend
Backend
MySQL
Storage
```

Use environment variables.

Do not hard-code credentials.

Provide:

```text
docker-compose.yml
.env.example
```

Never commit:

```text
.env
```

with real secrets.

---

# 59. DEVELOPMENT ENVIRONMENT

Provide clear setup documentation:

```text
git clone
install dependencies
configure environment
run migrations
seed database
start backend
start frontend
```

Document Windows and Linux commands where practical.

---

# 60. TESTING

Create tests for:

## Unit tests

* Authentication logic
* Subscription expiration
* Access control
* Document validation
* Search logic
* Academic hierarchy

## Integration tests

* Registration
* Login
* Document access
* Premium authorization
* Download authorization
* Admin operations
* Subscription activation

---

# 61. CRITICAL SECURITY TESTS

Explicitly test:

### Free user accessing Premium document

Must return:

```text
403 Forbidden
```

or an appropriate business response.

### Unauthenticated download

Must fail.

### Student accessing admin endpoint

Must fail.

### Suspended user

Must not be able to use restricted functionality.

### Expired Premium

Must not access Premium resources.

### Manipulated frontend

The backend must remain secure even if the user modifies JavaScript requests.

---

# 62. QA WORKFLOW

After each feature:

```text
Implement
 ↓
Build
 ↓
Unit tests
 ↓
Integration tests
 ↓
Manual test
 ↓
Fix bugs
 ↓
Regression test
 ↓
Commit
```

Do not continue if the current feature is broken.

---

# 63. GIT STRATEGY

Use meaningful commits.

Examples:

```text
feat(auth): implement student authentication
feat(academic): add specialty management
feat(documents): implement document upload
feat(search): implement document search
feat(subscription): add premium plans
fix(documents): secure premium download
```

Do not create giant commits containing unrelated features.

---

# 64. DOCUMENTATION

Maintain:

```text
/docs/architecture.md
/docs/database.md
/docs/api.md
/docs/security.md
/docs/deployment.md
/docs/roadmap.md
```

Update documentation whenever architecture changes.

---

# 65. DEVELOPMENT PHASES

Follow this exact order.

## PHASE 0

Project analysis.

Deliver:

```text
Architecture
Folder structure
Technology validation
Initial documentation
```

Do not build business features yet.

---

## PHASE 1 — FOUNDATION

Implement:

```text
Solution structure
Database
EF Core
Identity
JWT
Roles
Logging
Swagger
Health checks
Docker
Environment configuration
```

Test everything.

---

## PHASE 2 — ACADEMIC STRUCTURE

Implement:

```text
University
Faculty
Department
Domain
Specialty
Level
Semester
Academic Year
Module
Session
```

Implement admin CRUD.

---

## PHASE 3 — DOCUMENTS

Implement:

```text
Document
Document types
Upload
Storage
Metadata
Status
Publishing
PDF preview
```

---

## PHASE 4 — PUBLIC WEBSITE

Implement:

```text
Homepage
Faculties
Specialties
Levels
Modules
Documents
Exams
Search
SEO
```

---

## PHASE 5 — STUDENT EXPERIENCE

Implement:

```text
Registration
Profile
Dashboard
Personalized modules
Favorites
History
Downloads
```

---

## PHASE 6 — PREMIUM

Implement:

```text
Plans
Subscriptions
Access control
Expiration
Premium UI
Payment records
Manual activation
```

---

## PHASE 7 — ADMIN

Complete:

```text
Dashboard
Users
Documents
Contributions
Reports
Subscriptions
Payments
Analytics
Audit logs
```

---

## PHASE 8 — QUALITY

Perform:

```text
Security audit
Performance testing
Responsive testing
SEO testing
Accessibility testing
API testing
Database testing
File upload testing
Authorization testing
```

Fix all critical and high-priority issues.

---

# 66. FUTURE FEATURES

Do NOT implement these in the first MVP unless the core system is stable.

Potential future modules:

```text
AI study assistant
AI document summarization
AI exercise explanation
Personal study planner
Exam preparation
Question bank
Online quizzes
Flashcards
Student community
Discussion forums
Teacher accounts
Mobile application
Multi-university support
Advanced analytics
```

Architecture should allow these features later.

---

# 67. MULTI-UNIVERSITY PREPARATION

Although the MVP targets USTHB, do not hard-code:

```text
USTHB
```

everywhere in the backend.

The platform should conceptually support:

```text
University A
University B
University C
```

later.

The initial seed data can contain USTHB.

---

# 68. BUSINESS MODEL

The system must support:

```text
Free user
Premium monthly
Premium 3 months
Premium 6 months
Premium academic year
```

Admin can create additional plans.

Never hard-code subscription prices.

---

# 69. ADMIN BUSINESS ANALYTICS

Display:

```text
Total registered users
Active users
Premium users
Conversion rate
Active subscriptions
Expired subscriptions
Revenue
Documents
Downloads
Views
Top modules
Top documents
Top specialties
```

---

# 70. PERFORMANCE

Target:

* Fast homepage
* Fast search
* Efficient pagination
* Optimized images
* Lazy loading where appropriate
* Server-side rendering for public SEO pages
* No unnecessary API requests
* Database indexes
* Efficient EF Core queries

Avoid:

```text
N+1 queries
loading entire tables
unbounded API results
large unnecessary JSON responses
```

---

# 71. ACCESSIBILITY

Implement:

```text
Semantic HTML
Keyboard navigation
Visible focus
Accessible forms
ARIA only where necessary
Readable contrast
Alt text
```

---

# 72. INTERNATIONALIZATION

The initial interface can support:

```text
French
Arabic
English
```

At minimum, architect the frontend so translations can be added later.

Do not hard-code every user-facing string inside components.

---

# 73. ARABIC / RTL

Because the target market is Algeria:

Support RTL architecture.

When Arabic is enabled:

```text
dir="rtl"
```

The UI must remain usable.

Do not simply reverse the entire layout blindly.

Test:

* Sidebar
* Tables
* Forms
* PDF controls
* Navigation
* Cards
* Modals

---

# 74. CONTENT QUALITY

The platform should prioritize structured metadata.

A document without a module, specialty or academic year should not be published unless explicitly marked as "Unclassified".

Admin should be able to correct metadata.

---

# 75. DUPLICATE DETECTION

When uploading documents, attempt to detect duplicates using:

```text
file hash
filename
module
academic year
document type
```

Do not automatically delete duplicates.

Warn the administrator.

---

# 76. FILE HASH

Generate a cryptographic hash for uploaded files.

Example:

```text
SHA-256
```

Store it in the database.

Use it for duplicate detection.

---

# 77. DOCUMENT STATISTICS

Track:

```text
Views
Downloads
Favorites
```

Use these metrics for analytics.

Avoid tracking unnecessary personally identifiable information.

---

# 78. CLEAN CODE

Follow:

```text
SOLID
DRY
KISS
Clean Architecture
Separation of concerns
```

But do not overengineer.

Prefer simple maintainable code.

---

# 79. NO FAKE FEATURES

Never create buttons that do nothing.

If a feature is not implemented:

* either do not display it;
* or clearly mark it as coming soon.

Do not simulate successful payment.

Do not fake statistics.

Do not fake API responses in production code.

---

# 80. NO PLACEHOLDER DATA IN PRODUCTION

Seed/demo data is acceptable for development.

Production must allow the administrator to populate real academic data.

---

# 81. FINAL ACCEPTANCE CRITERIA

The MVP is considered complete only when a student can:

```text
1. Open the website
2. Create an account
3. Select their specialty
4. Select their level
5. See their modules
6. Search for a document
7. Open a module
8. Open an exam
9. View a PDF
10. Download an allowed document
11. Add a document to favorites
12. See Premium content
13. Subscribe to a Premium plan
14. Have Premium activated
15. Access Premium content
16. Lose access after expiration
```

And an administrator can:

```text
1. Login
2. Manage academic structure
3. Create modules
4. Upload documents
5. Categorize documents
6. Publish documents
7. Mark documents Premium
8. Manage users
9. Manage subscriptions
10. Approve payments
11. Review contributions
12. Handle reports
13. View statistics
14. Review audit logs
```

---

# 82. IMPORTANT — DO NOT STOP AT CODE GENERATION

After implementing each phase, actually run:

```text
Build
Tests
Database migrations
Application startup
API tests
```

If an error occurs:

1. Read the complete error.
2. Identify the root cause.
3. Fix it.
4. Re-run the failing test.
5. Run regression tests.
6. Continue only after it passes.

Do not simply say:

> "This should work."

Verify it.

---

# 83. FINAL DEVELOPMENT REPORT

At the end of every major phase, provide a concise report:

```text
PHASE:
Completed:

Implemented:
- ...
- ...
- ...

Tests:
- Passed: XX
- Failed: 0

Database:
- Migration created: yes/no

Security:
- Verified: yes/no

Known issues:
- ...

Next phase:
- ...
```

---

# 84. START NOW

Start by analyzing the current project.

Do NOT immediately generate a large amount of code.

First:

1. Inspect the repository.
2. Identify the existing structure.
3. Identify what already exists.
4. Create `/docs/architecture.md`.
5. Create `/docs/roadmap.md`.
6. Propose the implementation plan.
7. Then implement PHASE 1.

After PHASE 1 is implemented:

* build the solution;
* run tests;
* fix all errors;
* verify database migrations;
* verify authentication;
* verify roles;
* verify API startup.

Then continue automatically to PHASE 2.

Do not skip phases.

The final objective is a **production-ready, secure, scalable and commercially viable academic platform**, not a prototype or a collection of disconnected pages.
