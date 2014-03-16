using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace MiningController
{
    /// <summary>
    /// This class functions by making a list of any processes that have corresponding windows, and then allowing their visibility to be toggled if desired.
    /// </summary>
    public class WindowController : IWindowController
    {
        private delegate int EnumWindowsProc(IntPtr hwnd, int lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int EnumWindows(EnumWindowsProc x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(int hWnd);

        /// <summary>
        /// This method looks for the requested process in the list of processes associated with windows. If not found, it also tries the parent processes
        /// for the request process name. If a window is found, its current visibility is detected, and then ShowWindow is called with the opposite visibility.
        /// </summary>
        /// <param name="processName"></param>
        public void ToggleWindowVisibilityByProcessName(string processName)
        {   
            var windows = this.FindWindowsByProcessName(processName);

            foreach (IntPtr hWnd in windows)
            {
                bool visible = IsWindowVisible(hWnd.ToInt32());
                CheckError("IsWindowVisible");

                ShowWindow(hWnd, visible ? (int)WindowShowStyle.Hide : (int)WindowShowStyle.Show);
                CheckError("ShowWindow");
            }
        }

        public void SetWindowVisibilityByProcessName(string processName, bool visible)
        {
            var windows = this.FindWindowsByProcessName(processName);

            foreach (IntPtr hWnd in windows)
            {
                ShowWindow(hWnd, visible ? (int)WindowShowStyle.Show : (int)WindowShowStyle.Hide);
                CheckError("ShowWindow");
            }
        }

        public bool IsWindowVisibleByProcessName(string processName)
        {
            List<IntPtr> windows;

            try
            {
                windows = this.FindWindowsByProcessName(processName);
            }
            catch (ArgumentException)
            {
                return false;
            }

            foreach (IntPtr hWnd in windows)
            {
                bool visible = IsWindowVisible(hWnd.ToInt32());
                CheckError("IsWindowVisible");

                return visible; // ok to make this check on only the first window associated with the process
            }

            return false;
        }

        private List<IntPtr> FindWindowsByProcessName(string processName)
        {
            var mapThreadToWindows = this.CurrentWindowsByProcessId;

            var process = System.Diagnostics.Process.GetProcessesByName(processName).FirstOrDefault();
            if (process != null)
            {
                int processId = FindWindowedProcessId(mapThreadToWindows, process.Id);

                if (mapThreadToWindows.ContainsKey(processId))
                {
                    return mapThreadToWindows[processId];
                }
                else
                {
                    throw new ArgumentException(string.Format("Unable to locate a window associated with the process name [{0}].", processName));
                }
            }
            else
            {
                throw new ArgumentException(string.Format("Unable to locate a process associated with the name [{0}].", processName));
            }
        }

        /// <summary>
        /// Returns zero if unable to find a windowed process in the process hierarchy.
        /// </summary>
        /// <param name="mapThreadToWindows"></param>
        /// <param name="processId"></param>
        /// <returns></returns>
        private int FindWindowedProcessId(Dictionary<int, List<IntPtr>> mapThreadToWindows, int processId)
        {
            while (!mapThreadToWindows.ContainsKey(processId) && processId != 0)
            {
                processId = RetrieveParentProcessId(processId);
            }

            return processId;
        }

        private void CheckError(string methodName)
        {
            Int32 err = Marshal.GetLastWin32Error();
            if (err != 0)
            {
                var tempToGetMessage = new System.ComponentModel.Win32Exception(err);
                throw new System.ComponentModel.Win32Exception(err, methodName + ": " + tempToGetMessage.ToString());
            }
        }

        private int RetrieveParentProcessId(int processId)
        {
            var searcher = new ManagementObjectSearcher(string.Format("SELECT ParentProcessId FROM Win32_Process WHERE ProcessID = '{0}'", processId));
            ManagementObjectCollection retObjectCollection = searcher.Get();

            foreach (ManagementObject retObject in retObjectCollection)
            {
                return int.Parse(retObject["ParentProcessId"].ToString());
            }

            return 0;
        }

        private Dictionary<int, List<IntPtr>> CurrentWindowsByProcessId
        {
            get
            {
                var mapThreadToWindows = new Dictionary<int, List<IntPtr>>();
                var exceptions = new List<System.ComponentModel.Win32Exception>();
                foreach (Process processInfo in Process.GetProcesses())
                {
                    foreach (ProcessThread threadInfo in processInfo.Threads)
                    {
                        try
                        {
                            IEnumerable<IntPtr> windows = GetWindowHandlesForThread(threadInfo.Id);
                            if (windows != null && windows.Count() > 0)
                            {
                                List<IntPtr> threads;
                                if (mapThreadToWindows.ContainsKey(processInfo.Id))
                                {
                                    threads = mapThreadToWindows[processInfo.Id];
                                }
                                else
                                {
                                    threads = new List<IntPtr>();
                                    mapThreadToWindows.Add(processInfo.Id, threads);
                                }

                                threads.AddRange(windows);
                            }
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                            // ignore any problematic threads (e.g. maybe they have exited)
                        }
                    }
                }

                return mapThreadToWindows;
            }
        }

        private IEnumerable<IntPtr> GetWindowHandlesForThread(int threadHandle)
        {
            var results = new List<IntPtr>();

            EnumWindowsProc callback = (IntPtr hWnd, int lParam) =>
            {
                int processID = 0;
                int threadID = GetWindowThreadProcessId(hWnd, out processID);
                CheckError("GetWindowThreadProcessId");

                if (threadID == lParam)
                {
                    results.Add(hWnd);
                }

                return 1;
            };

            EnumWindows(callback, threadHandle);
            CheckError("EnumWindows");

            return results;
        }

        // From: http://www.pinvoke.net/default.aspx/user32.showwindow
        /// <summary>Enumeration of the different ways of showing a window using 
        /// ShowWindow</summary>
        private enum WindowShowStyle : uint
        {
            /// <summary>Hides the window and activates another window.</summary>
            /// <remarks>See SW_HIDE</remarks>
            Hide = 0,
            /// <summary>Activates and displays a window. If the window is minimized 
            /// or maximized, the system restores it to its original size and 
            /// position. An application should specify this flag when displaying 
            /// the window for the first time.</summary>
            /// <remarks>See SW_SHOWNORMAL</remarks>
            ShowNormal = 1,
            /// <summary>Activates the window and displays it as a minimized window.</summary>
            /// <remarks>See SW_SHOWMINIMIZED</remarks>
            ShowMinimized = 2,
            /// <summary>Activates the window and displays it as a maximized window.</summary>
            /// <remarks>See SW_SHOWMAXIMIZED</remarks>
            ShowMaximized = 3,
            /// <summary>Maximizes the specified window.</summary>
            /// <remarks>See SW_MAXIMIZE</remarks>
            Maximize = 3,
            /// <summary>Displays a window in its most recent size and position. 
            /// This value is similar to "ShowNormal", except the window is not 
            /// actived.</summary>
            /// <remarks>See SW_SHOWNOACTIVATE</remarks>
            ShowNormalNoActivate = 4,
            /// <summary>Activates the window and displays it in its current size 
            /// and position.</summary>
            /// <remarks>See SW_SHOW</remarks>
            Show = 5,
            /// <summary>Minimizes the specified window and activates the next 
            /// top-level window in the Z order.</summary>
            /// <remarks>See SW_MINIMIZE</remarks>
            Minimize = 6,
            /// <summary>Displays the window as a minimized window. This value is 
            /// similar to "ShowMinimized", except the window is not activated.</summary>
            /// <remarks>See SW_SHOWMINNOACTIVE</remarks>
            ShowMinNoActivate = 7,
            /// <summary>Displays the window in its current size and position. This 
            /// value is similar to "Show", except the window is not activated.</summary>
            /// <remarks>See SW_SHOWNA</remarks>
            ShowNoActivate = 8,
            /// <summary>Activates and displays the window. If the window is 
            /// minimized or maximized, the system restores it to its original size 
            /// and position. An application should specify this flag when restoring 
            /// a minimized window.</summary>
            /// <remarks>See SW_RESTORE</remarks>
            Restore = 9,
            /// <summary>Sets the show state based on the SW_ value specified in the 
            /// STARTUPINFO structure passed to the CreateProcess function by the 
            /// program that started the application.</summary>
            /// <remarks>See SW_SHOWDEFAULT</remarks>
            ShowDefault = 10,
            /// <summary>Windows 2000/XP: Minimizes a window, even if the thread 
            /// that owns the window is hung. This flag should only be used when 
            /// minimizing windows from a different thread.</summary>
            /// <remarks>See SW_FORCEMINIMIZE</remarks>
            ForceMinimized = 11
        }
    }
}
