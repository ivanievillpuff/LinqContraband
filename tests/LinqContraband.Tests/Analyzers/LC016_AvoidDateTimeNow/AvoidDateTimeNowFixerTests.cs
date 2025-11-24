using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    LinqContraband.Analyzers.LC016_AvoidDateTimeNow.AvoidDateTimeNowAnalyzer,
    LinqContraband.Analyzers.LC016_AvoidDateTimeNow.AvoidDateTimeNowFixer>;

namespace LinqContraband.Tests.Analyzers.LC016_AvoidDateTimeNow;

public class AvoidDateTimeNowFixerTests
{
    [Fact]
    public async Task FixDateTimeNow_InWhere()
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
        var q = users.Where(u => u.Dob < {|LC016:DateTime.Now|});
    }
}";

        var fix = @"
using System;
using System.Linq;
using System.Collections.Generic;

class User { public DateTime Dob { get; set; } }

class Test
{
    void Method(IQueryable<User> users)
    {
        var now = DateTime.Now;
        var q = users.Where(u => u.Dob < now);
    }
}";
        await VerifyCS.VerifyCodeFixAsync(test, fix);
    }

    [Fact]
    public async Task FixDateTimeUtcNow_InWhere()
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
        var q = users.Where(u => u.Dob < {|LC016:DateTime.UtcNow|});
    }
}";

        var fix = @"
using System;
using System.Linq;
using System.Collections.Generic;

class User { public DateTime Dob { get; set; } }

class Test
{
    void Method(IQueryable<User> users)
    {
        var utcNow = DateTime.UtcNow;
        var q = users.Where(u => u.Dob < utcNow);
    }
}";
        await VerifyCS.VerifyCodeFixAsync(test, fix);
    }

    [Fact]
    public async Task FixNameCollision()
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
        var now = 1;
        var q = users.Where(u => u.Dob < {|LC016:DateTime.Now|});
    }
}";

        var fix = @"
using System;
using System.Linq;
using System.Collections.Generic;

class User { public DateTime Dob { get; set; } }

class Test
{
    void Method(IQueryable<User> users)
    {
        var now = 1;
        var now1 = DateTime.Now;
        var q = users.Where(u => u.Dob < now1);
    }
}";
        await VerifyCS.VerifyCodeFixAsync(test, fix);
    }
}
