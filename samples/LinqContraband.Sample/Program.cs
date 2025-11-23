using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore; // Use our Mock

namespace LinqContraband.Sample
{
    public class User
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public List<Order> Orders { get; set; }
        public List<Role> Roles { get; set; }
    }

    public class Order { public int Id { get; set; } }
    public class Role { public int Id { get; set; } }

    class Program
    {
        static void Main(string[] args)
        {
            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), Name = "Alice", Age = 30, Orders = new(), Roles = new() },
                new User { Id = Guid.NewGuid(), Name = "Bob", Age = 25, Orders = new(), Roles = new() }
            }.AsQueryable();

            // LC001: Local Method
            // This calls a local method inside an IQueryable expression, preventing SQL translation.
            Console.WriteLine("Testing LC001...");
            var localResult = users.Where(u => IsAdult(u.Age)).ToList();

            // LC002: Premature Materialization
            // This calls ToList() (materializing all records) before filtering with Where().
            Console.WriteLine("Testing LC002...");
            var prematureResult = users.ToList().Where(u => u.Age > 20).ToList();

            // LC003: Any Over Count
            // This uses Count() > 0 to check existence, which may iterate the whole table.
            Console.WriteLine("Testing LC003...");
            if (users.Count() > 0)
            {
                Console.WriteLine("Users exist");
            }

            // LC004: Guid In Query
            // This generates a new Guid inside the query expression.
            Console.WriteLine("Testing LC004...");
            var guidResult = users.Where(u => u.Id == Guid.NewGuid()).ToList();

            // LC005: Multiple OrderBy
            // This calls OrderBy twice, resetting the first sort instead of chaining with ThenBy.
            Console.WriteLine("Testing LC005...");
            var orderResult = users.OrderBy(u => u.Age).OrderBy(u => u.Name).ToList();

            // LC006: Cartesian Explosion
            // This includes multiple collections in a single query without splitting.
            Console.WriteLine("Testing LC006...");
            var cartesianResult = users.Include(u => u.Orders).Include(u => u.Roles).ToList();
        }

        // Local method for LC001
        static bool IsAdult(int age) => age >= 18;
    }
}
