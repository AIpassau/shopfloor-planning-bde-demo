# Developer Guide

This document explains the internal structure of the Shopfloor Planning & BDE Demo for reviewers and developers.

## Project Structure

```text
ShopfloorPlanningBdeDemo/
├─ App.xaml
├─ App.xaml.cs
├─ AssemblyInfo.cs
├─ MainWindow.xaml
├─ MainWindow.xaml.cs
├─ ShopfloorPlanningBdeDemo.csproj
├─ README.md
├─ DEVELOPER.md
├─ .gitignore
└─ screenshots/
   ├─ 01-home.png
   ├─ 02-bde-terminal.png
   └─ 03-ai-report.png
```

## Architecture Overview

The application is implemented as a single-window WPF desktop app.

```text
MainWindow.xaml
    Defines the UI layout, sidebar navigation and four views:
    Home, BDE Terminal, SQLite Database and LLM Report Analysis.

MainWindow.xaml.cs
    Contains view switching, BDE validation, SQLite persistence,
    feedback history loading and LLM API integration.

SQLite database
    Created automatically at runtime as shopfloor_bde.db.
```

## Views

The sidebar navigation switches between four named WPF elements:

| View | XAML Name | Purpose |
|---|---|---|
| Home | `HomeView` | Manufacturing orders and machine status board |
| BDE Terminal | `BdeView` | Feedback entry form |
| SQLite Database | `DatabaseView` | Recent persisted feedback records |
| LLM Report Analysis | `AiView` | AI-based production report |

Navigation is handled in `MainWindow.xaml.cs`:

```csharp
ShowHomeView()
ShowBdeView()
ShowDatabaseView()
ShowAiView()
SetActiveNavigation(Button activeButton)
```

## Database Layer

The database path is generated from the app base directory:

```csharp
_databasePath = Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "shopfloor_bde.db");
```

The table is created automatically if it does not exist:

```sql
CREATE TABLE IF NOT EXISTS FeedbackRecords (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CreatedAt TEXT NOT NULL,
    OrderNumber TEXT NOT NULL,
    GoodQuantity INTEGER NOT NULL,
    ScrapQuantity INTEGER NOT NULL,
    Comment TEXT
);
```

Database access uses `Microsoft.Data.Sqlite`.

Important methods:

```csharp
InitializeDatabase()
SaveFeedbackRecord(...)
LoadFeedbackHistory()
LoadFeedbackDataForAi()
```

SQL inserts use parameters instead of string concatenation.

## LLM Integration

The LLM report feature is implemented in C# with `HttpClient`.

Important methods:

```csharp
GenerateAiReport_Click(...)
LoadFeedbackDataForAi()
GenerateAiReportAsync(...)
ExtractResponseText(...)
```

The current implementation calls:

```text
POST https://api.openai.com/v1/responses
```

with model:

```text
gpt-4.1-mini
```

The API key is read from:

```csharp
Environment.GetEnvironmentVariable("OPENAI_API_KEY")
```

This keeps credentials out of source control.

## Language Switching

The app has a lightweight German / English language switch. It updates text labels in place using:

```csharp
ToggleLanguage_Click(...)
ApplyLanguage()
```

The current language is tracked with:

```csharp
private bool _isEnglish = false;
```

## Build

Build from Visual Studio or PowerShell:

```powershell
dotnet build
```

Run:

```powershell
dotnet run
```

Because this is a WPF project, it targets Windows:

```xml
<TargetFramework>net10.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
```

## Publish A Windows Executable

For a self-contained Windows x64 build:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

The output is created under:

```text
bin\Release\net10.0-windows\win-x64\publish
```

This folder can be zipped and attached to a GitHub Release.

Do not commit the published output to the source repository.

## GitHub Release Recommendation

Use the repository for source code and documentation. Use GitHub Releases for a downloadable executable package.

Suggested release asset:

```text
ShopfloorPlanningBdeDemo-win-x64.zip
```

## Security Notes

- Do not commit API keys.
- Do not commit `.env` files.
- Do not commit generated database files.
- Do not commit `bin/`, `obj/` or `.vs/`.
- The AI report only sends recent BDE feedback rows selected by the application.

## Possible Next Improvements

- Add CSV export for feedback records.
- Add a real data grid for database history.
- Add configurable LLM provider support for OpenAI and OpenRouter.
- Add unit tests for validation and prompt creation.
- Move data access into a separate repository class if the project grows.
