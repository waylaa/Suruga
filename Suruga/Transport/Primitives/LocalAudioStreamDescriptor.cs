namespace Suruga.Transport.Primitives;

internal sealed record LocalAudioStreamDescriptor(string FilePath) : AudioStreamDescriptorBase(FilePath);
