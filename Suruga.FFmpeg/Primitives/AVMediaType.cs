namespace Suruga.FFmpeg.Primitives;

internal enum AVMediaType
{
    AV_MEDIA_TYPE_UNKNOWN = -1,
    AV_MEDIA_TYPE_VIDEO = 0,
    AV_MEDIA_TYPE_AUDIO = 1,
    AV_MEDIA_TYPE_DATA = 2,
    AV_MEDIA_TYPE_SUBTITLE = 3,
    AV_MEDIA_TYPE_ATTACHMENT = 4,
    AV_MEDIA_TYPE_NB = 5
}
