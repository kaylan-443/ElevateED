namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;
    using ElevateED.Models;

    internal sealed class Configuration : DbMigrationsConfiguration<ElevateED.Models.ElevateEDContext>
    {
        public Configuration()
        {
            // Enable automatic migrations for Azure (safe - won't delete data)
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = false;
        }

        protected override void Seed(ElevateED.Models.ElevateEDContext context)
        {
            // Seed runs after migrations - admin is handled in DatabaseConfig
        }
    }
}