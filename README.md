# LinqContraband

**LinqContraband** is a Roslyn Analyzer for .NET that prevents common Entity Framework Core performance pitfalls by detecting client-side evaluation risks and premature query materialization.

## Rules

### LC001: The Local Method Smuggler
Detects usage of local methods inside `IQueryable` expressions that cannot be translated to SQL.

**The Crime:**
```csharp
// CalculateAge is a local C# method, EF Core cannot translate it to SQL.
var query = db.Users.Where(u => CalculateAge(u.Dob) > 18);
```

**The Fix:** Extract the logic or variable outside the query, or use a supported translation method.

### LC002: Premature Materialization
Detects when filtering logic (`Where`) is applied *after* materialization (`ToList`, `ToArray`), causing the entire table to be fetched into memory.

**The Crime:**
```csharp
// ToList() executes the query (SELECT * FROM Users), fetching ALL rows.
// Where() then filters in memory.
var query = db.Users.ToList().Where(u => u.Age > 18);
```

**The Fix:** Move filtering before materialization.
```csharp
// SELECT * FROM Users WHERE Age > 18
var query = db.Users.Where(u => u.Age > 18).ToList();
```

### LC003: Prefer Any() over Count() > 0
Detects usage of `Count() > 0` or `0 < Count()` to check for existence.

**The Crime:**
```csharp
// Iterates the entire result set to count elements.
var hasUsers = db.Users.Count() > 0;
```

**The Fix:** Use `Any()` which returns as soon as a match is found.
```csharp
var hasUsers = db.Users.Any();
```

### LC004: Avoid Guid generation inside IQueryable
Detects `Guid.NewGuid()` or `new Guid(...)` inside `IQueryable` expressions.

**The Crime:**
```csharp
// May fail translation or cause client-side evaluation.
var query = db.Users.Where(u => u.Id == Guid.NewGuid());
```

**The Fix:** Generate the Guid outside the query.
```csharp
var newId = Guid.NewGuid();
var query = db.Users.Where(u => u.Id == newId);
```

### LC005: Multiple OrderBy calls
Detects consecutive `OrderBy` or `OrderByDescending` calls, which resets the sorting.

**The Crime:**
```csharp
// The first OrderBy is ignored/overwritten by the second one.
var query = db.Users.OrderBy(u => u.Name).OrderBy(u => u.Age);
```

**The Fix:** Use `ThenBy` or `ThenByDescending` to chain sorts.
```csharp
var query = db.Users.OrderBy(u => u.Name).ThenBy(u => u.Age);
```

## Installation

Install via NuGet (Package generation coming soon).

## Development

### Requirements
- .NET 10.0 SDK (compatible with .NET 8/9 projects)

### Building and Testing
```bash
dotnet build
dotnet test
```


