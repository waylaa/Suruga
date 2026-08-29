namespace Suruga.FFmpeg;

public static class Constants
{
    public const int EAGAIN = 11;

    public const int AVERROR_EOF = -541478725;
    public const int AVERROR_EIO = -5;
    
    public const int AVSEEK_SIZE = 0x10000;
    public const int AVSEEK_SET = 0;
    public const int AVSEEK_CUR = 1;
    public const int AVSEEK_END = 2;
    public const int AVSEEK_FLAG_BACKWARD = 1;
    public const int AVSEEK_FLAG_ANY = 4;
    
    public const int AVFMT_FLAG_CUSTOM_IO = 0x0080;
    
    public const int AV_TIME_BASE = 1000000;
    
    public const int AV_DICT_IGNORE_SUFFIX = 2;
    
    public const int AV_ERROR_MAX_STRING_SIZE = 64;
    
    public const int AV_LOG_PANIC = 0;
    public const int AV_LOG_FATAL = 8;
    public const int AV_LOG_ERROR = 16;
    public const int AV_LOG_WARNING = 24;
    public const int AV_LOG_INFO = 32;
    public const int AV_LOG_VERBOSE = 40;
    public const int AV_LOG_DEBUG = 48;
    public const int AV_LOG_TRACE = 56;
    
    public static int Error(int errno) => -errno;
}
