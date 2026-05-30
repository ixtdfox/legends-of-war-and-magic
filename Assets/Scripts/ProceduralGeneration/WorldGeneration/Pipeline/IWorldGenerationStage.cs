using System.Threading;
using System.Threading.Tasks;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline
{
    public interface IWorldGenerationStage<in TInput, TOutput>
    {
        string Name { get; }
        Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken);
    }

    public interface IWorldGenerationStage
    {
        string Name { get; }
        Task<GenerationContext> ExecuteAsync(GenerationContext context, CancellationToken cancellationToken);
    }
}
