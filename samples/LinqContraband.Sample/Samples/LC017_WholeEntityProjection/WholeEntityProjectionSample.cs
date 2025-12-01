using LinqContraband.Sample.Data;
using System.Linq;

namespace LinqContraband.Sample.Samples.LC017_WholeEntityProjection;

/// <summary>
///     Demonstrates the "Whole Entity Projection" violation (LC017).
/// </summary>
/// <remarks>
///     <para>
///         <strong>The Crime:</strong> Loading entire entities with many properties when only a few are actually used.
///     </para>
///     <para>
///         <strong>Why it's bad:</strong> When you load an entity without projection, EF Core retrieves ALL columns
///         from the database. For large entities with 10+ properties, this wastes bandwidth (more data over the wire),
///         memory (allocating unused properties), and CPU (change tracking overhead for data you don't need).
///     </para>
///     <para>
///         <strong>The Fix:</strong> Use <c>.Select()</c> to project only the properties you need. This tells EF Core
///         to generate SQL that retrieves only the required columns.
///     </para>
/// </remarks>
public class WholeEntityProjectionSample
{
    /// <summary>
    ///     Runs the sample demonstrating whole entity projection issues.
    /// </summary>
    /// <param name="db">The database context.</param>
    public static void Run(AppDbContext db)
    {
        Console.WriteLine("Testing LC017...");

        ProcessNames(db);
        ProcessNamesCorrectly(db);
    }

    /// <summary>
    ///     VIOLATION: Loads all 12 columns but only accesses the Name property.
    ///     Should trigger LC017.
    /// </summary>
    private static void ProcessNames(AppDbContext db)
    {
        // VIOLATION: Loading entire entity (12 properties) but only using Name
        // This wastes bandwidth by transferring 11 unused columns per row
        var allEntities = db.LargeEntities.Where(e => e.Price > 100).ToList();
        foreach (var e in allEntities)
        {
            Console.WriteLine(e.Name); // Only Name is ever accessed!
        }
    }

    /// <summary>
    ///     CORRECT: Uses .Select() to project only the needed property.
    /// </summary>
    private static void ProcessNamesCorrectly(AppDbContext db)
    {
        // CORRECT: Projects only the Name column
        // SQL: SELECT Name FROM LargeEntities WHERE Price > 100
        var names = db.LargeEntities
            .Where(e => e.Price > 100)
            .Select(e => e.Name)
            .ToList();

        foreach (var name in names)
        {
            Console.WriteLine(name);
        }
    }
}
