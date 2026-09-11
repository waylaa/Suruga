using Suruga.Resolvers.Primitives;

namespace Suruga.Resolvers.Classification;

internal interface IInputClassifier
{
    bool TryClassify(string rawValue, out InputType type, out string normalizedValue);
}
