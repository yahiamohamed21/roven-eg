using Stockify.Data;
using Stockify.Services.Interfaces;

namespace Stockify.Services.Implementations;

public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _context;

    public CustomerService(ApplicationDbContext context)
    {
        _context = context;
    }
}

