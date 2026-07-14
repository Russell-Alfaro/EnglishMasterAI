using EnglishMasterAI.Domain.Entities;

namespace EnglishMasterAI.Application.Ports.Output;

public interface IPendingRegistrationRepository
{
    Task<PendingRegistration> AddAsync(PendingRegistration registration, CancellationToken cancellationToken = default);
    Task<PendingRegistration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingRegistration>> GetByStatusAsync(RegistrationStatus status, CancellationToken cancellationToken = default);
    Task<bool> ExistsPendingOrApprovedByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}