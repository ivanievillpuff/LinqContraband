using System;
using System.Collections.Generic;
using System.Linq;
using LinqContraband.Sample.Data;
using Microsoft.EntityFrameworkCore;

namespace LinqContraband.Sample.Samples.LC009_MissingAsNoTracking
{
    /// <summary>
    /// Demonstrates the "The Tracking Tax" violation (LC009).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>The Crime:</strong> Fetching entities for read-only purposes without using <c>AsNoTracking()</c>.
    /// </para>
    /// <para>
    /// <strong>Why it's bad:</strong> By default, EF Core creates a "snapshot" of every entity it retrieves 
    /// to detect changes later. This consumes significant CPU and memory. If you are only reading data 
    /// (e.g., for an API response or dashboard) and not modifying it, this overhead is wasted.
    /// </para>
    /// <para>
    /// <strong>The Fix:</strong> Add <c>.AsNoTracking()</c> to queries that are strictly for read-only operations. 
    /// If you need identity resolution (to avoid duplicate instances of the same entity in the graph) but not tracking, 
    /// use <c>.AsNoTrackingWithIdentityResolution()</c>.
    /// </para>
    /// </remarks>
    public class MissingAsNoTrackingSample
    {
        /// <summary>
        /// Runs the sample demonstrating missing tracking optimizations.
        /// </summary>
        /// <param name="users">The source queryable of users.</param>
        public static void Run(IQueryable<User> users)
        {
            Console.WriteLine("Testing LC009...");
            
            GetUsersReadOnly(users);
            GetUsersWithIdentityResolution(users);
        }

        /// <summary>
        /// Fetches users without disabling tracking, triggering the violation.
        /// </summary>
        static List<User> GetUsersReadOnly(IQueryable<User> users)
        {
            // VIOLATION: Returning entities from read-only context without AsNoTracking.
            // EF Core tracks these entities, wasting resources.
            return users.Where(u => u.Age > 18).ToList();
        }

        /// <summary>
        /// Correctly uses identity resolution without full tracking.
        /// </summary>
        static List<User> GetUsersWithIdentityResolution(IQueryable<User> users)
        {
            // SAFE: Using AsNoTrackingWithIdentityResolution is explicitly allowed.
            return users.AsNoTrackingWithIdentityResolution().Where(u => u.Age > 21).ToList();
        }
    }
}
