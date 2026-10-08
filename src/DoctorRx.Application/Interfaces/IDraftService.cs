using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Application.Interfaces;

public interface IDraftService
{
    Task<Result<Guid>> SaveAsync(PrescriptionComposerState state, CancellationToken cancellationToken = default);
    Task<Result<PrescriptionComposerState>> GetAsync(Guid draftKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DraftSummaryDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<Result> DiscardAsync(Guid draftKey, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
    void MarkFinalized(Guid draftKey);
    bool IsFinalized(Guid draftKey);
}
