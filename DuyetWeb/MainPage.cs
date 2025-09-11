using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DuyetWeb
{
    public partial class MainPage : Form
    {
        #region ==== Config ====
        // Trang chủ mặc định
        private const string CONFIG_HOMEPAGE_URL = "https://wmsv6.tkelog.com";

        // Chu kỳ quét tiến trình (ms)
        private const int CONFIG_PROCESS_SCAN_INTERVAL_MS = 2000;
        #endregion

        #region ==== Members ====
        // Danh sách domain cho phép
        private readonly HashSet<string> m_dicAllowedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "wmsv6.tkelog.com",
        };

        // Danh sách trình duyệt block
        private readonly HashSet<string> m_dicBrowsersToBlock = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // "msedge", // Edge gốc
            "chrome",
            "firefox",
            "opera",
            "brave"
        };

        private Timer m_objProcessMonitorTimer;
        private int m_iCurrentProcessId;
        #endregion

        public MainPage()
        {
            InitializeComponent();
            this.Load += MainPage_Load;
            this.FormClosed += MainPage_FormClosed;
        }

        #region ==== Lifecycle ====
        private async void MainPage_Load(object sender, EventArgs e)
        {
            await webView.EnsureCoreWebView2Async(null);
            webView.NavigationStarting += WebView_NavigationStarting;
            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            webView.CoreWebView2.Navigate(CONFIG_HOMEPAGE_URL);
            m_iCurrentProcessId = Process.GetCurrentProcess().Id;
            m_objProcessMonitorTimer = new Timer
            {
                Interval = CONFIG_PROCESS_SCAN_INTERVAL_MS
            };
            m_objProcessMonitorTimer.Tick += ProcessMonitorTimer_Tick;
            m_objProcessMonitorTimer.Start();
        }

        private void MainPage_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (m_objProcessMonitorTimer != null)
            {
                m_objProcessMonitorTimer.Stop();
                m_objProcessMonitorTimer.Tick -= ProcessMonitorTimer_Tick;
                m_objProcessMonitorTimer.Dispose();
                m_objProcessMonitorTimer = null;
            }

            if (webView != null)
            {
                if (webView.CoreWebView2 != null)
                {
                    webView.NavigationStarting -= WebView_NavigationStarting;
                    webView.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
                }
                webView.Dispose();
            }
        }
        #endregion

        #region ==== Guards ====

        // Hàm điều hướng
        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (Uri.TryCreate(e.Uri, UriKind.Absolute, out Uri v_uri))
            {
                bool v_isAllowed = IsHostAllowed(v_uri.Host);
                if (!v_isAllowed)
                {
                    e.Cancel = true;
                    MessageBox.Show(
                        $"Truy cập vào trang web '{v_uri.Host}' đã bị chặn.",
                        "Truy cập bị từ chối",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }

        private void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
        }

        private bool IsHostAllowed(string p_strHost)
        {
            if (string.IsNullOrWhiteSpace(p_strHost)) return false;

            // Cho phép nếu host khớp chính xác hoặc là subdomain của domain được phép
            foreach (var v_domain in m_dicAllowedDomains)
            {
                if (p_strHost.Equals(v_domain, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (p_strHost.EndsWith("." + v_domain, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        #endregion

        #region ==== External Browser Blocker ====
        //  Định kỳ quét & kill các trình duyệt không cho phép
        private void ProcessMonitorTimer_Tick(object sender, EventArgs e)
        {
            Process[] v_arrAllProcesses;
            try
            {
                v_arrAllProcesses = Process.GetProcesses();
            }
            catch
            {
                return;
            }

            foreach (var v_process in v_arrAllProcesses)
            {
                if (v_process.Id == m_iCurrentProcessId) continue;

                string v_nameSafe;
                try
                {
                    v_nameSafe = v_process.ProcessName; 
                }
                catch
                {
                    continue;
                }

                if (m_dicBrowsersToBlock.Contains(v_nameSafe))
                {
                    try
                    {
                        v_process.Kill();
                    }
                    catch
                    {

                    }
                    finally
                    {
                        try { v_process.Dispose(); } catch { /* ignore */ }
                    }
                }
            }
        }
        #endregion

        #region ==== Toolbar Actions ====
        private void tsbBack_Click(object sender, EventArgs e)
        {
            if (webView.CanGoBack)
            {
                webView.GoBack();
            }
        }

        private void tsbForward_Click(object sender, EventArgs e)
        {
            if (webView.CanGoForward)
            {
                webView.GoForward();
            }
        }

        private void tsbReload_Click(object sender, EventArgs e)
        {
            webView?.Reload();
        }

        private void tsbHome_Click(object sender, EventArgs e)
        {
            if (webView?.CoreWebView2 != null)
            {
                webView.CoreWebView2.Navigate(CONFIG_HOMEPAGE_URL);
            }
        }
        #endregion
    }
}
