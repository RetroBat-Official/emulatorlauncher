using System.Windows.Forms;

namespace EmulatorLauncher.ControlCenter
{
    static class ControlCenterManager
    {
        private static System.Threading.Thread _thread;
        private static DialogResult _lastResult;
        private static bool _isRunning;

        public static void ShowControlCenter()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _lastResult = DialogResult.None;

            _thread = new System.Threading.Thread(DoShowControlCenter);
            _thread.IsBackground = true;
            _thread.SetApartmentState(System.Threading.ApartmentState.STA);
            _thread.Start();
        }

        private static void DoShowControlCenter()
        {
            using (var frm = new ControlCenterFrm())
            {
                Application.Run(frm);
                _lastResult = frm.DialogResult;
                _isRunning = false;
            }
        }

        public static bool HasKilledProcess()
        {
            if (_thread != null)
            {
                if (!_thread.Join(1000))
                    _thread.Abort();

                _thread = null;
            }

            return _lastResult == DialogResult.Abort;
        }
    }
}
