namespace Suruga.Resolvers.Youtube.Clients;

internal static class InnertubeClientProfiles
{
    internal static readonly InnertubeClientProfile Android = new()
    {
        ClientName = "ANDROID",
        ClientVersion = "21.35.448",
        OsName = "Android",
        AndroidSdkVersion = 31,
        OsVersion = "12",
        Platform = "MOBILE",
        UserAgent = "com.google.android.youtube/21.35.448 (Linux; U; Android 12) gzip",
        ClientNameHeaderValue = "3"
    };

    internal static readonly InnertubeClientProfile AndroidVr = new()
    {
        ClientName = "ANDROID_VR",
        ClientVersion = "1.60.19",
        DeviceMake = "Oculus",
        DeviceModel = "Quest 3",
        OsName = "Android",
        OsVersion = "12L",
        Platform = "MOBILE",
        UserAgent = "com.google.android.apps.youtube.vr.oculus/1.60.19 (Linux; U; Android 12L; Quest 3 Build/SQ3A.220605.009.A1) gzip",
        ClientNameHeaderValue = "28"
    };
}
