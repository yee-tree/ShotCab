using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ShotCab.App
{
    internal sealed class AboutForm : Form
    {
        private const string ProjectUrl = "https://github.com/yee-tree/ShotCab";
        private const string UpstreamUrl = "https://github.com/ShareX/ShareX";

        internal AboutForm()
        {
            bool english = Localize.English;
            string version = typeof(AboutForm).Assembly.GetName().Version.ToString(3);
            Text = Localize.T("关于 ShotCab");
            ClientSize = new Size(510, 350);
            MinimumSize = Size;
            MaximumSize = Size;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Ui.Style(this);

            var body = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 8),
                ColumnCount = 1, RowCount = 7, BackColor = Ui.Background
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.Controls.Add(TextLine("ShotCab · " + (english ? "Screenshot Cabinet" : "图柜"), 17, FontStyle.Bold));
            body.Controls.Add(TextLine((english ? "Preview version " : "开发预览版 ") + version, 9, color: Ui.Muted));
            body.Controls.Add(TextLine(english
                ? "Capture, annotate, and organize images locally."
                : "在本地截图、标注和管理图片历史。", 10));
            body.Controls.Add(LinkRow(english ? "Project source" : "项目源码", ProjectUrl));
            body.Controls.Add(LinkRow(english ? "ShareX upstream" : "ShareX 上游", UpstreamUrl));
            body.Controls.Add(TextLine(english
                ? "Original ShotCab code: GPL-3.0-or-later. ShareX portions retain their GPLv3 terms. Based on ShareX 17.1.0; this is an independent project, not an official ShareX release. See NOTICE.md and the included Licenses directory."
                : "ShotCab 自有代码采用 GPL-3.0-or-later；ShareX 原有部分保留 GPLv3 条款。基于 ShareX 17.1.0，本项目为独立修改版本，并非 ShareX 官方发行版。来源与第三方声明见 NOTICE.md 和随附的 Licenses 目录。",
                9, color: Ui.Muted));
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            actions.Controls.Add(Ui.GlassButton(english ? "Close" : "关闭", Close));
            body.Controls.Add(actions);
            Controls.Add(body);
        }

        private static Label TextLine(string text, float size, FontStyle style = FontStyle.Regular, Color? color = null)
        {
            return new Label {
                Text = text, AutoSize = true, MaximumSize = new Size(448, 0),
                Font = Ui.Font(size, style), ForeColor = color ?? Ui.Text,
                Margin = new Padding(0, 0, 0, 11)
            };
        }

        private static Control LinkRow(string title, string url)
        {
            var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 1, 0, 9) };
            row.Controls.Add(new Label { Text = title + "  ·  ", AutoSize = true, ForeColor = Ui.Muted, Margin = new Padding(0, 2, 0, 0) });
            var link = new LinkLabel {
                Text = url, AutoSize = true, LinkColor = Ui.Text, ActiveLinkColor = Ui.Accent,
                VisitedLinkColor = Ui.Text, LinkBehavior = LinkBehavior.HoverUnderline,
                Margin = new Padding(0, 2, 0, 0)
            };
            link.LinkClicked += (sender, args) => {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch (Exception error) { Ui.Error(error); }
            };
            row.Controls.Add(link);
            return row;
        }
    }
}
