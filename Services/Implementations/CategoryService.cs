using Stockify.Data;
using Stockify.Services.Interfaces;

namespace Stockify.Services.Implementations;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _context;

    public CategoryService(ApplicationDbContext context)
    {
        _context = context;
    }
}

