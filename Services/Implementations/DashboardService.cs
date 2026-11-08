using Stockify.Data;
using Stockify.Services.Interfaces;

namespace Stockify.Services.Implementations;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;

    public DashboardService(ApplicationDbContext context)
    {
        _context = context;
    }
}

