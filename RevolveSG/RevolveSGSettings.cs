using System;
using System.Collections.Generic;

namespace RevolveSG
{
    public static class RevolveSGSettings
    {
        internal static bool use_last_axis => RevolveSGPlugIn.Instance.Settings.GetBool("use_last_axis", true);
        internal static Guid g => RevolveSGPlugIn.Instance.Settings.GetGuid("axis_guid", new Guid());
        internal static int store_index => RevolveSGPlugIn.Instance.Settings.GetInteger("store_index", -1);

        public static void UseLastAxis(bool x) { RevolveSGPlugIn.Instance.Settings.SetBool("use_last_axis", x); }
        public static void StoreAxis(Guid x) { RevolveSGPlugIn.Instance.Settings.SetGuid ("axis_guid", x); }
        public static void StoreIndex(int x) { RevolveSGPlugIn.Instance.Settings.SetInteger("store_index", x); }

    }
}