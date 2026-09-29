using Avalonia.Media.Imaging;
using Ryujinx.Ava.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Helpers
{
    /// <summary>
    /// Avatar of the currently selected profile, shared by the Classic carousel
    /// and the Console library view.
    ///
    /// Source of truth is the native profile system: AccountManager.LastOpenedUser.
    /// The Nextendo login binds the account INTO a native profile (name + avatar +
    /// Mii, see ApplyNextendoIdentity) and makes it current, so the local image
    /// already is the Nextendo picture when linked — the server fetch below only
    /// refreshes it when it changed elsewhere. Unlinked users simply see their
    /// native Ryujinx profile picture.
    /// </summary>
    public static class UserProfileAvatar
    {
        // Fresh server avatar, cached per profile (5 min): activation returns must
        // not hit the network every time, but a profile switch refetches immediately.
        private static string _serverImageUserId;
        private static byte[] _serverImageCache;
        private static DateTime _serverImageAt;
        public static byte[] GetLocalImage(AccountManager accountManager) =>
            accountManager?.LastOpenedUser?.Image;

        public static string GetLocalName(AccountManager accountManager) =>
            accountManager?.LastOpenedUser?.Name ?? "";

        public static Bitmap ToBitmap(byte[] data)
        {
            if (data is not { Length: > 0 })
            {
                return null;
            }

            try
            {
                using var stream = new MemoryStream(data);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// (image, name) to display for the current profile. Local profile first
        /// (instant, always the right identity), upgraded from the server when a
        /// Nextendo account is linked and reachable.
        /// </summary>
        public static async Task<(byte[] image, string name)> GetDisplayAvatarAsync(AccountManager accountManager)
        {
            byte[] local = GetLocalImage(accountManager);
            string name = GetLocalName(accountManager);

            if (NextendoAccount.IsLinked && !NextendoServerOverride.HorsNextendo)
            {
                string uid = accountManager?.LastOpenedUser?.UserId.ToString() ?? "";
                if (uid != _serverImageUserId || (DateTime.UtcNow - _serverImageAt).TotalMinutes > 5)
                {
                    try
                    {
                        (string _, byte[] serverImage) = await NextendoApi.GetProfileSyncAsync();
                        _serverImageUserId = uid;
                        _serverImageCache = serverImage is { Length: > 0 } ? serverImage : null;
                        _serverImageAt = DateTime.UtcNow;
                    }
                    catch
                    {
                        // Offline or server hiccup: the local profile image stays.
                    }
                }

                if (_serverImageUserId == uid && _serverImageCache is { Length: > 0 })
                {
                    return (_serverImageCache, string.IsNullOrEmpty(name) ? NextendoAccount.Username : name);
                }
            }

            return (local, name);
        }
    }
}
