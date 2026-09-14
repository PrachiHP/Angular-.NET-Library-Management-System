# Original Roadmap (archived)

The step-by-step notes that lived in `README.md` before it was replaced with the
full project specification. Kept for reference — `TUTORIAL.md` supersedes it.

---

# Angular-.NET-Library-Management-System
Angular .NET Library Management System
 Create Git repo
 Clone it
 dotnet new webapi -n BackendAPI
 ng new FrontendApp
Fix the Cross-Origin (CORS) Issue because your frontend and backend run on different local ports, you must configure .NET to allow traffic from Angular. in BackendAPI/Program.cs. Write and add a policy to achieve this
Run Both Applications Simultaneously
    cd BackendAPI
    dotnet watch run
    cd FrontendApp
    ng serve
Connect Angular to the API in FrontendApp/src/app/app.config.ts and add provideHttpClient()
Create service to call a .NET endpoint
DB integration
Step 1: Install the EF Core NuGet Packages
cd BackendAPI
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet tool install --global dotnet-ef

Step 2: Add your SQL Server Connection String
Open BackendAPI/appsettings.json and add your database connection string at the top level of the JSON file

Step 3: Create a Data Model and DbContext
Create a new folder named Models inside BackendAPI
Create a new folder named Data inside BackendAPI. Inside it, create a file named AppDbContext.cs

Step 4: Register the DbContext in Program.cs
Open your BackendAPI/Program.cs file. Add the database configuration right next to where you added your CORS configuration earlier (before builder.Build())

Step 5: Run the Initial Database Migration
With everything wired up, run these two commands in your BackendAPI terminal tab. This auto-generates your SQL code and builds the database tables directly inside SQL Server:
dotnet ef migrations add InitialCreate
dotnet ef database update

Step 6: Expose the Data via a Controller
To let your Angular app pull this data, replace or add an API controller. Create a file named ProductsController.cs inside the existing Controllers folder
