using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.WorkSchedules;

public class GetGeneralWorkScheduleUseCase
{
    private readonly IWorkSchedulePeriodRepository _workSchedulePeriodRepository;

    public GetGeneralWorkScheduleUseCase(IWorkSchedulePeriodRepository workSchedulePeriodRepository)
    {
        _workSchedulePeriodRepository = workSchedulePeriodRepository;
    }

    public Task<IReadOnlyList<WorkSchedulePeriod>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _workSchedulePeriodRepository.ListAsync(null, cancellationToken);
    }
}
