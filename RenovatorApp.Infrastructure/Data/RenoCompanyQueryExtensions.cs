using RenovatorApp.Infrastructure.Models;

namespace RenovatorApp.Infrastructure.Data;

public static class RenoCompanyQueryExtensions
{
    public static IQueryable<T> ForCompany<T>(this IQueryable<T> query, Guid renoCompanyID)
        where T : IRenoCompanyEntity
    {
        return query.Where(item => item.RenoCompanyID == renoCompanyID);
    }
}
