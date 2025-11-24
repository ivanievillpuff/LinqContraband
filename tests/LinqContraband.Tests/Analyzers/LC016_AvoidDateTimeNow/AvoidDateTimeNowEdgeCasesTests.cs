using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    LinqContraband.Analyzers.LC016_AvoidDateTimeNow.AvoidDateTimeNowAnalyzer>;

namespace LinqContraband.Tests.Analyzers.LC016_AvoidDateTimeNow;

public class AvoidDateTimeNowEdgeCasesTests
{
    [Fact]
    public async Task Diagnostic_DateTimeOffsetUtcNow()
    {
        var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

class User { public DateTimeOffset CreatedAt { get; set; } }

class Test
{
    void Method(IQueryable<User> users)
    {
        var q = users.Where(u => u.CreatedAt < {|LC016:DateTimeOffset.UtcNow|});
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_ChainedMethod_AddDays()
    {
        var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

class User { public DateTime Dob { get; set; } }

class Test
{
    void Method(IQueryable<User> users)
    {
        // DateTime.Now.AddDays(1) - Now is still the root cause
        var q = users.Where(u => u.Dob < {|LC016:DateTime.Now|}.AddDays(1));
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_InsideSelectProjection()
    {
        var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

class User { public DateTime Dob { get; set; } }

class Test
{
    void Method(IQueryable<User> users)
    {
        // Projection containing DateTime.Now is also bad for caching if compiled
        var q = users.Select(u => new { IsRecent = u.Dob > {|LC016:DateTime.Now|} });
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_InsideOrderBy()
    {
        var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

class User { public DateTime Dob { get; set; } }

class Test
{
    void Method(IQueryable<User> users)
    {
        // Sort by difference from Now
        var q = users.OrderBy(u => {|LC016:DateTime.Now|} - u.Dob);
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_PassedAsMethodArgument()
    {
        var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

class User { public DateTime Dob { get; set; } }

class Test
{
    void Method(IQueryable<User> users)
    {
        // Passing Now to a method (EF.Functions or custom)
        var q = users.Where(u => CustomFunc(u.Dob, {|LC016:DateTime.Now|}));
    }

    bool CustomFunc(DateTime d1, DateTime d2) => true;
}
";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_InsideSkipWhile()
    {
        var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

class User { public DateTime Dob { get; set; } }

class Test
{
    void Method(IQueryable<User> users)
    {
        var q = users.SkipWhile(u => u.Dob < {|LC016:DateTime.Now|});
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_InsideJoin_KeySelector()
    {
        var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

class User { public DateTime Dob { get; set; } }
class Order { public DateTime Date { get; set; } }

class Test
{
    void Method(IQueryable<User> users, IQueryable<Order> orders)
    {
        // Join key selector using DateTime.Now (weird but possible)
        // The outer key selector uses DateTime.Now
        var q = users.Join(orders, 
            u => {|LC016:DateTime.Now|}, 
            o => o.Date, 
            (u, o) => u);
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_TernaryOperator()
    {
        var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

class User { public DateTime Dob { get; set; } }

class Test
{
    void Method(IQueryable<User> users, bool condition)
    {
        var q = users.Where(u => u.Dob < (condition ? {|LC016:DateTime.Now|} : {|LC016:DateTime.UtcNow|}));
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
