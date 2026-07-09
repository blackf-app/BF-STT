using Android.App;
using Android.Runtime;

namespace BFSTT.Droid
{
    /// <summary>
    /// Custom Application so settings are initialised before any Activity/Service starts.
    /// </summary>
    [Application]
    public class BfSttApp : Application
    {
        public BfSttApp(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();
            AppSettings.Init(this);
        }
    }
}
