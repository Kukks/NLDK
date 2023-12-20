using Microsoft.EntityFrameworkCore;

namespace NLDK;

public interface BaseEntity
{
    static abstract void OnModelCreating(ModelBuilder modelBuilder);
}