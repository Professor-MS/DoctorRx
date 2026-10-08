using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Entities;

namespace DoctorRx.Domain.Interfaces;

public interface IDraftRepository
{
    Task<Draft?> GetByDraftKeyAsync(Guid draftKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Draft>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Draft> AddAsync(Draft draft, CancellationToken cancellationToken = default);
    Task UpdateAsync(Draft draft, CancellationToken cancellationToken = default);
    Task DeleteAsync(Draft draft, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
