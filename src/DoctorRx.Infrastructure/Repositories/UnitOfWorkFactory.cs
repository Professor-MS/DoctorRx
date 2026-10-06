using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DoctorRx.Infrastructure.Repositories;

public class UnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IDbContextFactory<DoctorRxDbContext> _contextFactory;

    public UnitOfWorkFactory(IDbContextFactory<DoctorRxDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IUnitOfWork Create()
    {
        var context = _contextFactory.CreateDbContext();
        return new UnitOfWork(
            context,
            new PatientRepository(context),
            new PrescriptionRepository(context),
            new MedicineRepository(context),
            new DoctorRepository(context)
        );
    }
}
