# Build Tutorial & Viva Reference

A step-by-step walkthrough of how this project is built, written to be **read before a viva**. Every step says *what* to do, *why* it works that way, and *what breaks* if you do it differently.

`README.md` is the specification — what the finished system does.
This file is the explanation — how it gets built and why each decision was made.

**Environment this was built against:** .NET 8 · EF Core 8.0.28 · SQL Server instance `LP703` · Angular 20.3 (zoneless, SSR) · API on `http://localhost:5041` · Angular on `http://localhost:4200`

---

## Table of Contents

- [Part 0 — The big picture](#part-0--the-big-picture)
- [Part 1 — Creating the two projects](#part-1--creating-the-two-projects)
- [Part 2 — Running both together](#part-2--running-both-together)
- [Part 3 — Connecting Angular to .NET](#part-3--connecting-angular-to-net)
- [Part 4 — SQL Server and EF Core](#part-4--sql-server-and-ef-core)
- [Part 5 — The layered architecture](#part-5--the-layered-architecture)
- [Part 6 — Authentication with JWT](#part-6--authentication-with-jwt)
- [Part 7 — File handling](#part-7--file-handling)
- [Part 8 — Scheduled work without Hangfire](#part-8--scheduled-work-without-hangfire)
- [Part 9 — Viva question bank](#part-9--viva-question-bank)
- [Part 10 — Command reference](#part-10--command-reference)
- [Part 11 — Errors we actually hit](#part-11--errors-we-actually-hit)

---

## Part 0 — The big picture

### Two programs, not one

The single most important thing to be clear about: **this is two separate applications** that happen to talk to each other over HTTP.

```
  Browser (localhost:4200)              Server (localhost:5041)
  ┌──────────────────────┐              ┌──────────────────────┐
  │  Angular app         │   HTTP/JSON  │  ASP.NET Core API    │
  │  TypeScript          │ ───────────► │  C#                  │
  │  runs in the browser │ ◄─────────── │  runs on the machine │
  └──────────────────────┘              └──────────┬───────────┘
                                                   │ SQL (TDS)
                                        ┌──────────▼───────────┐
                                        │  SQL Server LP703    │
                                        │  LibraryDb           │
                                        └──────────────────────┘
```

Angular never touches SQL Server. It has no connection string, no credentials, no database driver. It only knows how to make HTTP requests. Everything about the database lives behind the API.

**Why that separation matters:** if the browser could reach the database directly, every user would need database credentials, and anyone could read every table. The API is the gatekeeper — it decides what a Member is allowed to see versus a Librarian.

### The life of one request

Trace `GET /api/books` end to end. You will likely be asked this:

| # | Where | What happens |
|---|---|---|
| 1 | Angular component | Calls `bookService.getBooks()` |
| 2 | Angular `HttpClient` | Builds an HTTP GET to `http://localhost:5041/api/books` |
| 3 | Browser | Checks CORS — different port means cross-origin |
| 4 | ASP.NET Core middleware | Request passes through CORS → auth → routing |
| 5 | `BooksController` | Action method runs, model-binds query params |
| 6 | `BookService` | Applies business rules |
| 7 | `BookRepository` | Builds a LINQ query |
| 8 | EF Core | Translates LINQ into T-SQL |
| 9 | SQL Server | Executes, returns rows |
| 10 | EF Core | Materialises rows into `Book` objects |
| 11 | `BookService` | Maps entities → `BookDto` |
| 12 | ASP.NET Core | Serialises DTOs to JSON (camelCase) |
| 13 | Angular | `Observable` emits, component renders |

Every layer in `README.md`'s architecture diagram is one row in that table.

---

## Part 1 — Creating the two projects

```bash
# Backend
dotnet new webapi -n BackendAPI

# Frontend
ng new FrontendApp
```

### What `dotnet new webapi` gives you

- `Program.cs` — the entire startup. Since .NET 6 there is **no `Startup.cs`**; services and middleware are configured in one file using top-level statements.
- `BackendAPI.csproj` — the project file. `<Project Sdk="Microsoft.NET.Sdk.Web">` is what makes it a web app rather than a console app.
- `appsettings.json` / `appsettings.Development.json` — configuration, environment-layered.
- `Properties/launchSettings.json` — **local development only**. Never deployed. It defines the profiles you can run.
- A sample `/weatherforecast` minimal-API endpoint.

### Two things in the generated project worth understanding

**`<Nullable>enable</Nullable>` in the csproj.** This turns on nullable reference types. `string` means "never null" and `string?` means "can be null", and the compiler warns you when you violate that. This matters more than it looks — EF Core reads this to decide whether a column is `NOT NULL`. It is why entity properties are written `public string Title { get; set; } = string.Empty;` (give it a non-null default) and navigation properties are written `public Book Book { get; set; } = null!;` (the `!` means "trust me, EF fills this in").

**`<ImplicitUsings>enable</ImplicitUsings>`.** Common namespaces (`System`, `System.Linq`, `System.Collections.Generic`, and for web projects `Microsoft.AspNetCore.Http`) are imported automatically. That is why files have far fewer `using` lines than older tutorials show.

### launchSettings.json and the hosting model

```json
"http":        { "commandName": "Project",    "applicationUrl": "http://localhost:5041" },
"https":       { "commandName": "Project",    "applicationUrl": "https://localhost:7270;http://localhost:5041" },
"IIS Express": { "commandName": "IISExpress" }
```

`commandName: "Project"` runs **Kestrel** — .NET's own cross-platform web server — as a standalone process. `commandName: "IISExpress"` hosts it inside IIS Express instead.

This is where **InProcess vs OutOfProcess** comes in, and it is a common viva question:

- **InProcess** (the default since ASP.NET Core 3.0) — the app is loaded *inside* the IIS worker process. Kestrel is replaced by `IISHttpServer`. One process, no network hop, faster.
- **OutOfProcess** — IIS acts as a reverse proxy to a separate `dotnet.exe` running Kestrel on a random port. Extra hop.

You set it with `<AspNetCoreHostingModel>` in the csproj. Ours is not set, so it defaults to InProcess — you can see `hostingModel="inprocess"` in the `web.config` that `dotnet publish` generates.

**But the catch:** this setting is only consulted when IIS hosts the app. We run the `http` profile, which is Kestrel standalone, so the hosting model does not apply to us at all.

### What `ng new` gives you

Angular 20 scaffolds differently from older tutorials in two ways that will trip you up:

**1. Standalone components, no NgModule.** There is no `app.module.ts`. Components declare their own `imports` array, and application-wide providers live in `app.config.ts`.

**2. Zoneless change detection.** Check `package.json` — there is **no `zone.js` dependency**, and `angular.json` has no `polyfills` entry. Historically Angular used Zone.js to monkey-patch every async API so it could know when to re-render. This project does not. Change detection is driven by **signals** instead.

Consequence: calling `provideZoneChangeDetection()` anywhere throws `NG0908: In this configuration Angular requires Zone.js` and breaks the build. Use `provideZonelessChangeDetection()`.

---

## Part 2 — Running both together

You need **two terminals**, both running at the same time:

```bash
# Terminal 1
cd BackendAPI
dotnet watch run          # rebuilds and restarts on file save

# Terminal 2
cd FrontendApp
ng serve                  # rebuilds and reloads on file save
```

| | URL |
|---|---|
| API | http://localhost:5041 |
| Swagger UI | http://localhost:5041/swagger |
| Angular | http://localhost:4200 |

**Check the API alone first.** Open Swagger before you touch Angular. If the endpoint does not work in Swagger, the problem is not Angular, and debugging in the browser will waste your time.

> **Gotcha:** `dotnet watch run` holds a lock on `bin\Debug\net8.0\BackendAPI.exe`. Any command that needs to build — including `dotnet ef migrations add` — will fail with *"The process cannot access the file... because it is being used by another process."* Stop the watcher first.

---

## Part 3 — Connecting Angular to .NET

This is the part the viva will probe hardest, because it is where most people copy code without understanding it.

### 3.1 Why they cannot just talk

Angular runs at `http://localhost:4200`. The API runs at `http://localhost:5041`. To a browser these are **different origins**, because an origin is the triple *(scheme, host, port)* — and the ports differ.

The browser's **same-origin policy** blocks a page from reading responses from a different origin by default. This is a security feature: without it, a malicious page you visit could silently call your bank's API using your logged-in cookies and read the response.

So the API must explicitly say *"I permit `http://localhost:4200` to read my responses."* That permission mechanism is **CORS** — Cross-Origin Resource Sharing.

### 3.2 The critical detail about CORS

**CORS is enforced by the browser, not the server.** The server always processes the request and sends a response. The browser then inspects the response headers and, if the permission header is missing, refuses to hand the body to your JavaScript.

Two consequences that confuse everybody:

- **Postman and curl never show CORS errors.** They are not browsers and do not enforce the policy. "It works in Postman but not in my app" almost always means CORS.
- **The request usually reached your server and ran.** A failed CORS `POST` may well have written to your database. The browser blocked you reading the *response*, not the server doing the work.

### 3.3 Preflight requests

For anything beyond a "simple" request — a `PUT`/`PATCH`/`DELETE`, or a `POST` with `Content-Type: application/json`, or a custom header like `Authorization` — the browser first sends an `OPTIONS` request to ask permission:

```
OPTIONS /api/books HTTP/1.1
Origin: http://localhost:4200
Access-Control-Request-Method: POST
Access-Control-Request-Headers: content-type, authorization
```

The server must answer with matching `Access-Control-Allow-*` headers before the browser will send the real request. In your Network tab you will see **two entries** for one call — the `OPTIONS`, then the `POST`. That is normal, not a bug.

This is also why `.AllowAnyHeader()` matters once JWT arrives: without it the `Authorization` header fails preflight and every authenticated call dies.

### 3.4 The .NET side

Two steps, in `Program.cs`. **Register** the policy before `builder.Build()`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy => policy.WithOrigins("http://localhost:4200")
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});
```

Then **use** it in the pipeline:

```csharp
var app = builder.Build();

app.UseCors("AllowAngular");   // must come before the endpoints it protects

app.MapControllers();
```

**Order is the thing to remember.** Middleware runs in the order you register it. `UseCors` must run before routing reaches your endpoints, or the response goes out without the headers. Our original code called `app.UseCors()` *after* `app.MapGet()` and happened to still work — because `WebApplication` auto-inserts routing middleware around whatever you register — but that is luck, not design. Put it before.

Three more points on this:

- **`WithOrigins` must match exactly** — scheme, host and port, and **no trailing slash**. `http://localhost:4200/` fails to match.
- **Never ship `AllowAnyOrigin()`** with credentials. The spec forbids combining `AllowAnyOrigin` with `AllowCredentials`, and it disables the protection entirely.
- **`AllowCredentials()` is needed only for cookies.** With a JWT in an `Authorization` header, `AllowAnyHeader()` is enough.

### 3.5 The Angular side — providing HttpClient

`HttpClient` is not available by default. Register it in `app.config.ts`:

```typescript
export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideZonelessChangeDetection(),
    provideHttpClient(withFetch())
  ]
};
```

**Why `withFetch()`:** by default `HttpClient` uses `XMLHttpRequest`, which does not exist in Node. This app has SSR, so the first render happens on a Node server. `withFetch()` switches to the Fetch API, which works in both. Without it you get warnings and failures during server-side rendering.

If you forget `provideHttpClient` entirely, you get:
> `NullInjectorError: No provider for _HttpClient!`

### 3.6 The service

Never call `HttpClient` from a component. Put it in an injectable service:

```typescript
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Book {
  id: number;
  title: string;
  isbn: string;
  availableCopies: number;
}

@Injectable({ providedIn: 'root' })
export class BookService {
  private readonly baseUrl = 'http://localhost:5041/api';
  private readonly http = inject(HttpClient);

  getBooks(): Observable<Book[]> {
    return this.http.get<Book[]>(`${this.baseUrl}/books`);
  }

  getBook(id: number): Observable<Book> {
    return this.http.get<Book>(`${this.baseUrl}/books/${id}`);
  }

  createBook(dto: Partial<Book>): Observable<Book> {
    return this.http.post<Book>(`${this.baseUrl}/books`, dto);
  }
}
```

Points to be ready to explain:

- **`providedIn: 'root'`** registers the service as a singleton in the root injector, and lets unused services be tree-shaken out of the bundle.
- **`inject()` vs constructor injection.** Both work. `inject()` is the modern form and avoids repeating the type name.
- **The generic `get<Book[]>`** is a *compile-time* annotation only. Angular does not validate the JSON at runtime — it just casts. If the API returns a different shape you get `undefined` at runtime, not an error.
- **`HttpClient` returns a cold `Observable`.** Nothing is sent until something subscribes. Call `getBooks()` without subscribing and **no HTTP request happens at all**. This surprises people constantly.
- **It completes after one value.** Unlike a long-lived stream, an `HttpClient` observable emits once then completes, which is why manual unsubscribing is usually unnecessary.

### 3.7 Why camelCase just works

Your C# `Book.Title` arrives in TypeScript as `title`. ASP.NET Core's `System.Text.Json` serialises using a camelCase naming policy by default, matching JavaScript convention. You do not have to configure anything — but know *why*, because "how does `Title` become `title`?" is a fair question.

### 3.8 Consuming it in a component

Three approaches. Know all three and when each fits.

**A. Subscribe manually** — most explicit:

```typescript
export class BookList {
  protected readonly books = signal<Book[]>([]);
  protected readonly error = signal<string | null>(null);

  private readonly bookService = inject(BookService);

  constructor() {
    this.bookService.getBooks().subscribe({
      next: (data) => this.books.set(data),
      error: (err) => this.error.set(err.message)
    });
  }
}
```

**B. `AsyncPipe`** — the template subscribes and unsubscribes for you:

```typescript
protected readonly books$ = inject(BookService).getBooks();
```
```html
@for (book of books$ | async; track book.id) {
  <li>{{ book.title }}</li>
}
```

**C. `toSignal()`** — bridge an Observable into a signal, the idiomatic choice in a zoneless app:

```typescript
import { toSignal } from '@angular/core/rxjs-interop';

protected readonly books = toSignal(inject(BookService).getBooks(), {
  initialValue: [] as Book[]
});
```

**Why signals matter here specifically.** This app is zoneless. In the old Zone.js model, Angular patched every async callback and re-rendered afterwards, so mutating a plain field inside `subscribe` "just worked". Without Zone.js there is no such hook — **updating a plain property may not re-render the view**. Signals notify Angular explicitly when their value changes. So in this project: store API results in signals, not plain fields.

### 3.9 Which URL — and the SSR trap

Our first attempt pointed at `https://localhost:7001`, which nothing was listening on. Get this right:

- The port comes from `launchSettings.json`, not from memory. Ours is **5041** for HTTP.
- We deliberately use **HTTP, not HTTPS, in development.** The HTTPS endpoint uses the .NET developer certificate. Browsers accept it after `dotnet dev-certs https --trust`, **but Node does not** — Node ships its own CA bundle and ignores the Windows certificate store. Because this app has SSR, the first render runs in Node, and an HTTPS call there fails with `UNABLE_TO_VERIFY_LEAF_SIGNATURE`. Using HTTP sidesteps the whole problem in development.
- Correspondingly, `Program.cs` only calls `app.UseHttpsRedirection()` outside Development — otherwise it would bounce the HTTP call to HTTPS and reintroduce the same failure.

### 3.10 Hard-coded URLs are a smell

`http://localhost:5041` in a service is fine for now but wrong for deployment. The real fix is Angular's environment files:

```typescript
// environments/environment.ts
export const environment = { apiUrl: 'http://localhost:5041/api' };

// environments/environment.prod.ts
export const environment = { apiUrl: '/api' };
```

Mention this if asked "what would you change for production?" — it shows you know the difference between working and production-ready.

### 3.11 The proxy alternative — no CORS at all

There is a second way to connect, and knowing it is a strong viva answer.

Create `FrontendApp/proxy.conf.json`:

```json
{
  "/api": {
    "target": "http://localhost:5041",
    "secure": false
  }
}
```

Point `angular.json`'s serve options at it, then call `/api/books` — a **relative** URL. The Angular dev server forwards it to :5041 server-to-server.

**Why this removes CORS entirely:** the browser now only ever talks to `localhost:4200`. Same origin, so the same-origin policy is never triggered. The forwarding happens outside the browser, where the policy does not apply.

**Trade-off:** the proxy is a *development-server* feature. It does not exist in production, where you would put both behind one reverse proxy or domain. We use explicit CORS because it is closer to how the deployed system actually behaves — and because CORS is the concept you are expected to demonstrate.

### 3.12 Interceptors — where the JWT goes

Rather than attaching the token in every service method, register one interceptor:

```typescript
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = localStorage.getItem('token');
  return token
    ? next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }))
    : next(req);
};

// app.config.ts
provideHttpClient(withFetch(), withInterceptors([authInterceptor]))
```

Two things to note:

- **`req.clone()` is required.** `HttpRequest` is immutable — you cannot assign to `req.headers`. You clone with modifications and pass the clone on.
- **`localStorage` does not exist during SSR.** On the server that line throws `ReferenceError: localStorage is not defined`. Guard it with `isPlatformBrowser`, or render auth-gated routes client-side only. This is a real conflict between SSR and the `localStorage` token strategy in `README.md` section 7 — worth raising yourself in the viva, because it shows you understood both halves.

### 3.13 Debugging checklist

When a call fails, work down this list in order:

| Symptom | Likely cause |
|---|---|
| `ERR_CONNECTION_REFUSED` | API not running, or wrong port. Not CORS. |
| Works in Swagger/Postman, fails in browser | CORS. Check `WithOrigins` matches exactly, no trailing slash. |
| `No 'Access-Control-Allow-Origin' header` | Policy not applied, or `UseCors` registered after the endpoints. |
| `OPTIONS` request returns 404/405 | Preflight not handled — `AllowAnyMethod`/`AllowAnyHeader` missing. |
| `NullInjectorError: No provider for _HttpClient` | `provideHttpClient()` missing from `app.config.ts`. |
| Nothing happens, no network request | You never subscribed. The Observable is cold. |
| Data arrives but the view does not update | Zoneless — you assigned to a plain field instead of a signal. |
| `localStorage is not defined` | SSR. Guard with `isPlatformBrowser`. |
| 401 on every request after login | Preflight rejecting `Authorization` — need `AllowAnyHeader()`. |

---

## Part 4 — SQL Server and EF Core

### 4.1 Install the packages

```bash
cd BackendAPI
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.28
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.28
dotnet tool install --global dotnet-ef
```

**Why two packages**, and why they are treated differently:

- **`.SqlServer`** is the runtime provider. It translates LINQ into T-SQL and manages connections. It ships with your app.
- **`.Design`** is design-time only — `dotnet ef` needs it to build the model and generate migration code. Look at the csproj: NuGet gave it `<PrivateAssets>all</PrivateAssets>`, meaning it is never deployed. It is a tool for the developer, not code for the user.

`dotnet ef` is a separate **global tool**, not a package. It is the CLI that drives migrations.

### 4.2 The connection string

Goes in `appsettings.json`, top level:

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=LP703;Initial Catalog=LibraryDb;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Application Name=BackendAPI"
  }
}
```

Part by part:

| Key | Meaning |
|---|---|
| `Data Source` | The server instance. `LP703` here; `.` or `(localdb)\MSSQLLocalDB` elsewhere |
| `Initial Catalog` | The database. **Omit it and you silently connect to `master`** |
| `Integrated Security=True` | Windows authentication — logs in as the current Windows user |
| `Encrypt=False` / `TrustServerCertificate=True` | Local dev only. Production should encrypt and validate properly |
| `Application Name` | Shows in SQL Server Activity Monitor — invaluable for finding which app holds a lock |

**Why this string is safe to commit:** `Integrated Security=True` means there is no password in it. It authenticates as whoever runs the app. A SQL-authentication string containing `User Id` and `Password` would **not** be safe — that belongs in User Secrets (`dotnet user-secrets set`) or environment variables.

Two settings worth removing if you copy a string out of SSMS:

- **`Pooling=False`** — fine for a single SSMS session, wrong for a web API. Connection pooling reuses open connections; disabling it makes every HTTP request pay a fresh TCP and authentication handshake.
- **`Command Timeout=0`** — means *wait forever*. A hung query would block a request thread indefinitely.

### 4.3 Entities — convention over configuration

Entities are plain C# classes. EF Core reads their shape and infers the schema. Six conventions do most of the work:

| Convention | Effect |
|---|---|
| Property named `Id` (or `<Type>Id`) | Primary key, `IDENTITY` in SQL Server |
| `CategoryId` + `Category` property pair | Foreign key column + navigation property |
| `ICollection<Book>` | The "many" side of a one-to-many; creates no column |
| `string` vs `string?` | `NOT NULL` vs `NULL` (because `<Nullable>enable</Nullable>`) |
| `DbSet<Book> Books` on the context | A table named `Books` |
| `int?`, `DateTime?` | Nullable column |

```csharp
public class Book
{
    public int Id { get; set; }                       // PK by convention
    public string Title { get; set; } = string.Empty; // NOT NULL
    public string? Description { get; set; }          // NULL

    public int CategoryId { get; set; }               // FK column
    public Category Category { get; set; } = null!;   // navigation

    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public bool IsActive { get; set; } = true;        // soft delete

    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}
```

**On `AvailableCopies`:** this is denormalised on purpose. You *could* compute it by counting unreturned `IssuedBooks` rows, but then every catalogue page load runs an aggregate over the loans table. Storing a counter trades a little redundancy for a lot of read speed. Be ready to defend it — and to say the risk is that the counter can drift if issue/return logic is buggy, which is why those updates belong in one service method.

**On `IsActive`:** books are never hard-deleted. Deleting a book row would orphan the `IssuedBook` and `Feedback` rows that reference it, destroying audit history.

### 4.4 The DbContext

`AppDbContext` is your session with the database — it holds the `DbSet`s, tracks changes, and translates LINQ to SQL.

We declare it **`partial`** and split it across two files so that bulky seed data does not drown the mapping configuration:

```
Data/AppDbContext.cs        ← DbSets and OnModelCreating
Data/AppDbContext.Seed.cs   ← HasData seed values
```

That is a genuine use of `partial`, not a contrived one: the two halves change for different reasons.

`OnModelCreating` handles everything convention cannot infer:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Composite key — BookAuthor has no Id of its own
    modelBuilder.Entity<BookAuthor>().HasKey(ba => new { ba.BookId, ba.AuthorId });

    // Enums as readable text, not 0/1/2
    modelBuilder.Entity<IssueRequest>().Property(r => r.Status)
        .HasConversion<string>().HasMaxLength(20);

    // Money needs explicit precision
    modelBuilder.Entity<IssuedBook>().Property(i => i.Fine).HasPrecision(18, 2);

    // Uniqueness the business rules depend on
    modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
    modelBuilder.Entity<Book>().HasIndex(b => b.Isbn).IsUnique();
}
```

Why each of those:

- **`HasConversion<string>()`** — in C# you get a type-safe enum and can `switch` on it; in SQL the column stores `'Pending'` rather than `0`. Readable in SSMS, and immune to someone reordering the enum members later.
- **`HasPrecision(18, 2)`** — without it EF warns and picks a default that silently rounds money.
- **Unique index on `Email`** — enforces "one account per email" at the database level, so a race between two simultaneous registrations cannot create duplicates. Validation in C# alone cannot guarantee that.

**Seed data must be constant.** `HasData` values are compared against the model snapshot on every `migrations add`. Using `DateTime.UtcNow` would make EF think the model changed every single time and generate an endless stream of pointless migrations.

### 4.5 Register it with DI

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
```

**`AddDbContext` registers a Scoped service** — one context per HTTP request, disposed when the request ends. Know why that is the right lifetime:

| Lifetime | Why not |
|---|---|
| Singleton | `DbContext` is **not thread-safe** and tracks changes. One user's half-saved edits would leak into another user's request. |
| Transient | Every injection gets its own context, so two repositories in one request could not share a transaction. |
| **Scoped** | One per request. Change tracking and `SaveChangesAsync()` work as a unit. |

The three DI lifetimes in general — `AddSingleton`, `AddScoped`, `AddTransient` — are near-certain viva material.

### 4.6 Migrations

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

**What each does:**

- `migrations add` compares your current model to the last snapshot and **generates C# code** describing the difference. It touches no database.
- `database update` **executes** the pending migrations against the server, creating the database if it does not exist.

Three files appear per migration:

| File | Purpose |
|---|---|
| `<timestamp>_InitialCreate.cs` | `Up()` applies the change, `Down()` reverses it |
| `<timestamp>_InitialCreate.Designer.cs` | Model snapshot at that point in time |
| `AppDbContextModelSnapshot.cs` | Current cumulative model — how EF computes the *next* diff |

EF records applied migrations in a table called **`__EFMigrationsHistory`**. That is how it knows what is already applied and never runs a migration twice.

**Rules that will save you:**

- `dotnet ef migrations remove` undoes the *last* migration — but only if you have not applied it yet.
- Never hand-edit a migration that has already been applied. Add a new one instead.
- Never delete `AppDbContextModelSnapshot.cs`. EF loses its reference point and the next migration tries to recreate everything.
- Commit migrations to git. They are source code, not build output.

### 4.7 The cascade-path problem

This is the wall almost everyone hits on their first real schema, and it makes an excellent viva answer.

EF defaults required relationships to `DeleteBehavior.Cascade`. In our schema, `Book` reaches `IssuedBook`, and `Member` reaches `IssuedBook` too. Two cascading paths converge on one table, and SQL Server refuses outright:

> *Introducing FOREIGN KEY constraint on table 'IssuedBooks' may cause cycles or multiple cascade paths.*

Why SQL Server refuses: with two cascade routes into the same row it cannot determine a safe deletion order, so it rejects the constraint rather than risk ambiguous behaviour.

The fix is to change the delete behaviour to `Restrict`:

```csharp
foreach (var fk in modelBuilder.Model.GetEntityTypes()
             .SelectMany(t => t.GetForeignKeys())
             .Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade
                          && fk.DeclaringEntityType.ClrType != typeof(Member)))
{
    fk.DeleteBehavior = DeleteBehavior.Restrict;
}
```

**And `Restrict` is what we actually want anyway** — deleting a book must not silently erase the record of who borrowed it. This is exactly why the design uses soft delete (`IsActive`) instead. The technical constraint and the business rule point the same way.

Our final schema has **14 `Restrict` and 1 `Cascade`** — the single cascade being `User → Member`, since deleting an account should remove its profile.

### 4.8 Verifying it worked

Do not trust the console output. Check the server:

```sql
USE LibraryDb;
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';
SELECT * FROM __EFMigrationsHistory;
```

You should see 11 tables plus `__EFMigrationsHistory`. Open the database diagram in SSMS and look at the foreign key arrows — seeing the shape makes everything above concrete.

---

## Part 5 — The layered architecture

### Why not put the SQL in the controller

You can. It works. It is also the thing the architecture in `README.md` exists to avoid, so be ready to explain the cost:

- **Untestable** — testing a controller that opens a database connection requires a database.
- **Unreusable** — a background job that needs the same rule has to duplicate it.
- **Unchangeable** — business rules scattered across nine controllers cannot be reasoned about.

### The four layers

```
Controller  →  Service  →  Repository  →  DbContext  →  SQL Server
   HTTP        business       data         EF Core
```

| Layer | Knows about | Must not know about |
|---|---|---|
| Controller | HTTP, routes, status codes | SQL, EF Core |
| Service | Business rules, DTOs | HTTP, `HttpContext` |
| Repository | EF Core, LINQ | HTTP, business rules |

The test: **a service method should be callable from a console app.** If it references `HttpContext`, the boundary has leaked.

### Generic repository

```csharp
public interface IRepository<T> where T : class
{
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
```

`BaseRepository<T>` implements it once; `BookRepository : BaseRepository<Book>` inherits all of it and `override`s only `DeleteAsync` to do a soft delete. That single pairing demonstrates **generics, inheritance, abstraction, and method overriding** in one place — several rows of the concept map at once.

### Why DTOs

Never return entities from a controller. Three concrete reasons:

1. **`User` contains `PasswordHash`.** Returning the entity leaks it.
2. **Circular references.** `Book` → `Feedbacks` → `Member` → `Feedbacks` → … the JSON serialiser loops forever.
3. **Coupling.** Renaming a column becomes a breaking API change for the Angular app.

A DTO also lets you *shape* data: `FullName` from `FirstName + LastName`, `AvgRating` computed from feedback.

### Dependency inversion in practice

Register the interface, not the class:

```csharp
builder.Services.AddScoped<IBookService, BookService>();
```

The controller asks for `IBookService` and DI supplies `BookService`. Swapping implementations — for a test double, or a caching decorator — is one line in `Program.cs` and no change to the controller. That is the practical payoff of programming against abstractions.

---

## Part 6 — Authentication with JWT

### Why tokens rather than sessions

A JWT is **self-contained**: it carries the user's id and role inside itself, signed by the server. The API can validate it with the signing key alone — no session lookup, no server-side state. That is what lets an API scale across machines and serve a browser app that holds no cookie.

### The shape of a token

`header.payload.signature`, base64url-encoded and dot-separated.

**The payload is encoded, not encrypted.** Anyone can decode and read it — paste one into jwt.io and you will see the claims in plain text. So: **never put secrets in a JWT.** What the signature guarantees is *integrity* — that nobody altered the claims — not confidentiality.

### The flow

1. `POST /api/auth/login` with email and password
2. Server verifies the password against `PasswordHash`
3. Server builds claims (`UserId`, `Role`, `Email`, `Name`), signs them, returns the token
4. Angular stores it and attaches `Authorization: Bearer <token>` to every request
5. `[Authorize(Roles = "Librarian")]` on a controller checks the role claim

### Never store a raw password

Hash it, with a **salted, deliberately slow** algorithm — BCrypt, or ASP.NET Core's `PasswordHasher<T>`. Not MD5 or SHA-256: those are built to be fast, which is exactly what you do not want when someone is brute-forcing a stolen table.

### Note the ordering in the pipeline

```csharp
app.UseAuthentication();   // who are you?   — reads and validates the token
app.UseAuthorization();    // are you allowed? — checks roles and policies
```

Authentication must come first — authorization has nothing to check until identity is established. Reversing them means every `[Authorize]` fails.

---

## Part 7 — File handling

Book covers and quote images go to `wwwroot/uploads/`, served by the static files middleware.

### The security rules

**Never trust the uploaded filename.** A file named `../../appsettings.json` is a path-traversal attack. Generate your own name:

```csharp
var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
var safeName = $"{Guid.NewGuid()}{extension}";
```

This also prevents two users overwriting each other's uploads with the same filename.

**Validate the extension against an allowlist**, never a blocklist:

```csharp
private static readonly HashSet<string> Allowed = new() { ".jpg", ".jpeg", ".png", ".webp" };
```

A `HashSet` gives O(1) lookup — and is a natural place to demonstrate why you would choose it over a `List`.

**Enforce a size limit**, or one upload fills the disk.

### Streams and `using`

```csharp
await using var stream = new FileStream(path, FileMode.Create);
await file.CopyToAsync(stream, ct);
```

`using` guarantees `Dispose()` runs even if an exception is thrown — the file handle is released either way. Without it, handles leak and the file stays locked. This is the practical face of `IDisposable` and deterministic cleanup.

### `TextWriter` polymorphism for CSV export

```csharp
public async Task ExportIssuedAsync(TextWriter writer, CancellationToken ct)
```

Accepting the **base type** `TextWriter` means the same method writes to a `StreamWriter` (a file), a `StringWriter` (memory, for an HTTP response), or `Console.Out` (debugging). The caller decides the destination; the method never changes. That is polymorphism earning its place rather than being bolted on for the sake of the syllabus.

---

## Part 8 — Scheduled work without Hangfire

`README.md` section 9 describes Hangfire. **We are not using it.** Here is what to do instead, and what to say if asked.

### `BackgroundService` — built into .NET

```csharp
public class DueDateReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DueDateReminderService> _logger;

    public DueDateReminderService(IServiceScopeFactory scopeFactory,
                                  ILogger<DueDateReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var dueSoon = await db.IssuedBooks
                    .Where(i => !i.IsReturned
                                && i.DueDate <= DateTime.UtcNow.AddDays(2))
                    .Include(i => i.Member)
                    .ToListAsync(stoppingToken);

                _logger.LogInformation("{Count} loans due within 2 days", dueSoon.Count);
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

Register it:
```csharp
builder.Services.AddHostedService<DueDateReminderService>();
```

### The one thing that will catch you out

**A `BackgroundService` is a singleton. `AppDbContext` is scoped.** Injecting `AppDbContext` directly into the constructor throws at startup:

> *Cannot consume scoped service 'AppDbContext' from singleton 'DueDateReminderService'.*

The fix is above: inject `IServiceScopeFactory` and create a scope per iteration. This is a favourite interview question because it tests whether you actually understand DI lifetimes rather than just reciting them.

### Why this instead of Hangfire — the honest answer

| | `BackgroundService` | Hangfire |
|---|---|---|
| Dependencies | None, built in | NuGet package + ~10 extra tables in your database |
| Survives restart | No — in-memory timer | Yes, jobs persisted |
| Retries | You write them | Automatic |
| Dashboard | None | Built-in UI |
| Multi-server | Would run on every instance | Coordinated |

For a single-instance assessment project, `BackgroundService` is the right call: zero dependencies, and it demonstrates `IHostedService`, DI scoping, `CancellationToken`, and `Task.Delay` — all syllabus items. Hangfire would hide those behind an abstraction.

Good viva answer: *"Hangfire earns its place when jobs must survive restarts or coordinate across servers. For one instance, `BackgroundService` does the job with no dependencies — and I'd move to Hangfire the moment we scaled out or needed guaranteed retries."*

> **Action item:** `README.md` still contains a Hangfire section (9), a Hangfire row in the async table, and a Hangfire mention in the issue-flow diagram. Update those to `BackgroundService` so the spec matches the code.

---

## Part 9 — Viva question bank

### Angular ↔ .NET

**Q. Why do you need CORS here?**
Angular is on port 4200, the API on 5041. Different port means different origin, and the browser's same-origin policy blocks the response unless the server explicitly permits that origin.

**Q. Where is CORS enforced — client or server?**
The browser enforces it; the server just sends headers. The request still reaches the server and runs. That is why it works in Postman but not the browser, and why a blocked POST may still have written to the database.

**Q. What is a preflight request?**
An automatic `OPTIONS` request the browser sends before non-simple requests (custom headers, JSON `POST`, `PUT`/`DELETE`) to ask permission. The real request follows only if the server approves.

**Q. Why must `UseCors` come before the endpoints?**
Middleware runs in registration order. Register it after and the response is produced before the CORS headers are added.

**Q. What does `provideHttpClient()` do?**
Registers `HttpClient` and its dependencies in the root injector. Without it you get `NullInjectorError: No provider for _HttpClient`.

**Q. Why `withFetch()`?**
Switches the backend from `XMLHttpRequest` to the Fetch API. Required for SSR, because `XMLHttpRequest` does not exist in Node.

**Q. You called `getBooks()` but no request was sent. Why?**
`HttpClient` returns a **cold** Observable. Nothing happens until you subscribe — directly, or via `AsyncPipe` / `toSignal`.

**Q. How does C# `Title` become TypeScript `title`?**
`System.Text.Json` serialises with a camelCase naming policy by default.

**Q. Data arrives but the view does not update. Why?**
The app is zoneless. Without Zone.js, Angular is not notified when a plain field changes. Store the result in a `signal`.

**Q. How would you connect without CORS at all?**
Angular's dev-server proxy (`proxy.conf.json`). The browser only ever calls `localhost:4200`, so it is same-origin; forwarding happens server-side where the policy does not apply. But it is dev-only.

### EF Core and the database

**Q. What does `AddDbContext` register, and why that lifetime?**
Scoped — one context per request. `DbContext` is not thread-safe and tracks changes, so a singleton would leak state between users.

**Q. `migrations add` vs `database update`?**
`add` generates C# describing the model diff and touches no database. `update` executes pending migrations against the server.

**Q. How does EF know which migrations have run?**
The `__EFMigrationsHistory` table.

**Q. What is "multiple cascade paths" and how did you fix it?**
Two cascading delete routes converge on one table — `Book`→`IssuedBook` and `Member`→`IssuedBook` — and SQL Server cannot determine a safe delete order, so it rejects the constraint. Fixed by setting those relationships to `Restrict`, which is what the business rules want anyway, since loan history must survive.

**Q. Why soft delete instead of real delete?**
A hard delete would orphan `IssuedBook` and `Feedback` rows and destroy audit history. `IsActive = false` preserves it.

**Q. Why store enums as strings?**
Readable in SSMS, and stable if enum members are reordered later. Costs a few bytes.

**Q. Why `HasPrecision(18, 2)` on `Fine`?**
Money needs exact decimal precision. Without it EF warns and picks a default that can silently round.

**Q. Is committing the connection string safe?**
Here, yes — `Integrated Security=True` means no password; it authenticates as the current Windows user. A SQL-auth string with a password belongs in User Secrets or environment variables.

**Q. Deferred vs immediate execution in LINQ?**
`IQueryable` builds an expression tree and sends nothing until you materialise it with `ToListAsync`, `FirstOrDefaultAsync`, etc. That is what lets you compose `Where` clauses conditionally before a single query is sent.

**Q. What does `AsNoTracking()` do?**
Skips change-tracking setup. Faster and lighter for read-only queries — appropriate for any `GET`.

**Q. `Include()` and what problem does it solve?**
Eager loading of navigation properties. Without it, accessing `book.Category` in a loop triggers a query per row — the N+1 problem.

### C# and ASP.NET Core

**Q. The three DI lifetimes?**
Singleton — one for the app. Scoped — one per request. Transient — one per injection.

**Q. Can a singleton depend on a scoped service?**
No. It throws at startup. Inject `IServiceScopeFactory` and create a scope when you need one — exactly what the `BackgroundService` does.

**Q. What does `async`/`await` actually buy you?**
Not speed for a single request. While awaiting I/O the thread returns to the pool and serves other requests, so the server handles far more concurrent load with the same threads.

**Q. Why `CancellationToken` on every action?**
If the client disconnects, the token is signalled and the work — including the SQL query — is abandoned instead of burning resources on a response nobody will read.

**Q. `Task` vs `ValueTask`?**
`Task` is a reference type and allocates. `ValueTask` avoids the allocation when the result is often already available synchronously — worth it on hot paths like a cached lookup.

**Q. Why `IAsyncEnumerable<T>` for the overdue report?**
It streams rows as they arrive instead of materialising the whole result set in memory. Constant memory regardless of row count.

**Q. `abstract class` vs `interface` — why does the project use both?**
`IRepository<T>` is an interface because it is a pure contract with no shared code. `BaseRepository<T>` is a class because it carries a real implementation to inherit. A class can implement many interfaces but inherit one base.

**Q. Where is a multicast delegate used, and why not just call the methods?**
`OnBookIssued` fires email and logging. The point is decoupling: adding a new notification channel means registering a subscriber, with zero changes to `IssueService`.

**Q. Why is `FineCalculator` sealed and static?**
It is a stateless utility. `static` means no instance is needed; `sealed` documents that it is not designed for inheritance and lets the compiler devirtualise calls.

**Q. Where would boxing occur, and how do you avoid it?**
Storing value types in a non-generic collection like `ArrayList`. `List<T>` and generics avoid it — the type is known at compile time so no heap allocation is needed.

### Authentication and authorization

**Q. 401 versus 403 — what is the difference?**
401 means "I do not know who you are" — missing, expired or invalid token. 403 means "I know exactly who you are, and you are not allowed" — valid token, wrong role. A member calling a Librarian endpoint gets 403, not 401.

**Q. Why does the interceptor log out on 401 but not 403?**
401 means the session is over, so clearing it is right. 403 means the session is perfectly valid — the user simply lacks the role. Logging out there would eject a legitimately signed-in user.

**Q. Two `[Authorize]` attributes, one on the class and one on the action — how do they combine?**
With AND. Every applicable attribute must be satisfied. An action-level attribute can narrow access but never widen it — which is why the class carries a bare `[Authorize]` and the roles sit on the actions.

**Q. Where does `MemberId` come from when a member requests a book?**
From the `memberId` claim in the validated token, never from the request body. Taking it from the body would let one member raise requests in another member's name.

**Q. Why BCrypt rather than SHA-256?**
BCrypt salts each password automatically and is deliberately slow, which makes brute-forcing a stolen table expensive. SHA-256 is built to be fast — exactly the wrong property here.

**Q. Why does login return the same message for an unknown email and a wrong password?**
Distinguishing them lets an attacker enumerate which emails have accounts.

**Q. Is anything in a JWT secret?**
No. The payload is base64-encoded, not encrypted — anyone can read it. The signature guarantees integrity, not confidentiality. Never put a secret in a claim.

### Recommendation engine

**Q. Which approach did you use, and why not collaborative filtering?**
Content-based: the member's own borrowing history becomes a weighted profile of categories and authors, and unread books are scored against it. Collaborative filtering needs a large user base before member-to-member overlap means anything — on a library with a handful of members it returns an empty list. Content-based works from the first loan. The trade-off is that it is narrower: it will not surface anything outside what they already read.

**Q. Why is an author worth 3× a category?**
Sharing "Fiction" with a book carries almost no information — a large share of the catalogue is Fiction. Sharing an author carries a lot. The weights reflect how much each signal actually tells you.

**Q. How do ratings affect it?**
They reweight the member's own history. A book they rated 4–5 contributes ×1.5, an unrated or middling one ×1.0, and one they rated 1–2 only ×0.25 — so the engine does not keep recommending more of what they disliked.

**Q. Why are the popularity and rating weights so small?**
They are tie-breakers, at 0.10 and 0.05 against affinity weights of 1.0 and 3.0. They order books that already match the profile. If they were larger, a popular book the member has no affinity for would outrank a genuine match, and the recommender would degenerate into a bestseller list.

**Q. What happens for a brand-new member?**
Cold start: there is no profile to match, so it falls back to normalised popularity plus average rating, and the response says `isColdStart: true` so the UI can explain itself. Returning an empty page would be a worse experience than a generic suggestion.

**Q. Why does each recommendation carry a reason?**
A recommendation a member cannot understand is one they will not act on. It also makes the ranking inspectable — you can see why a book placed where it did, rather than trusting a number. The raw score is returned alongside it for the same reason.

**Q. Why a `HashSet` for the exclusion list?**
It is checked once per candidate book. `Contains` is O(1) on a `HashSet` and O(n) on a `List`, so the cost goes from O(candidates × excluded) to O(candidates).

**Q. What would you improve?**
Three things. The candidate set is loaded into memory and scored in C#; with tens of thousands of books the scoring should move into SQL or a precomputed table. There is no diversity control, so a member who reads one author heavily gets a page of that author. And the weights are hard-coded constants — they should be configurable, and ideally tuned against click-through rather than chosen by hand.

### Trending

**Q. How is trending different from the dashboard's "most issued"?**
Most-issued is all-time volume. Trending compares a recent window against the equally long window before it, so it measures momentum. Without that distinction the trending page would just be a second copy of the dashboard list.

**Q. Why is momentum weighted higher than volume?**
At 1.5 versus 1.0, a genuinely accelerating title beats a steadily popular one. If volume dominated, the ranking would collapse back into all-time popularity and the feature would be pointless.

**Q. Why count distinct members separately from issues?**
Two members borrowing once each is broader demand than one member borrowing the same book twice. Counting loans alone cannot tell those apart.

**Q. Why do requests count towards the score?**
A request that never became a loan usually means no copy was free. That is unmet demand — precisely what a librarian needs to see when deciding what to buy more of, and it is invisible in loan counts.

**Q. Why is `percentChange` null instead of 0 or 100 for a new book?**
Growth from zero is not a percentage — any number reported there would be fabricated. Null is the honest answer, and the `New` trend direction is what communicates that case.

**Q. A book has two authors. How is one loan counted?**
`SelectMany` flattens each loan into (loan, author) pairs before grouping, so the loan contributes to both authors. Grouping on the loan alone would undercount co-authored titles.

**Q. What would you improve?**
Both windows are loaded into memory and bucketed in C#. That is fine for this scale but should become a SQL `GROUP BY` with a date filter, or a nightly aggregate table, once loan history is large. The weights are also hard-coded constants that ought to be configurable.

### Business logic

**Q. Why is the cooldown measured from `DueDate` rather than `ReturnDate`?**
So a member cannot return a book early to reset the cooldown and immediately re-request it. The cooldown is demand management, not a penalty.

**Q. Why track `AvailableCopies` instead of counting loans?**
Counting unreturned `IssuedBooks` on every catalogue load is expensive. The trade-off is possible drift, which is why issue and return both update it inside one `SaveChangesAsync`.

**Q. Two members request the last copy simultaneously. What happens?**
Both could read `AvailableCopies = 1` and both decrement. The fix is a concurrency token (`[Timestamp]` / `IsRowVersion()`) so the second save fails with a `DbUpdateConcurrencyException`, or doing the check and decrement inside a transaction. Raising this yourself shows real understanding.

---

## Part 10 — Command reference

```bash
# ---- Backend ----
cd BackendAPI
dotnet restore
dotnet build
dotnet run                      # start once
dotnet watch run                # start with hot reload
dotnet run --launch-profile http

# ---- EF Core ----
dotnet ef migrations add <Name>
dotnet ef migrations remove       # undo the last, if unapplied
dotnet ef migrations list
dotnet ef database update
dotnet ef database update <Name>  # migrate to a specific point
dotnet ef database update 0       # roll everything back
dotnet ef migrations script       # generate SQL instead of applying
dotnet ef dbcontext info

# ---- Frontend ----
cd FrontendApp
npm install
ng serve
ng build
ng test
ng generate component books/book-list
ng generate service services/book
```

> Remember: stop `dotnet watch run` before any `dotnet ef` command, or the build fails on a locked `BackendAPI.exe`.

---

## Part 11 — Errors we actually hit

Real failures from building this project. These make good answers precisely because they happened.

### `ERR_CONNECTION_REFUSED` calling the API

**Cause:** the Angular service pointed at `https://localhost:7001`, copied from a tutorial. Nothing was listening there — the API runs on `5041`.
**Fix:** read the port from `launchSettings.json`, never from memory.
**Lesson:** confirm the endpoint responds in Swagger before debugging the client.

### `NG0908: In this configuration Angular requires Zone.js`

**Cause:** `app.config.ts` called `provideZoneChangeDetection()`, but Angular 20 scaffolded this app **without** `zone.js` — no dependency, no `polyfills` entry.
**Fix:** `provideZonelessChangeDetection()`.
**Lesson:** the build failed at the SSR prerender step with a bare error code. Error codes are searchable — `NG0908` maps to `MISSING_ZONEJS` in the Angular source.

### `Introducing FOREIGN KEY constraint may cause cycles or multiple cascade paths`

**Cause:** `Book` and `Member` both cascade into `IssuedBook`.
**Fix:** set the converging relationships to `DeleteBehavior.Restrict`.
**Lesson:** see Part 4.7. The database constraint and the business rule agreed — history must not be cascade-deleted.

### `The process cannot access the file 'BackendAPI.exe'`

**Cause:** `dotnet watch run` was running and held the output binary while `dotnet ef` tried to build.
**Fix:** stop the watcher first.

### SSR cannot call the HTTPS endpoint

**Cause:** Node ignores the Windows certificate store and does not trust the .NET dev certificate, so server-side rendering fails on `https://localhost:7270` even after `dotnet dev-certs https --trust`.
**Fix:** call the HTTP endpoint in development, and only apply `UseHttpsRedirection()` outside Development.

### Data loaded but the page stayed empty

**Cause:** zoneless change detection — assigning to a plain component field does not notify Angular.
**Fix:** store the result in a `signal`.

### `NG0908` again, this time in the unit tests

**Cause:** `TestBed.configureTestingModule` defaults to **zone-based** change detection regardless of what the application config says. A spec whose `providers` array omitted `provideZonelessChangeDetection()` threw NG0908 even though the app itself was fine.
**Fix:** every `TestBed` in this project must include `provideZonelessChangeDetection()`.
**Lesson:** a scaffolded spec is not automatically consistent with your app config. Also note the second failure it caused — `httpMock` was left `undefined` because `beforeEach` threw partway, so `afterEach(() => httpMock.verify())` then failed with a confusing `Cannot read properties of undefined`. **One root cause, two error messages.** Fix the first and the second disappears; chasing the second wastes time.

### `/api/members/me` returned 403 to the members it was written for

**Cause:** `MembersController` had `[Authorize(Roles = "Librarian")]` at class level and a plain `[Authorize]` on the `me` action, expecting the action to widen access.
**Fix:** class-level `[Authorize]` with **no** role, and the role declared per action.
**Lesson:** multiple `[Authorize]` attributes combine with **AND, never OR**. An action-level attribute can *narrow* access but can never widen what the class already requires. The "protect by default" instinct is right — just put the bare `[Authorize]` on the class and the roles on the actions.

### `The JSON value could not be converted to ProblemType`

**Cause:** `System.Text.Json` serialises and expects enums as **numbers** by default, so a client sending `"problemType": "WaterDamage"` fails; it would have had to send `2`.
**Fix:** register the converter globally:
```csharp
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```
**Lesson:** worth doing on any API with enums. Numeric enum values in JSON are unreadable, and silently change meaning if anyone reorders the enum.

### The dashboard reported an average rating of 0 for unrated books

**Cause:** `ratings.GetValueOrDefault(bookId)` on a `Dictionary<int, double>` returns `0.0` for a missing key, not `null` — so an unrated book looked like a book everyone rated zero.
**Fix:** `ratings.TryGetValue(bookId, out var avg) ? avg : null`.
**Lesson:** `GetValueOrDefault` returns `default(T)`, and for a value type that is a real value, not absence. When "missing" and "zero" mean different things, `TryGetValue` is the only correct choice. The build was clean and no test failed — only reading the JSON caught it.

### Prerendering hung for ~12 minutes, then failed with `[object Object]`

**Cause:** `app.routes.server.ts` used `RenderMode.Prerender` for `**`. Prerendering executes your components at **build time** to produce static HTML — so the books page tried to call the API during `ng build`.
**Fix:** `RenderMode.Client`. Build time went from 706 seconds and a failure to 7 seconds and success.
**Lesson:** prerendering suits content that is identical for every visitor and known at build time — a marketing page, documentation. It is wrong for live catalogue data, and impossible for authenticated pages: at build time there is no logged-in user and no `localStorage`. **This is also what resolved the SSR/JWT conflict** — with client rendering, `localStorage` and the auth interceptor work normally.

---

## Appendix — Known gaps between spec and code

Be ready to speak to these; knowing your own project's loose ends is a strength.

| Gap | Status |
|---|---|
| `README.md` section 9 describes Hangfire | Not used — replaced by `BackgroundService` (Part 8). Spec needs updating. |
| `README.md` section 11 says `cd API` / `cd client` | Actual folders are `BackendAPI` / `FrontendApp`. |
| `README.md` section 11 says the API is on `https://localhost:5001` | Actually `http://localhost:5041`. |
| `README.md` says Angular CLI 17+ | Actually Angular 20.3, zoneless, with SSR. |
| ~~SSR + `localStorage` JWT~~ | **Resolved.** `app.routes.server.ts` now uses `RenderMode.Client`, so components run only in the browser where `localStorage` exists. |
| Concurrent issue of the last copy | No concurrency token yet. Would need `IsRowVersion()` or a transaction. |
