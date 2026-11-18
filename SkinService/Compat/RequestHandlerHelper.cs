#if !UNITY_EDITOR

using System;
using System.Linq;
using System.Reflection;
using KmyTarkovApi;
using UnityEngine;

namespace SkinService.Compat
{
    /// <summary>
    ///     Calls SPT's client-side <c>SPT.Common.Http.RequestHandler</c> (the EFTApi RequestHandlerHelper
    ///     equivalent) so requests go through the game's configured backend/session.
    /// </summary>
    public static class RequestHandlerHelper
    {
        private static readonly MethodInfo PutJsonMethod;
        private static readonly MethodInfo GetJsonMethod;

        static RequestHandlerHelper()
        {
            var sptCommon = EFTPlugins.SPTCommon ??
                            AppDomain.CurrentDomain.GetAssemblies()
                                .FirstOrDefault(x => x.GetName().Name == "spt-common");

            var requestHandlerType = sptCommon?.GetType("SPT.Common.Http.RequestHandler");

            PutJsonMethod = requestHandlerType?.GetMethod("PutJson",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(string), typeof(string) }, null);

            GetJsonMethod = requestHandlerType?.GetMethod("GetJson",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(string) }, null);

            if (PutJsonMethod == null)
            {
                SkinServicePlugin.Log.LogError("SPT.Common.Http.RequestHandler.PutJson not found");
            }
        }

        public static string PutJson(string path, string json)
        {
            if (PutJsonMethod == null)
                return string.Empty;

            try
            {
                return (string)PutJsonMethod.Invoke(null, new object[] { path, json });
            }
            catch (Exception ex)
            {
                SkinServicePlugin.Log.LogError(
                    $"Request to {path} failed: {ex.InnerException?.Message ?? ex.Message}");
                return string.Empty;
            }
        }

        public static string GetJson(string path)
        {
            if (GetJsonMethod == null)
                return string.Empty;

            try
            {
                return (string)GetJsonMethod.Invoke(null, new object[] { path });
            }
            catch (Exception ex)
            {
                SkinServicePlugin.Log.LogError(
                    $"Request to {path} failed: {ex.InnerException?.Message ?? ex.Message}");
                return string.Empty;
            }
        }
    }
}

#endif
