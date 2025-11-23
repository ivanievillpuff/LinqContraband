using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using CodeFixTest =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixTest<
        LinqContraband.Analyzers.LC002_PrematureMaterialization.PrematureMaterializationAnalyzer,
        LinqContraband.Analyzers.LC002_PrematureMaterialization.PrematureMaterializationFixer,
        Microsoft.CodeAnalysis.Testing.Verifiers.XUnitVerifier>;

namespace LinqContraband.Tests.Analyzers.LC002_PrematureMaterialization;

public class PrematureMaterializationFixerTests
{
    private const string Usings = @"
using System;
using System.Linq;
using System.Collections.Generic;
using TestNamespace;
";

    private const string MockNamespace = @"
namespace TestNamespace
{
    public class User
    {
        public int Age { get; set; }
    }

    public class DbContext
    {
        public IQueryable<User> Users => new List<User>().AsQueryable();
    }
}";

    [Fact]
    public async Task FixCrime_MovesWhereBeforeToList()
    {
        var test = Usings + @"
class Program
{
    void Main()
    {
        var db = new DbContext();
        var query = db.Users.ToList().Where(x => x.Age > 18);
    }
}
" + MockNamespace;

        var fixedCode = Usings + @"
class Program
{
    void Main()
    {
        var db = new DbContext();
        var query = db.Users.Where(x => x.Age > 18).ToList();
    }
}
" + MockNamespace;

        var testObj = new CodeFixTest
        {
            TestCode = test,
            FixedCode = fixedCode,
        };
        
        // Expect diagnostic on the whole 'db.Users.ToList().Where(...)' expression
        testObj.ExpectedDiagnostics.Add(new DiagnosticResult("LC002", DiagnosticSeverity.Warning)
            .WithSpan(12, 21, 12, 61)
            .WithArguments("Where"));

        await testObj.RunAsync();
    }

    [Fact]
    public async Task FixCrime_AvoidsDoubleToList()
    {
        var test = Usings + @"
class Program
{
    void Main()
    {
        var db = new DbContext();
        // Double ToList scenario
        var query = db.Users.ToList().Where(x => x.Age > 18).ToList();
    }
}
" + MockNamespace;

        var fixedCode = Usings + @"
class Program
{
    void Main()
    {
        var db = new DbContext();
        // Double ToList scenario
        var query = db.Users.Where(x => x.Age > 18).ToList();
    }
}
" + MockNamespace;

        var testObj = new CodeFixTest
        {
            TestCode = test,
            FixedCode = fixedCode,
        };

        // Diagnostic is on the inner expression: db.Users.ToList().Where(...)
        // Line 13
        testObj.ExpectedDiagnostics.Add(new DiagnosticResult("LC002", DiagnosticSeverity.Warning)
            .WithSpan(13, 21, 13, 61)
            .WithArguments("Where"));

        await testObj.RunAsync();
    }

    [Fact]
    public async Task FixCrime_AvoidsToListThenToArray()
    {
        var test = Usings + @"
class Program
{
    void Main()
    {
        var db = new DbContext();
        var query = db.Users.ToList().Where(x => x.Age > 18).ToArray();
    }
}
" + MockNamespace;

        var fixedCode = Usings + @"
class Program
{
    void Main()
    {
        var db = new DbContext();
        var query = db.Users.Where(x => x.Age > 18).ToArray();
    }
}
" + MockNamespace;

        var testObj = new CodeFixTest
        {
            TestCode = test,
            FixedCode = fixedCode,
        };

        testObj.ExpectedDiagnostics.Add(new DiagnosticResult("LC002", DiagnosticSeverity.Warning)
            .WithSpan(12, 21, 12, 61)
            .WithArguments("Where"));

        await testObj.RunAsync();
    }
}