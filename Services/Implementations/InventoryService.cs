using Stockify.Data;
using Stockify.Services.Interfaces;

namespace Stockify.Services.Implementations;

public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _context;

    public InventoryService(ApplicationDbContext context)
    {
        _context = context;
    }
}

