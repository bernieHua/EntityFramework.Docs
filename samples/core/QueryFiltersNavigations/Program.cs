using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Samples
{
    public class Program
    {
        private static void Main()
        {
            SetupDatabase();

            using (var animalContext = new AnimalContext())
            {
                Console.WriteLine("*****************");
                Console.WriteLine("* Animal lovers *");
                Console.WriteLine("*****************");

                // Jamie and Paul are filtered out.
                // Paul doesn't own any pets. Jamie owns Puffy, but her pet has been filtered out.
                var animalLovers = animalContext.People.ToList();
                DisplayResults(animalLovers);

                Console.WriteLine("**************************************************");
                Console.WriteLine("* Animal lovers and their pets - filters enabled *");
                Console.WriteLine("**************************************************");

                // Jamie and Paul are filtered out.
                // Paul doesn't own any pets. Jamie owns Puffy, but her pet has been filtered out.
                // Simba's favorite toy has also been filtered out.
                // Puffy is filtered out so he doesn't show up as Hati's friend.
                var ownersAndTheirPets = animalContext.People
                    .Include(p => p.Pets)
                    .ThenInclude(p => ((Dog)p).FavoriteToy)
                    .ToList();

                DisplayResults(ownersAndTheirPets);

                Console.WriteLine("*********************************************************");
                Console.WriteLine("* Animal lovers and their pets - query filters disabled *");
                Console.WriteLine("*********************************************************");

                var ownersAndTheirPetsUnfiltered = animalContext.People
                    .IgnoreQueryFilters()
                    .Include(p => p.Pets)
                    .ThenInclude(p => ((Dog)p).FavoriteToy)
                    .ToList();

                DisplayResults(ownersAndTheirPetsUnfiltered);
            }
        }

        private static void DisplayResults(List<Person> people)
        {
            foreach (var person in people)
            {
                Console.WriteLine($"{person.Name}");
                if (person.Pets != null)
                {
                    foreach (var pet in person.Pets)
                    {
                        Console.Write($" - {pet.Name} [{pet.GetType().Name}] ");
                        if (pet is Cat cat)
                        {
                            Console.Write($"| Prefers cardboard boxes: {(cat.PrefersCardboardBoxes ? "Yes" : "No")} ");
                            Console.WriteLine($"| Tolerates: {(cat.Tolerates != null ? cat.Tolerates.Name : "No one")}");
                        }
                        else if (pet is Dog dog)
                        {
                            Console.Write($"| Favorite toy: {(dog.FavoriteToy != null ? dog.FavoriteToy.Name : "None")} ");
                            Console.WriteLine($"| Friend: {(dog.FriendsWith != null ? dog.FriendsWith.Name : "The Owner")}");
                        }
                    }
                }

                Console.WriteLine();
            }
        }

        private static void SetupDatabase()
        {
            using (var animalContext = new AnimalContext())
            {
                if (animalContext.Database.EnsureCreated())
                {
                    var janice = new Person { Name = "Janice" };
                    var jamie = new Person { Name = "Jamie" };
                    var cesar = new Person { Name = "Cesar" };
                    var paul = new Person { Name = "Paul" };
                    var dominic = new Person { Name = "Dominic" };

                    var kibbles = new Cat { Name = "Kibbles", PrefersCardboardBoxes = false, Owner = janice };
                    var sammy = new Cat { Name = "Sammy", PrefersCardboardBoxes = true, Owner = janice };
                    var puffy = new Cat { Name = "Puffy", PrefersCardboardBoxes = true, Owner = jamie };
                    var hati = new Dog { Name = "Hati", FavoriteToy = new Toy { Name = "Squeeky duck" }, Owner = dominic, FriendsWith = puffy };
                    var simba = new Dog { Name = "Simba", FavoriteToy = new Toy { Name = "Bone" }, Owner = cesar, FriendsWith = sammy };
                    puffy.Tolerates = hati;
                    sammy.Tolerates = simba;

                    animalContext.People.AddRange(janice, jamie, cesar, paul, dominic);
                    animalContext.Animals.AddRange(kibbles, sammy, puffy, hati, simba);
                    animalContext.SaveChanges();
                }
            }
        }
    }

    public class AnimalContext : DbContext
    {
        private static readonly ILoggerFactory _loggerFactory
            = LoggerFactory.Create(
                builder =>
                {
                    builder
                        .AddFilter((category, level) =>
                            level == LogLevel.Information
                            && category.EndsWith("Connection", StringComparison.Ordinal))
                        .AddConsole();
                });

        public DbSet<Person> People { get; set; }
        public DbSet<Animal> Animals { get; set; }
        public DbSet<Toy> Toys { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseSqlServer(
                    @"Server=(localdb)\mssqllocaldb;Database=Demo.QueryFiltersNavigations;Trusted_Connection=True;ConnectRetryCount=0;")
                .UseLoggerFactory(_loggerFactory);
        }

        #region Configuration
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Cat>().HasOne(c => c.Tolerates).WithOne(d => d.FriendsWith).HasForeignKey<Cat>(c => c.ToleratesId);
            modelBuilder.Entity<Dog>().HasOne(d => d.FavoriteToy).WithOne(t => t.BelongsTo).HasForeignKey<Toy>(d => d.BelongsToId);

            modelBuilder.Entity<Person>().HasQueryFilter(p => p.Pets.Count > 0);
            modelBuilder.Entity<Animal>().HasQueryFilter(a => !a.Name.StartsWith("P"));
            modelBuilder.Entity<Toy>().HasQueryFilter(a => a.Name.Length > 5);

            // invalid - cycle in query filter definitions
            //modelBuilder.Entity<Animal>().HasQueryFilter(a => a.Owner.Name != "John"); 
        }
        #endregion
    }

    #region Entities
    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Animal> Pets { get; set; }
    }

    public abstract class Animal
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Person Owner { get; set; }
    }

    public class Cat : Animal
    {
        public bool PrefersCardboardBoxes { get; set; }

        public int? ToleratesId { get; set; }

        public Dog Tolerates { get; set; }
    }

    public class Dog : Animal
    {
        public Toy FavoriteToy { get; set; }
        public Cat FriendsWith { get; set; }
    }

    public class Toy
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? BelongsToId { get; set; }
        public Dog BelongsTo { get; set; }
    }
    #endregion
}