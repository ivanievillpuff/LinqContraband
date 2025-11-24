using LinqContraband.Sample.Data;

namespace LinqContraband.Sample.Samples.LC013_DisposedContextQuery;

/// <summary>
///     Demonstrates the "Disposed Context Query" violation (LC013).
/// </summary>
/// <remarks>
///     <para>
///         <strong>The Crime:</strong> Returning a deferred query (e.g., <c>IQueryable</c>, <c>IAsyncEnumerable</c>)
///         that was built from a <c>DbContext</c> which is disposed within the same scope (e.g., via <c>using</c>).
///     </para>
///     <para>
///         <strong>Why it's bad:</strong> EF Core queries are deferred execution. They don't run when you define them;
///         they run when you iterate them. If you return the query, the caller will try to iterate it later.
///         By that time, the <c>using</c> block has finished, and the <c>DbContext</c> is disposed.
///         This causes an <c>ObjectDisposedException</c> or other runtime failures.
///     </para>
///     <para>
///         <strong>Advanced Detection:</strong> This analyzer also detects these time bombs hiding inside
///         conditional operators (<c>? :</c>), null-coalescing operators (<c>??</c>), and switch expressions.
///     </para>
///     <para>
///         <strong>The Fix:</strong> Materialize the result (e.g., <c>ToList()</c>, <c>ToArray()</c>) while the context
///         is still alive, or ensure the context's lifetime is managed externally (e.g., via dependency injection).
///     </para>
/// </remarks>
public class DisposedContextQuerySample
{
    /// <summary>
    ///     Demonstrates a violation where the query bombs upon use.
    /// </summary>
    /// <returns>A query connected to a dead context.</returns>
    public IQueryable<User> GetUsers_Violation()
    {
        using var db = new AppDbContext();
        // VIOLATION: Returning a query from a context that is about to be disposed.
        // The query is not executed here. It is a ticking time bomb.
        return db.Users.Where(u => u.Age > 18);
    }

    /// <summary>
    ///     Demonstrates a violation inside a conditional expression.
    /// </summary>
    public IQueryable<User> GetUsers_Branching_Violation(bool filterAdults)
    {
        using var db = new AppDbContext();
        // VIOLATION: Both branches return a query from the disposed context.
        return filterAdults
            ? db.Users.Where(u => u.Age >= 18)
            : db.Users;
    }

    /// <summary>
    ///     Demonstrates a violation hidden in a null-coalescing operator.
    /// </summary>
    public IQueryable<User> GetUsers_Coalesce_Violation(IQueryable<User> existingQuery)
    {
        using var db = new AppDbContext();
        // VIOLATION: If existingQuery is null, we return a dead query.
        return existingQuery ?? db.Users;
    }

    /// <summary>
    ///     Demonstrates the correct approach (Materialization).
    /// </summary>
    /// <returns>A safe, in-memory list of users.</returns>
    public List<User> GetUsers_Valid()
    {
        using var db = new AppDbContext();
        // SAFE: Materialized before return using ToList().
        // The data is fetched while 'db' is alive.
        return db.Users.Where(u => u.Age > 18).ToList();
    }

    /// <summary>
    ///     Demonstrates the correct approach (External Context).
    /// </summary>
    /// <param name="db">An externally managed context.</param>
    /// <returns>A query that remains valid as long as the caller keeps 'db' alive.</returns>
    public IQueryable<User> GetUsers_External(AppDbContext db)
    {
        // SAFE: Context lifetime is managed by the caller (e.g., DI container).
        return db.Users.Where(u => u.Age > 18);
    }
}
