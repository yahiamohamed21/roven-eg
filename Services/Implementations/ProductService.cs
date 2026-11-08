using Stockify.Data;
using Stockify.Services.Interfaces;

namespace Stockify.Services.Implementations;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _context;

    public ProductService(ApplicationDbContext context)
    {
        _context = context;
    }
}

