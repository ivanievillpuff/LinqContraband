using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    LinqContraband.Analyzers.LC002_PrematureMaterialization.PrematureMaterializationAnalyzer>;

namespace LinqContraband.Tests.Analyzers.LC002_PrematureMaterialization;

public class PrematureMaterializationEdgeCasesTests
{
    private const string Usings = @"
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore; // Mock
using TestNamespace;
";

    private const string MockClasses = @"
namespace Microsoft.EntityFrameworkCore
{
    public class DbContext : IDisposable { public void Dispose() {} }
    public class DbSet<T> : IQueryable<T>
    {
        public Type ElementType => typeof(T);
        public System.Linq.Expressions.Expression Expression => System.Linq.Expressions.Expression.Constant(this);
        public IQueryProvider Provider => null;
        public IEnumerator<T> GetEnumerator() => null;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null;
    }
}

namespace TestNamespace
{
    public class User { public int Id { get; set; } public int Age { get; set; } }
    public class AppDbContext : Microsoft.EntityFrameworkCore.DbContext 
    { 
        public Microsoft.EntityFrameworkCore.DbSet<User> Users { get; set; } 
    }
}";

    [Fact]
    public async Task NewList_ThenWhere_ShouldTriggerLC002()
    {
        var test = Usings + MockClasses + @"
namespace TestApp 
{
    public class Program
    {
        public void Main()
        {
            using var db = new AppDbContext();
            
            // Constructing a List eagerly, then filtering in memory.
            var query = {|LC002:new List<User>(db.Users).Where(u => u.Age > 18)|};
        }
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NewHashSet_ThenCount_ShouldTriggerLC002()
    {
        var test = Usings + MockClasses + @"
namespace TestApp 
{
    public class Program
    {
        public void Main()
        {
            using var db = new AppDbContext();
            
            var count = {|LC002:new HashSet<User>(db.Users).Count(u => u.Age > 18)|};
        }
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}