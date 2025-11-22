using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using CodeFixTest = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixTest<LinqContraband.LocalMethodAnalyzer, LinqContraband.LocalMethodFixer, Microsoft.CodeAnalysis.Testing.Verifiers.XUnitVerifier>;

namespace LinqContraband.Tests
{
    public class LocalMethodFixerTests
    {
        private const string Usings = @"
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using TestNamespace;
";

        private const string MockNamespace = @"
namespace TestNamespace
{
    public class User
    {
        public int Age { get; set; }
        public DateTime Dob { get; set; }
    }

    public class DbContext
    {
        public IQueryable<User> Users => new List<User>().AsQueryable();
    }
}";

        [Fact]
        public async Task FixCrime_ExtractsToVariable_EvenIfInvalid()
        {
            var test = Usings + @"
class Program
{
    void Main()
    {
        var db = new DbContext();
        var query = db.Users.Where(u => CalculateAge(u.Dob) > 18);
    }

    int CalculateAge(DateTime dob) => 0;
}
" + MockNamespace;

            var fixedCode = Usings + @"
class Program
{
    void Main()
    {
        var db = new DbContext();
        var value = CalculateAge(u.Dob);
        var query = db.Users.Where(u => value > 18);
    }

    int CalculateAge(DateTime dob) => 0;
}
" + MockNamespace;

            var testObj = new CodeFixTest
            {
                TestCode = test,
                FixedCode = fixedCode,
                CompilerDiagnostics = CompilerDiagnostics.None 
            };
            
            testObj.ExpectedDiagnostics.Add(new DiagnosticResult("LC001", DiagnosticSeverity.Warning)
                .WithSpan(13, 41, 13, 60)
                .WithArguments("CalculateAge"));

            await testObj.RunAsync();
        }
    }
}
