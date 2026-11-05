using System;
using System.Security.Cryptography;
using System.Text;

namespace SmartStudyPlanner.Services.ML
{
    internal static class DeviceHelper
    {
        public static string GetId()
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName));
            return "desktop-" + Convert.ToHexString(hash)[..8].ToLowerInvariant();
        }
    }
}
