// PixCake 助手 - 多账号 & 预设管理工具
// Compiled C# WinForms — 替代原 PowerShell GUI
// Build: build.bat (uses Windows built-in csc.exe)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace PixCakeHelper
{
    // ═══ Data Models ═══
    public class AccountData  { public string username { get; set; } public string password { get; set; } public bool used { get; set; } }
    public class PresetData   { public string name { get; set; } public string content { get; set; } }
    public class ConfigData   { public string password { get; set; } public AccountData[] accounts { get; set; } public PresetData[] presets { get; set; } }

    // ═══ Win32 P/Invoke ═══
    internal static class NativeMethods
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("gdi32.dll")]  public static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);
        [DllImport("user32.dll")] public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        public static extern int SetWindowTheme(IntPtr hwnd, string appName, string subIdList);
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HTCAPTION = 2;
        public const int CS_DROPSHADOW = 0x00020000;
    }

    // ═══ Main Form ═══
    public class MainForm : Form
    {
        // --- DPI Scale ---
        private float D = 1f;
        private int S(int v) { return (int)(v * D); }

        // --- Config ---
        private string configPath;
        private string password;
        private List<AccountData> accounts = new List<AccountData>();
        private List<PresetData> presets = new List<PresetData>();

        // --- Colors ---
        static readonly Color BgDark     = ColorTranslator.FromHtml("#0e0e11");
        static readonly Color BgCard     = ColorTranslator.FromHtml("#111113");
        static readonly Color BgNav      = ColorTranslator.FromHtml("#16161a");
        static readonly Color BgInput    = ColorTranslator.FromHtml("#16161a");
        static readonly Color Border     = ColorTranslator.FromHtml("#242427");
        static readonly Color Accent     = ColorTranslator.FromHtml("#3b82f6");
        static readonly Color TextPri    = ColorTranslator.FromHtml("#f4f4f5");
        static readonly Color TextSec    = ColorTranslator.FromHtml("#e4e4e7");
        static readonly Color TextMuted  = ColorTranslator.FromHtml("#71717a");
        static readonly Color TextDimmed = ColorTranslator.FromHtml("#a1a1aa");
        static readonly Color Green      = ColorTranslator.FromHtml("#10b981");
        static readonly Color Red        = ColorTranslator.FromHtml("#ef4444");
        static readonly Color CloseRed   = ColorTranslator.FromHtml("#e81123");
        static readonly Color DarkGray   = ColorTranslator.FromHtml("#27272a");
        static readonly Color DotUsed    = ColorTranslator.FromHtml("#52525b");
        static readonly Color HoverBg    = ColorTranslator.FromHtml("#1a1a1f");
        static readonly Color WarnRed    = ColorTranslator.FromHtml("#f43f5e");

        // --- Fonts (not DPI-scaled; GDI+ handles point→pixel at current DPI) ---
        Font fTitle   = new Font("Segoe UI", 10f, FontStyle.Bold);
        Font fTabOn   = new Font("微软雅黑", 9.5f, FontStyle.Bold);
        Font fTabOff  = new Font("微软雅黑", 9.5f);
        Font fLabel   = new Font("微软雅黑", 9f);
        Font fLabelB  = new Font("微软雅黑", 9f, FontStyle.Bold);
        Font fBtn     = new Font("微软雅黑", 9f, FontStyle.Bold);
        Font fList    = new Font("Segoe UI", 9.5f);
        Font fListCN  = new Font("微软雅黑", 9.5f);
        Font fCode    = new Font("Consolas", 9.5f);
        Font fInput   = new Font("微软雅黑", 9.5f);
        Font fWinCtrl = new Font("Consolas", 11f);

        // --- Key Controls ---
        Panel navPanel, indicatorBar, contentPanel;
        Panel panelAccounts, panelPresets;
        Button btnTabAccounts, btnTabPresets;
        ListBox listBox, presetListBox;
        Label statusBar, presetStatus;
        TextBox txtPresetName, txtPresetContent;

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= NativeMethods.CS_DROPSHADOW; return cp; }
        }

        // ──── Constructor ────
        public MainForm()
        {
            // Detect DPI scale factor
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                D = g.DpiX / 96f;

            configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "accounts.json");
            if (!LoadConfig()) return;

            SetupForm();
            SetupNavbar();
            SetupAccountsPanel();
            SetupPresetsPanel();

            // Apply dark scrollbar theme once handles exist
            this.Shown += (s, e) =>
            {
                NativeMethods.SetWindowTheme(listBox.Handle, "DarkMode_Explorer", null);
                NativeMethods.SetWindowTheme(presetListBox.Handle, "DarkMode_Explorer", null);
                if (txtPresetContent != null)
                    NativeMethods.SetWindowTheme(txtPresetContent.Handle, "DarkMode_Explorer", null);
            };

            RefreshAccountList(-1);
            RefreshPresetList();
        }

        // ──── Form Setup ────
        private void SetupForm()
        {
            this.Text = "像素蛋糕多账号 & 预设管理助手";
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            this.AutoScaleMode = AutoScaleMode.None;
            this.ClientSize = new Size(S(600), S(700));
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = BgDark;
            this.ForeColor = TextPri;
            this.DoubleBuffered = true;

            // Icon
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            try { if (File.Exists(icoPath)) this.Icon = new Icon(icoPath); }
            catch { this.Icon = SystemIcons.Application; }

            this.Load   += (s, e) => ApplyRoundCorners();
            this.Resize += (s, e) => ApplyRoundCorners();

            contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = BgDark };
            this.Controls.Add(contentPanel);
        }

        private void ApplyRoundCorners()
        {
            if (this.Width <= 0 || this.Height <= 0) return;
            var rgn = NativeMethods.CreateRoundRectRgn(0, 0, this.Width, this.Height, S(14), S(14));
            NativeMethods.SetWindowRgn(this.Handle, rgn, true);
        }

        // ──── Navbar ────
        private void SetupNavbar()
        {
            int navH = S(44);
            var navBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Border };
            this.Controls.Add(navBorder);

            navPanel = new Panel { Dock = DockStyle.Top, Height = navH, BackColor = BgNav };
            this.Controls.Add(navPanel);
            navPanel.MouseDown += DragForm;

            // Logo image (extract from exe instead of relying on external app.png)
            if (this.Icon != null)
            {
                var img = new PictureBox
                {
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(S(22), S(22)),
                    Location = new Point(S(14), S(11)),
                    Cursor = Cursors.SizeAll, BackColor = Color.Transparent
                };
                try { img.Image = this.Icon.ToBitmap(); } catch { }
                img.MouseDown += DragForm;
                navPanel.Controls.Add(img);
            }

            // Title
            var lblTitle = new Label
            {
                Text = "PixCake", AutoSize = true,
                Location = new Point(S(40), S(10)),
                ForeColor = TextSec, Font = fTitle,
                Cursor = Cursors.SizeAll, BackColor = Color.Transparent
            };
            lblTitle.MouseDown += DragForm;
            navPanel.Controls.Add(lblTitle);

            // Tab buttons (wider for CJK text)
            int tabW = S(100);
            int tabX1 = S(160);
            int tabX2 = tabX1 + tabW;

            btnTabAccounts = MakeTabBtn("账号管理", tabX1, tabW, true);
            btnTabPresets  = MakeTabBtn("预设分享", tabX2, tabW, false);
            navPanel.Controls.Add(btnTabAccounts);
            navPanel.Controls.Add(btnTabPresets);

            indicatorBar = new Panel
            {
                Size = new Size(tabW, S(3)),
                BackColor = Accent,
                Location = new Point(tabX1, navH - S(3))
            };
            navPanel.Controls.Add(indicatorBar);

            // Close / Minimize (anchored to right edge)
            int formW = this.ClientSize.Width;
            var btnClose = MakeWinBtn("✕", formW - S(40));
            btnClose.Click += (s, e) => this.Close();
            btnClose.MouseEnter += (s, e) => { btnClose.BackColor = CloseRed; btnClose.ForeColor = Color.White; };
            btnClose.MouseLeave += (s, e) => { btnClose.BackColor = Color.Transparent; btnClose.ForeColor = TextMuted; };

            var btnMin = MakeWinBtn("—", formW - S(80));
            btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            btnMin.MouseEnter += (s, e) => { btnMin.BackColor = Border; btnMin.ForeColor = Color.White; };
            btnMin.MouseLeave += (s, e) => { btnMin.BackColor = Color.Transparent; btnMin.ForeColor = TextMuted; };

            navPanel.Controls.Add(btnClose);
            navPanel.Controls.Add(btnMin);

            btnTabAccounts.Click += (s, e) => SwitchTab(true);
            btnTabPresets.Click  += (s, e) => SwitchTab(false);
        }

        private Button MakeTabBtn(string text, int x, int w, bool active)
        {
            var btn = new Button
            {
                Text = text, Size = new Size(w, S(44)), Location = new Point(x, 0),
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
                ForeColor = active ? Accent : TextDimmed,
                Font = active ? fTabOn : fTabOff, Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            return btn;
        }

        private Button MakeWinBtn(string text, int x)
        {
            var btn = new Button
            {
                Text = text, Size = new Size(S(40), S(44)), Location = new Point(x, 0),
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
                ForeColor = TextMuted, Font = fWinCtrl, Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void SwitchTab(bool showAccounts)
        {
            panelAccounts.Visible = showAccounts;
            panelPresets.Visible  = !showAccounts;
            btnTabAccounts.ForeColor = showAccounts ? Accent : TextDimmed;
            btnTabAccounts.Font      = showAccounts ? fTabOn : fTabOff;
            btnTabPresets.ForeColor  = showAccounts ? TextDimmed : Accent;
            btnTabPresets.Font       = showAccounts ? fTabOff : fTabOn;
            indicatorBar.Location = new Point(showAccounts ? btnTabAccounts.Left : btnTabPresets.Left, indicatorBar.Top);
        }

        private void DragForm(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(this.Handle, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);
            }
        }

        // ──── Accounts Panel (dynamic Y layout) ────
        private void SetupAccountsPanel()
        {
            panelAccounts = new Panel { Dock = DockStyle.Fill, BackColor = BgDark };
            contentPanel.Controls.Add(panelAccounts);

            int pad = S(20);
            int w = this.ClientSize.Width - pad * 2;
            int gap = S(10);
            int y = S(12);

            // Title
            var lbl = new Label
            {
                Text = "选择账号后双击 或 点击下方自动登录按钮",
                Location = new Point(pad, y), Size = new Size(w, S(22)),
                ForeColor = TextMuted, Font = fLabel
            };
            panelAccounts.Controls.Add(lbl);
            y += S(22) + gap;

            // ListBox container
            int listH = S(240); // Reduced height to fit an extra button row
            var container = MakeBorderedPanel(pad, y, w, listH);
            panelAccounts.Controls.Add(container);

            listBox = new ListBox
            {
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
                BackColor = BgCard, ForeColor = TextPri,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = S(36),
                IntegralHeight = false
            };
            listBox.DrawItem += AccountList_DrawItem;
            listBox.DoubleClick += (s, e) => DoLogin();
            container.Controls.Add(listBox);
            y += listH + S(12);

            // Status bar
            statusBar = new Label
            {
                Text = "注意：自动登录前，请先用鼠标点击像素蛋糕的【手机号/账号】输入框。",
                Location = new Point(pad, y), Size = new Size(w, S(38)),
                ForeColor = WarnRed, Font = fLabelB
            };
            panelAccounts.Controls.Add(statusBar);
            y += S(38) + gap;

            // Button row 1
            int halfW = (w - gap) / 2;
            var btnLogin = MakeButton("⚡ 自动登录", new Point(pad, y), new Size(halfW, S(44)), Green, Color.White);
            btnLogin.Click += (s, e) => DoLogin();
            panelAccounts.Controls.Add(btnLogin);

            var btnToggle = MakeButton("🔄 标记已用/未用", new Point(pad + halfW + gap, y), new Size(halfW, S(44)), DarkGray, TextSec);
            btnToggle.Click += (s, e) => DoToggleStatus();
            panelAccounts.Controls.Add(btnToggle);
            y += S(44) + gap;

            // Button row 2
            var btnCopyAcc = MakeButton("📋 仅复制账号", new Point(pad, y), new Size(halfW, S(40)), DarkGray, TextSec);
            btnCopyAcc.Click += (s, e) => DoCopyAccount();
            panelAccounts.Controls.Add(btnCopyAcc);

            var btnCopyPwd = MakeButton("🔑 仅复制密码", new Point(pad + halfW + gap, y), new Size(halfW, S(40)), DarkGray, TextSec);
            btnCopyPwd.Click += (s, e) => DoCopyPassword();
            panelAccounts.Controls.Add(btnCopyPwd);
            y += S(40) + gap;

            // Button row 3
            var btnImport = MakeButton("📥 从剪贴板批量导入", new Point(pad, y), new Size(halfW, S(40)), Accent, Color.White);
            btnImport.Click += (s, e) => DoImportAccounts();
            panelAccounts.Controls.Add(btnImport);

            var btnClear = MakeButton("🗑️ 删除选中项", new Point(pad + halfW + gap, y), new Size(halfW, S(40)), Red, Color.White);
            btnClear.Click += (s, e) => DoDeleteAccount();
            panelAccounts.Controls.Add(btnClear);
        }

        // ──── Presets Panel (dynamic Y layout) ────
        private void SetupPresetsPanel()
        {
            panelPresets = new Panel { Dock = DockStyle.Fill, BackColor = BgDark, Visible = false };
            contentPanel.Controls.Add(panelPresets);

            int pad = S(20);
            int formW = this.ClientSize.Width - pad * 2;
            int gap = S(10);
            int leftW = S(230);
            int rightX = pad + leftW + gap;
            int rightW = formW - leftW - gap;
            int y = S(12);

            // ── Left column: preset list ──
            var lblList = new Label
            {
                Text = "预设列表 (双击粘贴)", Location = new Point(pad, y),
                Size = new Size(leftW, S(22)), ForeColor = TextMuted, Font = fLabel
            };
            panelPresets.Controls.Add(lblList);

            // ── Right column: input fields ──
            var lblName = new Label
            {
                Text = "新预设名称：", Location = new Point(rightX, y),
                Size = new Size(rightW, S(22)), ForeColor = TextMuted, Font = fLabel
            };
            panelPresets.Controls.Add(lblName);
            y += S(22) + S(5);

            // Preset ListBox
            int listH = S(280);
            var presetContainer = MakeBorderedPanel(pad, y, leftW, listH);
            panelPresets.Controls.Add(presetContainer);

            presetListBox = new ListBox
            {
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
                BackColor = BgCard, ForeColor = TextPri,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = S(36),
                IntegralHeight = false
            };
            presetListBox.DrawItem += PresetList_DrawItem;
            presetListBox.DoubleClick += (s, e) => DoPastePreset();
            presetContainer.Controls.Add(presetListBox);

            // Preset Name TextBox
            txtPresetName = MakeTextBox(panelPresets, new Point(rightX, y), new Size(rightW, S(34)), false);
            int nameBottom = y + S(34);

            // Content label + textbox
            int contentLabelY = nameBottom + gap;
            var lblContent = new Label
            {
                Text = "粘贴预设代码或完整口令：", Location = new Point(rightX, contentLabelY),
                Size = new Size(rightW, S(22)), ForeColor = TextMuted, Font = fLabel
            };
            panelPresets.Controls.Add(lblContent);

            int contentBoxY = contentLabelY + S(22) + S(3);
            int contentBoxH = (y + listH) - contentBoxY; // Align bottom with ListBox
            txtPresetContent = MakeTextBox(panelPresets, new Point(rightX, contentBoxY), new Size(rightW, contentBoxH), true);

            y += listH + gap;

            // Delete + Save buttons
            var btnDel = MakeButton("✕ 删除选中", new Point(pad, y), new Size(leftW, S(40)), Red, Color.White);
            btnDel.Click += (s, e) => DoDeletePreset();
            panelPresets.Controls.Add(btnDel);

            var btnSave = MakeButton("💾 保存并添加", new Point(rightX, y), new Size(rightW, S(40)), Accent, Color.White);
            btnSave.Click += (s, e) => DoSavePreset();
            panelPresets.Controls.Add(btnSave);
            y += S(40) + S(12);

            // Paste + Copy buttons
            int halfW = (formW - gap) / 2;
            var btnPaste = MakeButton("⚡ 粘贴选中预设", new Point(pad, y), new Size(halfW, S(44)), Accent, Color.White);
            btnPaste.Click += (s, e) => DoPastePreset();
            panelPresets.Controls.Add(btnPaste);

            var btnCopy = MakeButton("📋 复制预设口令", new Point(pad + halfW + gap, y), new Size(halfW, S(44)), DarkGray, TextSec);
            btnCopy.Click += (s, e) => DoCopyPreset();
            panelPresets.Controls.Add(btnCopy);
            y += S(44) + gap;

            // Status
            presetStatus = new Label
            {
                Text = "提示：可粘贴分享完整口令，会自动提取解析名称和代码。",
                Location = new Point(pad, y), Size = new Size(formW, S(35)),
                ForeColor = TextMuted, Font = fLabel
            };
            panelPresets.Controls.Add(presetStatus);
        }

        // ──── UI Factory ────

        private Panel MakeBorderedPanel(int x, int y, int w, int h)
        {
            var p = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = BgCard };
            p.Paint += (s, e) =>
            {
                using (var pen = new Pen(Border, 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            };
            return p;
        }

        private Button MakeButton(string text, Point loc, Size size, Color bg, Color fg)
        {
            var btn = new Button
            {
                Text = text, Location = loc, Size = size,
                FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = fg,
                Font = fBtn, Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;

            int d = S(12);
            Action applyRound = () =>
            {
                if (btn.Width <= d || btn.Height <= d) return;
                var path = new GraphicsPath();
                path.AddArc(0, 0, d, d, 180, 90);
                path.AddArc(btn.Width - d, 0, d, d, 270, 90);
                path.AddArc(btn.Width - d, btn.Height - d, d, d, 0, 90);
                path.AddArc(0, btn.Height - d, d, d, 90, 90);
                path.CloseFigure();
                if (btn.Region != null) btn.Region.Dispose();
                btn.Region = new Region(path);
            };
            btn.Resize += (s, e) => applyRound();
            applyRound();

            Color hc = Color.FromArgb(200, bg.R, bg.G, bg.B);
            btn.MouseEnter += (s, e) => btn.BackColor = hc;
            btn.MouseLeave += (s, e) => btn.BackColor = bg;
            return btn;
        }

        private TextBox MakeTextBox(Panel parent, Point loc, Size size, bool multiline)
        {
            var container = new Panel { Location = loc, Size = size, BackColor = BgInput, Tag = false };
            parent.Controls.Add(container);

            int inpad = S(8);
            var txt = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = BgInput, ForeColor = TextPri,
                Font = multiline ? fCode : fInput
            };
            if (multiline)
            {
                txt.Multiline = true;
                txt.ScrollBars = ScrollBars.Vertical;
                txt.Location = new Point(inpad, inpad);
                txt.Size = new Size(size.Width - inpad * 2, size.Height - inpad * 2);
            }
            else
            {
                txt.Location = new Point(inpad, S(7));
                txt.Size = new Size(size.Width - inpad * 2, S(20));
            }
            container.Controls.Add(txt);

            container.Paint += (s, e) =>
            {
                bool focused = container.Tag is bool && (bool)container.Tag;
                Color c = focused ? Accent : Border;
                using (var pen = new Pen(c, focused ? 2 : 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, container.Width - 1, container.Height - 1);
            };
            txt.GotFocus  += (s, e) => { container.Tag = true;  container.Invalidate(); };
            txt.LostFocus += (s, e) => { container.Tag = false; container.Invalidate(); };
            return txt;
        }

        // ──── Owner-Draw: Account List ────
        private void AccountList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var r = e.Bounds;
            string itemText = listBox.Items[e.Index].ToString();
            bool isUsed = itemText.StartsWith("\u26AB");
            string username = itemText;
            int pipe = itemText.IndexOf('|');
            if (pipe >= 0 && pipe + 2 < itemText.Length) username = itemText.Substring(pipe + 2);
            bool isSel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (var br = new SolidBrush(isSel ? HoverBg : BgCard)) g.FillRectangle(br, r);
            if (isSel) using (var pen = new Pen(Accent, S(3))) g.DrawLine(pen, r.X + 1, r.Y, r.X + 1, r.Bottom);

            Color dotCol = isUsed ? DotUsed : Green;
            float dotSz = S(8);
            using (var br = new SolidBrush(dotCol))
                g.FillEllipse(br, r.X + S(14), r.Y + (r.Height - dotSz) / 2f, dotSz, dotSz);

            using (var br = new SolidBrush(isUsed ? TextMuted : TextSec))
                g.DrawString(username, fList, br, r.X + S(32), r.Y + (r.Height - fList.Height) / 2f);
        }

        // ──── Owner-Draw: Preset List ────
        private void PresetList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var r = e.Bounds;
            string name = presetListBox.Items[e.Index].ToString();
            bool isSel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (var br = new SolidBrush(isSel ? HoverBg : BgCard)) g.FillRectangle(br, r);
            if (isSel) using (var pen = new Pen(Accent, S(3))) g.DrawLine(pen, r.X + 1, r.Y, r.X + 1, r.Bottom);
            using (var br = new SolidBrush(isSel ? Accent : TextSec))
                g.DrawString(name, fListCN, br, r.X + S(12), r.Y + (r.Height - fListCN.Height) / 2f);
        }

        // ════ Config I/O ════
        private bool LoadConfig()
        {
            if (!File.Exists(configPath))
            {
                // Create a default empty config if it doesn't exist
                password = "";
                accounts = new List<AccountData>();
                presets = new List<PresetData>();
                SaveConfig();
                return true;
            }
            try
            {
                string json = File.ReadAllText(configPath, Encoding.UTF8);
                var cfg = new JavaScriptSerializer().Deserialize<ConfigData>(json);
                password = cfg.password ?? "";
                accounts = cfg.accounts != null ? new List<AccountData>(cfg.accounts) : new List<AccountData>();
                presets  = cfg.presets  != null ? new List<PresetData>(cfg.presets)   : new List<PresetData>();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("解析 accounts.json 失败：\n" + ex.Message, "解析错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void SaveConfig()
        {
            try
            {
                var cfg = new ConfigData { password = this.password, accounts = this.accounts.ToArray(), presets = this.presets.ToArray() };
                string json = FormatJson(new JavaScriptSerializer().Serialize(cfg));
                File.WriteAllText(configPath, json, new UTF8Encoding(false));
            }
            catch (Exception ex) { MessageBox.Show("保存配置文件失败：" + ex.Message, "保存错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ════ List Refresh ════
        private void RefreshAccountList(int keepIndex)
        {
            listBox.Items.Clear();
            foreach (var acc in accounts)
                listBox.Items.Add((acc.used ? "⚫ 已用 | " : "🟢 未用 | ") + acc.username);
            if (keepIndex >= 0 && keepIndex < accounts.Count) { listBox.SelectedIndex = keepIndex; return; }
            for (int i = 0; i < accounts.Count; i++)
                if (!accounts[i].used) { listBox.SelectedIndex = i; return; }
            if (accounts.Count > 0) listBox.SelectedIndex = 0;
        }

        private void RefreshPresetList()
        {
            presetListBox.Items.Clear();
            foreach (var p in presets) presetListBox.Items.Add(p.name);
            if (presets.Count > 0) presetListBox.SelectedIndex = 0;
        }

        // ════ Actions ════
        private void DoLogin()
        {
            int idx = listBox.SelectedIndex;
            if (idx < 0 || idx >= accounts.Count) { MessageBox.Show("请先在列表中选择一个账号！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            statusBar.Text = "正在尝试唤醒【像素蛋糕】客户端..."; statusBar.ForeColor = Accent; panelAccounts.Refresh();
            this.WindowState = FormWindowState.Minimized; Thread.Sleep(300);

            bool activated = ActivatePixCake();
            if (!activated)
            {
                if (MessageBox.Show("未检测到运行中的【像素蛋糕】客户端。\n点击确定后，请在 2 秒内手动点击像素蛋糕的【手机号/账号输入框】！",
                    "需要手动对焦", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.Cancel)
                { this.WindowState = FormWindowState.Normal; statusBar.Text = "操作已取消。"; statusBar.ForeColor = Red; return; }
                Thread.Sleep(2000);
            }
            else Thread.Sleep(600);

            try
            {
                string acc = EscapeSendKeys(accounts[idx].username);
                string pwd = EscapeSendKeys(string.IsNullOrEmpty(accounts[idx].password) ? password : accounts[idx].password);
                SendKeys.SendWait("{END}"); Thread.Sleep(50); SendKeys.SendWait("+{HOME}"); Thread.Sleep(50); SendKeys.SendWait("{BACKSPACE}"); Thread.Sleep(50);
                for (int i = 0; i < 25; i++) { SendKeys.SendWait("{BACKSPACE}"); SendKeys.SendWait("{DELETE}"); }
                Thread.Sleep(100); SendKeys.SendWait(acc); Thread.Sleep(150); SendKeys.SendWait("{TAB}"); Thread.Sleep(150);
                SendKeys.SendWait("{END}"); Thread.Sleep(50); SendKeys.SendWait("+{HOME}"); Thread.Sleep(50); SendKeys.SendWait("{BACKSPACE}"); Thread.Sleep(50);
                for (int i = 0; i < 30; i++) { SendKeys.SendWait("{BACKSPACE}"); SendKeys.SendWait("{DELETE}"); }
                Thread.Sleep(100); SendKeys.SendWait(pwd); Thread.Sleep(150); SendKeys.SendWait("{ENTER}");
                accounts[idx].used = true; SaveConfig(); RefreshAccountList(-1);
                statusBar.Text = "已完成输入发送！该账号已标记为已使用。"; statusBar.ForeColor = Green;
            }
            catch (Exception ex) { MessageBox.Show("自动输入时出错：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); statusBar.Text = "输入发生错误。"; statusBar.ForeColor = Red; }
            finally { this.WindowState = FormWindowState.Normal; }
        }

        private void DoToggleStatus()
        {
            int idx = listBox.SelectedIndex;
            if (idx < 0 || idx >= accounts.Count) { MessageBox.Show("请先选择一个账号！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            accounts[idx].used = !accounts[idx].used; SaveConfig(); RefreshAccountList(idx);
            statusBar.Text = "状态更改成功！"; statusBar.ForeColor = Green;
        }

        private void DoCopyAccount()
        {
            int idx = listBox.SelectedIndex;
            if (idx >= 0 && idx < accounts.Count) { Clipboard.SetText(accounts[idx].username); statusBar.Text = "账号已复制到剪贴板！"; statusBar.ForeColor = Green; }
            else MessageBox.Show("请先选择一个账号！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void DoCopyPassword()
        {
            int idx = listBox.SelectedIndex;
            string pwd = password;
            if (idx >= 0 && idx < accounts.Count && !string.IsNullOrEmpty(accounts[idx].password)) { pwd = accounts[idx].password; }

            if (!string.IsNullOrEmpty(pwd)) { Clipboard.SetText(pwd); statusBar.Text = "密码已复制到剪贴板！"; statusBar.ForeColor = Green; }
            else MessageBox.Show("密码为空，请先配置全局密码或导入带密码的账号！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void DoImportAccounts()
        {
            string text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text)) { MessageBox.Show("剪贴板为空，请先复制账号内容！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int added = 0;
            foreach (var line in lines)
            {
                string l = line.Trim();
                if (string.IsNullOrEmpty(l)) continue;
                
                string acc = l;
                string pwd = null;

                // Handle delimiters: ----, space, comma, tab
                var match = Regex.Match(l, @"^(.+?)(----|\s+|,|\t)(.+)$");
                if (match.Success)
                {
                    acc = match.Groups[1].Value.Trim();
                    pwd = match.Groups[3].Value.Trim();
                }

                // Check for duplicates
                bool exists = false;
                foreach (var existing in accounts) { if (existing.username == acc) { exists = true; break; } }
                if (!exists)
                {
                    accounts.Add(new AccountData { username = acc, password = pwd, used = false });
                    added++;
                }
            }

            if (added > 0)
            {
                SaveConfig(); RefreshAccountList(-1);
                statusBar.Text = string.Format("成功导入 {0} 个新账号！", added); statusBar.ForeColor = Green;
            }
            else { statusBar.Text = "剪贴板中的账号已存在或格式无法识别。"; statusBar.ForeColor = Color.Orange; }
        }

        private void DoDeleteAccount()
        {
            int idx = listBox.SelectedIndex;
            if (idx < 0 || idx >= accounts.Count) { MessageBox.Show("请先选择要删除的账号！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string acc = accounts[idx].username;
            if (MessageBox.Show("确定要删除账号【" + acc + "】吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            { accounts.RemoveAt(idx); SaveConfig(); RefreshAccountList(-1); statusBar.Text = "已删除账号【" + acc + "】。"; statusBar.ForeColor = Green; }
        }

        private void DoSavePreset()
        {
            string name = txtPresetName.Text.Trim();
            string content = txtPresetContent.Text.Trim();
            if (string.IsNullOrEmpty(content)) { MessageBox.Show("请输入或粘贴预设内容！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            var match = Regex.Match(content, @"好友给你分享了1个预设-(.+?)等「(.+?)」");
            if (match.Success)
            {
                if (string.IsNullOrEmpty(name)) { name = match.Groups[1].Value.Trim(); txtPresetName.Text = name; }
                content = match.Groups[2].Value.Trim(); txtPresetContent.Text = content;
            }
            if (string.IsNullOrEmpty(name)) { MessageBox.Show("请输入预设名称！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            int existIdx = -1;
            for (int i = 0; i < presets.Count; i++) if (presets[i].name == name) { existIdx = i; break; }
            if (existIdx >= 0)
            {
                if (MessageBox.Show("预设【" + name + "】已存在，是否覆盖？", "确认覆盖", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                { presets[existIdx].content = content; presetStatus.Text = "预设【" + name + "】已更新！"; }
                else return;
            }
            else { presets.Add(new PresetData { name = name, content = content }); presetStatus.Text = "预设【" + name + "】保存成功！"; }
            SaveConfig(); RefreshPresetList();
            for (int i = 0; i < presets.Count; i++) if (presets[i].name == name) { presetListBox.SelectedIndex = i; break; }
            txtPresetName.Text = ""; txtPresetContent.Text = ""; presetStatus.ForeColor = Green;
        }

        private void DoDeletePreset()
        {
            int idx = presetListBox.SelectedIndex;
            if (idx < 0 || idx >= presets.Count) { MessageBox.Show("请先选择要删除的预设！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string name = presets[idx].name;
            if (MessageBox.Show("确定要删除预设【" + name + "】吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            { presets.RemoveAt(idx); SaveConfig(); RefreshPresetList(); presetStatus.Text = "已删除预设【" + name + "】。"; presetStatus.ForeColor = Green; }
        }

        private void DoPastePreset()
        {
            int idx = presetListBox.SelectedIndex;
            if (idx < 0 || idx >= presets.Count) { MessageBox.Show("请先在列表中选择一个预设！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            var preset = presets[idx];
            presetStatus.Text = "正在尝试唤醒【像素蛋糕】客户端..."; presetStatus.ForeColor = Accent; panelPresets.Refresh();
            Clipboard.SetText("好友给你分享了1个预设-" + preset.name + "等「" + preset.content + "」，快复制口令打开PixCake导入吧~");
            this.WindowState = FormWindowState.Minimized; Thread.Sleep(300);
            bool activated = ActivatePixCake();
            if (!activated)
            {
                if (MessageBox.Show("未检测到运行中的【像素蛋糕】客户端。\n点击确定后，请在 2 秒内手动激活像素蛋糕窗口以触发导入！",
                    "需要手动对焦", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.Cancel)
                { this.WindowState = FormWindowState.Normal; presetStatus.Text = "操作已取消。"; presetStatus.ForeColor = Red; return; }
                Thread.Sleep(2000);
            }
            else Thread.Sleep(600);
            try { SendKeys.SendWait("^v"); presetStatus.Text = "已成功复制预设口令并唤醒像素蛋糕！"; presetStatus.ForeColor = Green; }
            catch { presetStatus.Text = "已复制口令，请手动激活窗口。"; presetStatus.ForeColor = Color.Orange; }
            finally { this.WindowState = FormWindowState.Normal; }
        }

        private void DoCopyPreset()
        {
            int idx = presetListBox.SelectedIndex;
            if (idx >= 0 && idx < presets.Count)
            { Clipboard.SetText("好友给你分享了1个预设-" + presets[idx].name + "等「" + presets[idx].content + "」，快复制口令打开PixCake导入吧~"); presetStatus.Text = "口令已复制到剪贴板！"; presetStatus.ForeColor = Green; }
            else MessageBox.Show("请先选择一个预设！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ════ Utilities ════
        private bool ActivatePixCake()
        {
            string[] titles = { "像素蛋糕", "PixCake", "PixCakeAI" };
            foreach (var proc in Process.GetProcesses())
            { try { string t = proc.MainWindowTitle; if (!string.IsNullOrEmpty(t)) foreach (var tt in titles) if (t.IndexOf(tt, StringComparison.OrdinalIgnoreCase) >= 0) { NativeMethods.SetForegroundWindow(proc.MainWindowHandle); return true; } } catch { } }
            return false;
        }

        private static string EscapeSendKeys(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                switch (c) { case '+': case '^': case '%': case '~': case '(': case ')': case '[': case ']': case '{': case '}': sb.Append('{').Append(c).Append('}'); break; default: sb.Append(c); break; }
            }
            return sb.ToString();
        }

        private static string FormatJson(string json)
        {
            var sb = new StringBuilder(); int indent = 0; bool inStr = false; bool esc = false;
            foreach (char c in json)
            {
                if (esc) { sb.Append(c); esc = false; continue; }
                if (c == '\\' && inStr) { sb.Append(c); esc = true; continue; }
                if (c == '"') { inStr = !inStr; sb.Append(c); continue; }
                if (inStr) { sb.Append(c); continue; }
                switch (c)
                {
                    case '{': case '[': sb.Append(c); sb.AppendLine(); indent++; sb.Append(new string(' ', indent * 2)); break;
                    case '}': case ']': sb.AppendLine(); indent--; if (indent < 0) indent = 0; sb.Append(new string(' ', indent * 2)); sb.Append(c); break;
                    case ',': sb.Append(c); sb.AppendLine(); sb.Append(new string(' ', indent * 2)); break;
                    case ':': sb.Append(": "); break;
                    default: if (!char.IsWhiteSpace(c)) sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }

    // ═══ Entry Point ═══
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try { NativeMethods.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new MainForm()); }
            catch (Exception ex)
            {
                string log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                try { File.WriteAllText(log, DateTime.Now + "\n" + ex.ToString()); } catch { }
                MessageBox.Show("程序启动失败：\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
