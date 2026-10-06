using System.Collections.Generic;

namespace DoctorRx.Application.Common;

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize)
{
    public bool HasMore => (PageNumber * PageSize) < TotalCount;
}
