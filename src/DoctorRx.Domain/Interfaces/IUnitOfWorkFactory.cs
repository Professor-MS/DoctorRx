namespace DoctorRx.Domain.Interfaces;

public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
}
