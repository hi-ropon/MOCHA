using System.Threading;
using System.Threading.Tasks;

namespace MOCHA.Factories;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
