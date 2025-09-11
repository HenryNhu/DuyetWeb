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
using System.IO;

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

        private async void MainPage_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Dừng & giải phóng timer
            if (m_objProcessMonitorTimer != null)
            {
                try
                {
                    m_objProcessMonitorTimer.Stop();
                    m_objProcessMonitorTimer.Tick -= ProcessMonitorTimer_Tick;
                    m_objProcessMonitorTimer.Dispose();
                }
                catch { /* ignore */ }
                finally { m_objProcessMonitorTimer = null; }
            }

            // Xoá dữ liệu duyệt TRƯỚC khi Dispose WebView2
            if (webView != null && webView.CoreWebView2 != null)
            {
                try
                {
                    var profile = webView.CoreWebView2.Profile;

                    // GỘP MỌI CỜ BROWSING-DATA CÓ SẴN Ở PHIÊN BẢN SDK HIỆN TẠI
                    var kindsCombined = (CoreWebView2BrowsingDataKinds)0;
                    foreach (CoreWebView2BrowsingDataKinds k in Enum.GetValues(typeof(CoreWebView2BrowsingDataKinds)))
                    {
                        // Bỏ qua 0 nếu có
                        if (Convert.ToInt64(k) != 0)
                            kindsCombined |= k;
                    }

                    // Xoá theo tập cờ đã gộp
                    await profile.ClearBrowsingDataAsync(kindsCombined);

                    // Xoá cookie runtime
                    try { webView.CoreWebView2.CookieManager.DeleteAllCookies(); } catch { /* ignore */ }
                }
                catch { /* ignore */ }
            }

            // 3) Bỏ đăng ký sự kiện & Dispose WebView2
            if (webView != null)
            {
                try
                {
                    if (webView.CoreWebView2 != null)
                    {
                        webView.NavigationStarting -= WebView_NavigationStarting;
                        webView.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
                    }
                }
                catch { /* ignore */ }

                try { webView.Dispose(); } catch { /* ignore */ }
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
