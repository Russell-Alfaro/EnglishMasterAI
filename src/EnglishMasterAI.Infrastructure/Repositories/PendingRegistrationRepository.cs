using EnglishMasterAI.Application.Ports.Output;
using EnglishMasterAI.Domain.Entities;
using EnglishMasterAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishMasterAI.Infrastructure.Repositories;

public class PendingRegistrationRepository : IPendingRegistrationRepository
{
    private readonly AppDbContext _context;

    public PendingRegistrationRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PendingRegistration> AddAsync(
        PendingRegistration registration, CancellationToken cancellationToken = default)
    {
        await _context.PendingRegistrations.AddAsync(registration, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return registration;
    }

    public async Task<PendingRegistration?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PendingRegistrations
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PendingRegistration>> GetByStatusAsync(
        RegistrationStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.PendingRegistrations
            .Where(r => r.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsPendingOrApprovedByEmailAsync(
        string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _context.PendingRegistrations
            .AnyAsync(r => r.Email == normalizedEmail &&
                           (r.Status == RegistrationStatus.Pending || r.Status == RegistrationStatus.Approved),
                      cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}