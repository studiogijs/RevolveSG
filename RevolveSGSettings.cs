using System;
using System.Collections.Generic;



namespace RevolveSG
{
    public static class RevolveSGSettings
    {
        internal static bool use_last_axis => RevolveSGPlugIn.Instance.Settings.GetBool("use_last_axis", Default_UseLastAxis);
        internal static Guid g => RevolveSGPlugIn.Instance.Settings.GetGuid("axis_guid", null);
    }
}