using System.Collections.Generic;
using System.Threading.Tasks;

namespace DoctorRx.Application.Interfaces;

public interface IQuickPhrasesService
{
    Task<IReadOnlyList<string>> GetQuickPhrasesAsync();
    Task SaveQuickPhrasesAsync(IEnumerable<string> phrases);
}
