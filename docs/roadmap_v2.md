# LinqContraband v2 Roadmap

This document outlines the plan for the next set of analyzers to be added to LinqContraband.
Each feature will follow a strict **TDD -> Analyzer -> Fixer -> Sample** workflow.

## 1. LC006: The "Cartesian Exploder" (Multiple Collection Includes)
**The Crime:** Chaining multiple `.Include()` calls for **collection** navigation properties in a single query without splitting.
**Why:** Causes geometric data explosion (rows = Parent * Children1 * Children2).
**Logic:**
- Detect `IQueryable` chains.
- Count invocations of `Include` or `ThenInclude` where the navigation property is a collection (IEnumerable/List/etc).
- If count > 1 and `.AsSplitQuery()` is missing, trigger warning.
**The Fix:** Append `.AsSplitQuery()` to the chain.

## 2. LC007: The "N+1 Looper" (Query Inside Loop)
**The Crime:** Executing a query (accessing `DbContext` set or calling immediate execution methods like `ToList`, `Count`, `First`) inside a loop statement (`for`, `foreach`, `while`, `do`).
**Why:** Causes a database roundtrip for every iteration. 100 items = 100 queries.
**Logic:**
- Detect loop bodies.
- Scan for `InvocationExpression` or `MemberAccess` referencing a `DbContext` or `IQueryable`.
**The Fix:** (Complex) Suggest moving query outside loop (may require manual intervention, potentially just a diagnostic with no auto-fix).

## 3. LC008: The "Sync Blocker" (Synchronous I/O in Async)
**The Crime:** Calling synchronous materializers (`ToList`, `First`, `Count`, `SaveChanges`) inside an `async` method.
**Why:** Blocks ThreadPool threads, reducing server throughput (Sync-over-Async).
**Logic:**
- Check if containing method has `async` modifier.
- Detect synchronous EF Core methods (e.g., `ToList` instead of `ToListAsync`).
**The Fix:** Replace with the `Async` counterpart and `await` it.

## 4. LC009: The "Tracking Tax" (Missing AsNoTracking)
**The Crime:** Returning entities from a method that doesn't call `SaveChanges`, without using `.AsNoTracking()`.
**Why:** Change tracking consumes CPU/Memory unnecessarily for read-only scenarios.
**Logic:**
- Heuristic: If a method returns `List<Entity>` or `Entity` and does *not* call `SaveChanges()` (or `Add`/`Update`/`Remove`) within its scope.
- Check if the query chain lacks `.AsNoTracking()`.
**The Fix:** Inject `.AsNoTracking()` into the query chain.
