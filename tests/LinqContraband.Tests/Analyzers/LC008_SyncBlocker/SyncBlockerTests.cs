using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    LinqContraband.Analyzers.LC008_SyncBlocker.SyncBlockerAnalyzer>;

namespace LinqContraband.Tests.Analyzers.LC008_SyncBlocker;

public class SyncBlockerTests
{
    private const string Usings = @"
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TestNamespace;
";

    private const string MockNamespace = @"
namespace Microsoft.EntityFrameworkCore
{
    public class DbContext : IDisposable
    {
        public void Dispose() { }
        public DbSet<T> Set<T>() where T : class => new DbSet<T>();
        public int SaveChanges() => 0;
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
    }

    public class DbSet<T> : IQueryable<T> where T : class
    {
        public Type ElementType => typeof(T);
        public System.Linq.Expressions.Expression Expression => System.Linq.Expressions.Expression.Constant(this);
        public IQueryProvider Provider => null;
        public IEnumerator<T> GetEnumerator() => null;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null;
        
        public T Find(params object[] keyValues) => null;
        public Task<T> FindAsync(params object[] keyValues) => Task.FromResult<T>(null);
    }

    public static class EntityFrameworkQueryableExtensions
    {
        public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source) => Task.FromResult(new List<T>());
        public static Task<int> CountAsync<T>(this IQueryable<T> source) => Task.FromResult(0);
        public static Task<T> FirstAsync<T>(this IQueryable<T> source) => Task.FromResult<T>(default);
    }
}

namespace TestNamespace
{
    public class User { public int Id { get; set; } }
    
    public class MyDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
    }
}";

    [Fact]
    public async Task TestCrime_AsyncMethod_CallingToList_TriggersDiagnostic()
    {
        var test = Usings + @"
class Program
{
    async Task Main()
    {
        var db = new MyDbContext();
        
        // Crime: Sync ToList inside async method
        var users = db.Users.ToList();
        await Task.Delay(1);
    }
}
" + MockNamespace;

        var expected = VerifyCS.Diagnostic("LC008")
            .WithSpan(16, 21, 16, 38) // "ToList"
            .WithArguments("ToList", "ToListAsync");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task TestCrime_AsyncMethod_CallingSaveChanges_TriggersDiagnostic()
    {
        var test = Usings + @"
class Program
{
    async Task DoWork()
    {
        var db = new MyDbContext();
        // Crime: Sync SaveChanges
        db.SaveChanges();
        await Task.Delay(1);
    }
}
" + MockNamespace;

        var expected = VerifyCS.Diagnostic("LC008")
            .WithSpan(15, 9, 15, 25) // "SaveChanges"
            .WithArguments("SaveChanges", "SaveChangesAsync");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task TestInnocent_AsyncMethod_CallingToListAsync_NoDiagnostic()
    {
        var test = Usings + @"
class Program
{
    async Task Main()
    {
        var db = new MyDbContext();
        // Innocent: Async version
        var users = await db.Users.ToListAsync();
    }
}
" + MockNamespace;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task TestInnocent_SyncMethod_CallingToList_NoDiagnostic()
    {
        var test = Usings + @"
class Program
{
    void Main()
    {
        var db = new MyDbContext();
        // Innocent: Sync method, so sync I/O is allowed (though not ideal, it's not sync-over-async)
        var users = db.Users.ToList();
    }
}
" + MockNamespace;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task TestCrime_AsyncMethod_CallingCount_TriggersDiagnostic()
    {
        var test = Usings + @"
class Program
{
    public async Task<int> GetCount()
    {
        var db = new MyDbContext();
        // Crime
        return db.Users.Count();
    }
}
" + MockNamespace;

        var expected = VerifyCS.Diagnostic("LC008")
            .WithSpan(15, 16, 15, 32)
            .WithArguments("Count", "CountAsync");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }
}
