# Library Management System

A full-stack web application built with **ASP.NET Core Web API**, **Angular**, and **SQL Server**. Designed as a comprehensive practical implementation covering the full spectrum of C# and .NET concepts — from OOP fundamentals to async programming, file handling, delegates, and RESTful API design.

---

## Table of Contents

1. [Application Overview](#1-application-overview)
2. [Architecture & Design Decisions](#2-architecture--design-decisions)
3. [Database Design](#3-database-design)
4. [C# Concepts Implementation Map](#4-c-concepts-implementation-map)
5. [Feature Flows & Implementation](#5-feature-flows--implementation)
6. [API Reference](#6-api-reference)
7. [Authentication & Authorization](#7-authentication--authorization)
8. [File Handling Strategy](#8-file-handling-strategy)
9. [Background Jobs](#9-background-jobs)
10. [Frontend — Angular](#10-frontend--angular)
11. [Project Setup](#11-project-setup)
12. [Design Decisions & Trade-offs](#12-design-decisions--trade-offs)

---

## 1. Application Overview

The Library Management System supports two roles:

| Role | Capabilities |
|---|---|
| **Librarian** | Books CRUD, Member management, Approve/Reject issue requests, View all issued books, Manage overdue, Resolve book problems, Dashboard |
| **Member** | Browse books, Submit issue requests, View due dates, Re-issue (max 2×), Submit feedback & quotes, Report book problems |

### Business Rules

- A member cannot issue a book they currently hold
- A member cannot re-request the same book within **3 months of the final due date**
- Re-issue is allowed a maximum of **2 times** per issue cycle
- A fine of **₹5 per overdue day** is calculated on return
- Book availability is tracked via `AvailableCopies` — decremented on issue, incremented on return
- Quotes can be submitted as typed text **or** an uploaded image of a page

---

## 2. Architecture & Design Decisions

### Layered Architecture

```
┌─────────────────────────────────────────────────┐
│                Angular Frontend                  │
│         HttpClient · Guards · Interceptors       │
└──────────────────────┬──────────────────────────┘
                       │ HTTP / JSON
┌──────────────────────▼──────────────────────────┐
│              API Layer (ASP.NET Core)            │
│   Controllers · Middleware · Filters · Swagger   │
└──────────────────────┬──────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────┐
│             Application Layer                    │
│    Services · Business Rules · Events · DTOs    │
└──────────────────────┬──────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────┐
│           Infrastructure Layer                   │
│   Repositories · EF Core · File Storage · Jobs  │
└──────────────────────┬──────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────┐
│                  SQL Server                      │
│            11 Tables · Relationships             │
└─────────────────────────────────────────────────┘
```

### Why Layered Architecture?

**Separation of Concerns** — each layer has a single responsibility. Controllers know nothing about the database. Services know nothing about HTTP. This makes the codebase testable, maintainable, and extensible.

**Dependency Inversion** — every layer depends on abstractions (interfaces), not concrete implementations. This is enforced through .NET's built-in Dependency Injection container.

### Design Patterns Used

| Pattern | Where | Why |
|---|---|---|
| Repository | `Infrastructure/Repositories` | Abstracts EF Core, enables unit testing |
| Generic Repository | `BaseRepository<T>` | Avoids code duplication across entities |
| Service Layer | `Application/Services` | Centralises business logic |
| DTO Pattern | All API boundaries | Decouples DB schema from API contract |
| Strategy (via DI) | `IFileStorageService` | Swap local/cloud storage without changing business code |
| Observer (Events) | `IssueService.OnBookIssued` | Decoupled notifications on issue/return |

---

## 3. Database Design

### Entity Relationship Overview

```
Users ──────────────────────────────────────── (Auth)
  │
  └── Members (1:1)
        │
        ├── IssueRequests ──► Books
        │     │ [Approve]
        │     ▼
        ├── IssuedBooks ──► Books
        │     (ReIssueCount ≤ 2, 3-month cooldown rule)
        │
        ├── Feedbacks ──► Books
        ├── BookQuotes ──► Books
        └── BookProblems ──► Books

Books ──► Categories
Books ──► BookAuthors (junction) ──► Authors
```

### All 11 Tables

| # | Table | Purpose |
|---|---|---|
| 1 | `Users` | Authentication — stores role (Librarian/Member) |
| 2 | `Members` | Extended member profile, links to User |
| 3 | `Books` | Core book record with availability tracking |
| 4 | `Authors` | Author profiles |
| 5 | `BookAuthors` | Many-to-many junction between Books and Authors |
| 6 | `Categories` | Book genres/categories |
| 7 | `IssueRequests` | Member requests (Pending → Approved/Rejected) |
| 8 | `IssuedBooks` | Physical handover record, tracks due/return dates |
| 9 | `Feedbacks` | Member ratings and reviews per book |
| 10 | `BookQuotes` | Member-submitted text or image quotes from books |
| 11 | `BookProblems` | Damage/missing page reports by members |

### Key Columns — IssuedBooks

```sql
IssuedBooks (
    Id              INT PRIMARY KEY,
    IssueRequestId  INT FK → IssueRequests,
    BookId          INT FK → Books,
    MemberId        INT FK → Members,
    IssuedDate      DATETIME,
    DueDate         DATETIME,       -- extended on re-issue
    ReturnDate      DATETIME NULL,  -- null = not yet returned
    IsReturned      BIT,
    ReIssueCount    INT DEFAULT 0,  -- max 2
    Fine            DECIMAL(18,2)   -- calculated on return
)
```

---

## 4. C# Concepts Implementation Map

This section maps every topic from the C#/.NET syllabus to its concrete implementation in the project.

### C# Fundamentals

| Concept | Where Used | Example |
|---|---|---|
| Data Types | All entities | `int`, `string`, `decimal`, `bool`, `DateTime` throughout |
| Variables & var | Service methods | `var query = _db.Books.AsQueryable()` |
| const | `IssueService` | `const int MAX_REISSUE = 2; const decimal FINE_PER_DAY = 5m` |
| readonly | Service constructors | `private readonly AppDbContext _db` |
| Properties | All entities and DTOs | `public string Title { get; set; }` |
| Control Flow (if/else) | Business rules | Cooldown check, availability check, re-issue limit |
| Switch | Enum processing | `ProblemType` → display label mapping |
| Loops (foreach/for) | CSV export, batch jobs | Iterating issued records for export |
| break/continue | Search filtering loops | Early exit on invalid filters |
| String interpolation | Responses, logs | `$"Cooldown until {cooldownEnd:dd MMM yyyy}"` |
| Type casting | EF Core results | `(int)`, `(decimal)` in LINQ projections |
| checked/unchecked | Fine calculation | `checked { overdueDays * FINE_PER_DAY }` for overflow safety |
| Boxing/Unboxing | Avoided via generics | `List<T>` used instead of `ArrayList` — no boxing |
| Stack/Heap | Entity instances | Value types in DTOs on stack; class instances on heap |
| Static keyword | `FineCalculator`, `JwtHelper` | Utility classes with no state |
| Static vs Non-static | `BookExtensions` | Static helper methods extend instance types |
| Operators | Date arithmetic | `dueDate.AddMonths(3)`, `overdueDays * rate` |

### OOP Concepts

| Concept | Where Used |
|---|---|
| Classes & Objects | All 11 entities, all service classes |
| Constructors | Entity constructors with defaults, service constructors (DI) |
| Static constructor | `FineCalculator` initialises rate table once |
| Private constructor | `Singleton` pattern for configuration reader |
| Destructors / GC | `IDisposable` on `FileStream`, `StreamWriter` — `using` keyword |
| Access Specifiers | `public` entities, `private` fields, `internal` helpers, `protected` in base repo |
| Encapsulation | Private fields + public properties on all entities |
| Abstraction | `IRepository<T>`, `IAuthService`, `IFileStorageService` interfaces |
| Inheritance | `BaseRepository<T>` → `BookRepository`, `MemberRepository` |
| Abstract Class | `BaseService` with shared validation logic |
| Interface | `IRepository<T>`, `IAuthService`, `IEmailService`, `IIssueService` |
| Method Overloading | `ExportAsync(TextWriter)` and `ExportAsync(string filePath)` |
| Method Overriding | `BookRepository.DeleteAsync()` overrides soft-delete behaviour |
| Method Hiding | N/A — avoided intentionally (override used instead) |
| Polymorphism | `TextWriter` base accepted by `ExportIssuedAsync` → works with `StreamWriter` or `StringWriter` |
| Sealed class | `FineCalculator` sealed — utility, not designed for inheritance |
| Partial class | EF Core `AppDbContext` split across `AppDbContext.cs` and `AppDbContext.Seed.cs` |
| Extension methods | `BookExtensions.IsAvailable()`, `DateTimeExtensions.IsCooldownExpired()`, `IssuedBook.OverdueDays()` |
| Variable reference | Interface reference `IRepository<Book>` holds `BookRepository` instance |
| Generics | `IRepository<T>`, `BaseRepository<T>`, `List<T>`, `Task<T>`, `ActionResult<T>` |

### Exception Handling

| Concept | Where Used |
|---|---|
| try/catch | All service methods wrapping EF Core operations |
| Multiple catch blocks | `catch (NotFoundException)`, `catch (BusinessException)`, `catch (Exception ex)` |
| finally | File stream cleanup in upload methods |
| Inner Exception | `throw new AppException("Return failed", ex)` — wraps EF exception |
| Exception Handling Abuse | Avoided — `TryParse`, `TryGetValue`, `FirstOrDefault` used instead of catch-for-control-flow |

Custom exception hierarchy:
```
Exception
  └── AppException
        ├── NotFoundException     (404)
        ├── BusinessException     (422)
        ├── ValidationException   (400)
        └── UnauthorizedException (401)
```

### Delegates, Events & Lambdas

| Concept | Where Used |
|---|---|
| Delegate | `Action<IssuedBook>` for event handler signatures |
| Multicast delegate | `OnBookIssued` fires the confirmation queue + the audit log |
| Generic delegates | `Func<Book, bool>` for filter predicates, `Action<string>` for notification |
| Anonymous method | Inline LINQ predicates where stored reference not needed |
| Lambda expressions | All LINQ queries — `Where`, `Select`, `OrderBy`, `Any`, `All` |
| Events | `IssueService.OnBookIssued`, `IssueService.OnBookReturned` |
| Event Handler | `EmailHandler.SendConfirmation`, `LogHandler.LogIssuance` |

### Collections

| Collection | Where Used |
|---|---|
| `List<T>` | Author lists, feedback lists, search results |
| `Dictionary<K,V>` | Filter parameter mapping, error dictionary in validation |
| `HashSet<T>` | Duplicate ISBN check, allowed file extension set |
| `Queue<T>` | Email notification queue in background job |
| `IEnumerable<T>` | All LINQ query return types before materialisation |
| Arrays | Allowed file extensions `new[] { ".jpg", ".png", ".webp" }` |

### File Handling

| Class | Where Used |
|---|---|
| `FileStream` | Saving book cover images and quote images to disk |
| `StreamWriter` | Writing issued book CSV export to file |
| `StreamReader` | Reading configuration or seed data files |
| `StringWriter` | Building CSV content in memory for HTTP response |
| `StringReader` | Parsing text content during quote sanitisation |
| `TextWriter` | Base type for `ExportIssuedAsync(TextWriter writer)` — polymorphic |
| `BinaryWriter/Reader` | Storing/reading binary metadata for cover image dimensions |
| `FileInfo` | Inspecting uploaded file — extension, size before saving |
| `DirectoryInfo` | Creating upload folders if they don't exist |
| `File` (static) | `File.Exists`, `File.Delete` for cleanup operations |

### Asynchronous Programming

| Concept | Where Used |
|---|---|
| `async/await` | Every controller action and service method |
| `Task<T>` | All service method return types |
| `Task.WhenAll` | Sending due-date reminder emails in parallel |
| Continuation Tasks | Reminder sweep chaining — query → notify → drain queue |
| `ValueTask<T>` | `GetMemberStatusAsync` — frequently called, often returns cached result |
| Async streams | `IAsyncEnumerable<IssuedBook>` for large overdue report streaming |
| `CancellationToken` | All controller actions pass `CancellationToken ct` from HTTP request |

### ASP.NET Core

| Concept | Where Used |
|---|---|
| Middleware pipeline | JWT auth, global exception handler, CORS, HTTPS redirect |
| `appsettings.json` | JWT settings, connection string, file upload path, loan and fine policy |
| Dependency Injection | All services, repositories registered with correct lifetimes |
| Web API Controllers | 9 controllers covering all features |
| Routing | Attribute routing `[Route("api/[controller]")]` throughout |
| Parameter Binding | `[FromRoute]`, `[FromQuery]`, `[FromBody]`, `[FromForm]` |
| Return Types | `ActionResult<T>` on all actions |
| Request/Response formats | JSON (System.Text.Json with camelCase) |
| Media Type Formatters | JSON default; CSV for export via custom `ContentResult` |
| Web API Filters | `LoggingFilter`, `GlobalExceptionFilter`, `[Authorize]` |
| Swagger | Full documentation with JWT bearer auth support |
| Static Files Middleware | Serving uploaded cover images and quote images from `wwwroot` |

---

## 5. Feature Flows & Implementation

### Issue Request Flow

```
Member submits request
    │
    ▼
POST /api/issue-requests
    │
    ├── [Guard] Member role only
    ├── Check: member already has this book? → 422
    ├── Check: 3-month cooldown from last DueDate? → 422 with date
    ├── Check: pending request already exists? → 422
    │
    ▼
IssueRequest created (Status = "Pending")
    │
    ▼
Librarian views PATCH /api/issue-requests/{id}/approve
    │
    ├── Status → "Approved"
    ├── IssuedBook created (DueDate = Now + 14 days)
    ├── Book.AvailableCopies--
    ├── SaveChangesAsync()
    │
    ▼
OnBookIssued event fired (multicast)
    ├── EmailHandler.SendConfirmation()
    ├── LogHandler.LogIssuance()
    └── (DueDateReminderService picks it up on its next sweep)
```

### Return + Fine Calculation Flow

```
Librarian/Member triggers return
    │
PATCH /api/issued/{id}/return
    │
    ├── Load IssuedBook with Book
    ├── Check: already returned? → 422
    │
    ├── returnDate = DateTime.UtcNow
    ├── overdueDays = Max(0, (returnDate - DueDate).Days)
    ├── fine = overdueDays * FINE_PER_DAY (const = ₹5)
    │
    ├── IssuedBook.ReturnDate = returnDate
    ├── IssuedBook.IsReturned = true
    ├── IssuedBook.Fine = fine
    ├── Book.AvailableCopies++
    └── SaveChangesAsync()
```

### Re-issue Logic

```
Member requests extension
    │
PATCH /api/issued/{id}/reissue
    │
    ├── Check: ReIssueCount >= MAX_REISSUE (const = 2)? → 422
    ├── Check: already returned? → 422
    │
    ├── DueDate = DueDate + 14 days
    ├── ReIssueCount++
    └── SaveChangesAsync()
    
    Note: The 3-month cooldown is calculated from the FINAL DueDate
    (after all re-issues) — not the return date. Members cannot
    game the system by returning early to reset the cooldown.
```

---

## 6. API Reference

### Authentication
| Method | Endpoint | Role | Description |
|---|---|---|---|
| POST | `/api/auth/login` | Public | Returns JWT token |
| POST | `/api/auth/register` | Public | Member self-registration |

### Books
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/api/books` | Public | List with filters (genre, available, search) |
| GET | `/api/books/{id}` | Public | Detail with authors, avg rating, feedback |
| GET | `/api/books/{id}/member-status` | Member | Returns canRequest, cooldownUntil |
| POST | `/api/books` | Librarian | Create book with cover image |
| PUT | `/api/books/{id}` | Librarian | Update book |
| DELETE | `/api/books/{id}` | Librarian | Soft delete |
| POST | `/api/books/{id}/cover` | Librarian | Upload/replace cover image |

### Members
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/api/members` | Librarian | All members |
| GET | `/api/members/{id}` | Librarian | Profile + full history |
| POST | `/api/members` | Librarian | Add member |
| PUT | `/api/members/{id}` | Librarian | Update |
| PATCH | `/api/members/{id}/deactivate` | Librarian | Deactivate |

### Issue Requests
| Method | Endpoint | Role | Description |
|---|---|---|---|
| POST | `/api/issue-requests` | Member | Request a book |
| GET | `/api/issue-requests` | Librarian | All requests (filter: Pending) |
| GET | `/api/issue-requests/my` | Member | Own requests |
| PATCH | `/api/issue-requests/{id}/approve` | Librarian | Approve → creates IssuedBook |
| PATCH | `/api/issue-requests/{id}/reject` | Librarian | Reject with reason |

### Issued Books
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/api/issued` | Librarian | Full issued list |
| GET | `/api/issued/overdue` | Librarian | Overdue records |
| GET | `/api/issued/my` | Member | My active issues + due dates |
| PATCH | `/api/issued/{id}/return` | Librarian | Return + calculate fine |
| PATCH | `/api/issued/{id}/reissue` | Member | Extend due date (max 2×) |
| GET | `/api/issued/export` | Librarian | Download CSV |

### Feedback, Quotes, Problems
| Method | Endpoint | Role | Description |
|---|---|---|---|
| POST | `/api/feedback` | Member | Submit rating + review |
| GET | `/api/books/{id}/feedback` | Public | All feedback for book |
| POST | `/api/quotes` | Member | Post text or image quote |
| GET | `/api/books/{id}/quotes` | Public | All quotes for book |
| PATCH | `/api/quotes/{id}/like` | Member | Like a quote |
| POST | `/api/book-problems` | Member | Report damage/issue |
| GET | `/api/book-problems` | Librarian | View all reports |
| PATCH | `/api/book-problems/{id}/resolve` | Librarian | Mark resolved |

### Dashboard
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/api/dashboard` | Librarian | Stats: totals, overdue count, popular books |

### Recommendations
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/api/recommendations` | Member | Personalised suggestions with a reason for each |

### Trending
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/api/trending?days=30` | Librarian | Books, categories and authors gaining momentum |

---

## 6a. Recommendation Engine

A **content-based** recommender: a member's own borrowing history describes their taste as a weighted set of categories and authors, and every book they have not read is scored against that profile.

### Why content-based rather than collaborative filtering

Collaborative filtering ("members who borrowed this also borrowed…") is the textbook approach, but it needs a large user base before the overlap between members means anything. With a handful of members it returns an empty list — a feature that demos as nothing at all. Content-based works from the member's **very first loan**, because it only needs their own history.

The trade-off, stated honestly: content-based recommendations are narrower. They will never surface a book outside the categories and authors the member already reads. Collaborative filtering is what you would move to once the library had a real user base, and the two are usually blended in production.

### The scoring model

```
score(book) = Σ author affinity  (weight 3.0 per shared author)
            + Σ category affinity (weight 1.0 per shared category)
            + averageRating × 0.10      ← tie-break only
            + normalisedPopularity × 0.05 ← tie-break only
```

**Why authors outweigh categories 3:1.** Sharing "Fiction" with a book says very little — a third of the catalogue is Fiction. Sharing an author says a great deal. The weights encode how much information each signal actually carries.

**Ratings reshape the profile.** A book in the member's history contributes according to what they thought of it:

| Member's rating | Multiplier | Rationale |
|---|---|---|
| 4–5 | ×1.5 | They liked it — find more like this |
| 3, or unrated | ×1.0 | Neutral; borrowing alone is a weaker signal than rating |
| 1–2 | ×0.25 | They disliked it — do not recommend more of the same |

**Tie-breakers are deliberately tiny.** Rating and popularity contribute at 0.10 and 0.05 so they order books that already match the profile, rather than letting a popular book the member has no affinity for outrank a genuine match.

### Exclusions

A book is never recommended if the member has borrowed it, or already has a pending request for it. Held in a `HashSet<int>` because it is checked once per candidate — O(1) rather than O(n) on a list.

### Cold start

A member with no history has no profile to match. Rather than return an empty page, the engine falls back to normalised popularity plus average rating, and says so — the response carries `isColdStart: true` and each reason reads "Popular with other members". The UI explains that borrowing a book will start personalising the list.

### Explainability

Every recommendation carries a `reason` string built from the strongest contributing signal — "You have read 2 book(s) by R. K. Narayan" beats "More Fiction, which you have borrowed 3 time(s)" when both apply. A recommendation a member cannot understand is one they will not act on, and it makes the ranking inspectable rather than a black box. The raw `score` is returned too, so the ordering can be defended.

---

## 6b. Trending (Librarian)

A librarian-facing view of what is **gaining momentum**, grouped by book, category and author.

### Trending is not the same as popular

The dashboard already lists most-issued titles all-time. If trending repeated that, it would be a second copy of the same list. So trending compares a recent window against the **equally long window immediately before it**:

```
window          = last N days (default 30, selectable 7 / 30 / 90)
previous window = the N days before that
```

A book borrowed 4 times this month outranks one borrowed 40 times over three years but twice lately. In the seeded demo data, *Swami and Friends* has **more** issues in the window (2) than *Event Test Book* (1) yet ranks **below** it, because its demand is falling — which a popularity list could never show.

### Score

```
score = issuesInWindow          × 1.0    volume
      + (issuesInWindow − prev) × 1.5    momentum, weighted highest
      + distinctMembers         × 0.5    reach
      + requestsInWindow        × 0.75   unmet demand
```

**Momentum outweighs volume** deliberately — otherwise the ranking collapses back into the dashboard's list.

**Distinct members matter** because two members borrowing once each is broader demand than one member borrowing twice.

**Requests are counted** because a request that never became a loan is usually a book with no free copy — exactly the signal a librarian needs when deciding what to buy more of.

### Trend classification

| Direction | Condition | Meaning |
|---|---|---|
| `New` | activity now, none before | Newly in demand |
| `Rising` | now > before | Accelerating |
| `Steady` | now = before | Level |
| `Falling` | now < before | Declining |

`percentChange` is **null**, not 0 or 100, when there was no previous activity — growth from zero is not a percentage, and reporting one would be a lie. That case is what `New` exists to express.

### Authors with multiple books

One loan of a two-author book contributes to both authors. `SelectMany` flattens loans into (loan, author) pairs before grouping, so co-authored titles are not undercounted.

---

## 7. Authentication & Authorization

**Strategy:** JWT Bearer tokens with role-based claims.

```
POST /api/auth/login
Body: { "email": "...", "password": "..." }
Response: { "token": "eyJ...", "role": "Member", "name": "..." }
```

Token contains: `UserId`, `Role`, `Email`, `Name` as claims.

**Lifetimes:** Token expiry = 24 hours (configurable via `appsettings.json`).

**Angular side:** Token stored in `localStorage`. HTTP interceptor attaches `Authorization: Bearer {token}` to every request. Route guards check role before activating Librarian routes.

---

## 8. File Handling Strategy

All uploaded files are stored in `wwwroot/uploads/` and served as static files.

| Upload type | Path | Max size | Allowed types |
|---|---|---|---|
| Book cover | `wwwroot/uploads/covers/` | 2 MB | JPG, PNG, WebP |
| Quote image | `wwwroot/uploads/quotes/` | 5 MB | JPG, PNG |

**Naming:** `{Guid}.{extension}` — prevents collisions and path traversal attacks.

**File class usage:**

```csharp
// FileInfo  — inspect before saving
// FileStream — stream bytes to disk
// DirectoryInfo — create folder if not exists
// File.Delete — cleanup on record delete
// StreamWriter / StringWriter — CSV export
// TextWriter — polymorphic export (file or HTTP response)
```

---

## 9. Background Jobs

Scheduled work runs in a `BackgroundService` — the built-in .NET base class for a long-running `IHostedService`. No third-party scheduler is used.

`DueDateReminderService` wakes every 24 hours and performs one sweep:

| Step | What it does |
|---|---|
| Due soon | Finds unreturned loans due within 2 days and notifies each member |
| Overdue | Logs every loan past its due date with the fine accrued so far |
| Cooldown released | Counts cooldown windows that expired in the last 24 hours |
| Drain queue | Empties the `Queue<string>` that the issue/return events filled |

The due-soon notifications are dispatched with `Task.WhenAll`, so the sweep takes about as long as the slowest send rather than the sum of all of them.

### The DI lifetime trap

A `BackgroundService` is registered as a **singleton**; `AppDbContext` is **scoped**. Injecting the context directly fails at startup:

> *Cannot consume scoped service 'AppDbContext' from singleton 'DueDateReminderService'.*

The service therefore injects `IServiceScopeFactory` and creates a scope per iteration:

```csharp
using var scope = _scopeFactory.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
```

### Why not Hangfire

| | `BackgroundService` | Hangfire |
|---|---|---|
| Dependencies | None — built in | NuGet package + ~10 extra tables |
| Survives restart | No, in-memory timer | Yes, jobs persisted |
| Retries | Hand-written | Automatic |
| Dashboard | None | Built-in UI |
| Multi-server | Runs on every instance | Coordinated |

For a single-instance application, `BackgroundService` does the job with no dependencies and demonstrates `IHostedService`, DI scoping, `CancellationToken` and `Task.WhenAll` directly rather than hiding them behind a library. Hangfire would earn its place the moment jobs had to survive restarts or coordinate across servers.

---

## 10. Frontend — Angular

### Pages

```
/login                          Auth
/register                       Member self-register

/books                          Browse with search + filter + avg rating
/books/:id                      Book detail + feedback + quotes
/books/:id/quotes               Full quote wall
/my/requests                    My issue requests + status
/my/issued                      Active issues + due dates + reissue button
/my/feedback/add/:bookId        Submit feedback after return
/my/quotes                      My submitted quotes
/my/report-problem              Report book damage

/librarian/dashboard            Stats overview
/librarian/books                Books CRUD + image upload
/librarian/members              Members CRUD
/librarian/requests             Pending requests → approve/reject
/librarian/issued               Full issued list + return action
/librarian/overdue              Overdue with fine summary
/librarian/problems             Damage reports management
```

### Key Angular Concepts Used

- `HttpClient` for all API calls
- `HttpInterceptor` to attach JWT token to every request
- `CanActivate` guards for Librarian vs Member route protection
- `ReactiveFormsModule` for all forms with validation
- `AsyncPipe` for observable binding in templates

---

## 11. Project Setup

### Prerequisites

- .NET 8 SDK
- SQL Server (LocalDB or full)
- Node.js 18+
- Angular CLI 17+

### Backend

```bash
cd BackendAPI
dotnet restore
dotnet tool install --global dotnet-ef   # once, if not already installed
# Update the connection string in appsettings.json to point at your SQL Server
dotnet ef database update                # creates LibraryDb and all 11 tables
dotnet watch run
# API available at http://localhost:5041
# Swagger UI at http://localhost:5041/swagger
```

On first run a default Librarian account is seeded from the `Seed` section of `appsettings.json` — `librarian@library.local` / `Librarian#123`. Change it before any real use.

> **Note:** `dotnet watch run` holds a lock on `bin\Debug\net8.0\BackendAPI.exe`. Stop it before running any `dotnet ef` command, or the build fails with *"The process cannot access the file..."*.

### Frontend

```bash
cd FrontendApp
npm install
ng serve
# Angular app at http://localhost:4200
```

Run both at once, in two terminals. The API must be running before the Angular app can load anything.

### A note on HTTP vs HTTPS in development

The API is called over **HTTP** (port 5041), not HTTPS, during development. The HTTPS endpoint uses the .NET developer certificate, which browsers accept after `dotnet dev-certs https --trust` but Node does not — Node ships its own CA bundle and ignores the Windows certificate store. Because the Angular app is configured for SSR, using HTTPS would break server-side rendering. Correspondingly, `Program.cs` applies `UseHttpsRedirection()` only outside the Development environment.

### Environment Configuration (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "Default": "Server=.;Database=LibraryDb;Trusted_Connection=True;"
  },
  "Jwt": {
    "Key": "your-256-bit-secret-key-here",
    "Issuer": "LibraryAPI",
    "Audience": "LibraryClient",
    "ExpiryHours": 24
  },
  "FileStorage": {
    "UploadPath": "wwwroot/uploads",
    "MaxCoverSizeMB": 2,
    "MaxQuoteImageSizeMB": 5
  },
  "Fine": {
    "RatePerDay": 5.00
  }
}
```

---

## 12. Design Decisions & Trade-offs

### DTO Pattern — why not return entities directly

Entities expose all DB columns including sensitive ones (`PasswordHash`). DTOs let us shape the response independently of the schema — `FullName` computed from `FirstName + LastName`, `AvgRating` computed from Feedbacks. Breaking DB schema changes don't become breaking API changes.

### Interface-based services over concrete injection

Every service is registered and injected via its interface (`IIssueService`, not `IssueService`). This allows unit testing with mock implementations and makes swapping implementations (e.g., switching from local file storage to Azure Blob) a one-line change in `Program.cs`.

### Cooldown from DueDate, not ReturnDate

The 3-month cooldown is calculated from the **final DueDate** (after any re-issues), not the actual return date. This prevents a member from returning a book a day early and immediately re-requesting it. The cooldown is a demand management mechanism, not a penalty.

### Events for post-issue notifications

Rather than calling `emailService.Send()` directly inside `IssueService.ApproveRequestAsync()`, the service fires an `OnBookIssued` event. Email, logging, and job scheduling subscribe to it independently. Adding a new notification channel (e.g., WhatsApp) requires zero changes to `IssueService` — a new subscriber is registered at startup.

### TextWriter polymorphism for CSV export

The `ExportIssuedAsync(TextWriter writer)` method accepts a `TextWriter` base type. This means the same method can write to a `StreamWriter` (disk file), a `StringWriter` (in-memory for HTTP response), or `Console.Out` (for debugging). No duplication, and the output destination is decided by the caller.

### Soft delete on books

Books are not hard-deleted — `IsActive = false` is set instead. This preserves historical `IssuedBook` and `Feedback` records that reference the book. A physical delete would orphan those records or require cascading deletes that destroy audit history.

---

*Built as a practical .NET learning project — every C# concept from OOP to async programming, file handling, delegates, and ASP.NET Core middleware is intentionally represented in a real working feature.*
