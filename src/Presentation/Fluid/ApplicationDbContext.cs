

// Added using directive for the models

namespace Fluid
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Define DbSets for existing models
        public DbSet<Entity> Entities { get; set; }
        public DbSet<EntityColumn> EntityColumns { get; set; }
    }
}
