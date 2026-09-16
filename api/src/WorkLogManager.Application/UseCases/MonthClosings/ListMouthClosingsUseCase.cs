using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.MonthClosings;

public sealed class ListMonthClosingsUseCase
{
    private readonly IMonthClosingRepository _monthClosingRepository;

    public ListMonthClosingsUseCase(IMonthClosingRepository monthClosingRepository)
    {
        _monthClosingRepository = monthClosingRepository;
    }

    public async Task<IReadOnlyList<MonthClosing>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _monthClosingRepository.ListAsync(cancellationToken);
    }
}