using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LetMeSleep.Updater {
    static class Theme {
        public static readonly Color Background = Color.FromArgb(22, 34, 49);
        public static readonly Color Sidebar = Color.FromArgb(12, 25, 40);
        public static readonly Color Cream = Color.FromArgb(255, 244, 219);
        public static readonly Color Muted = Color.FromArgb(163, 186, 195);
        public static readonly Color Gold = Color.FromArgb(255, 208, 91);
        public static readonly Color Teal = Color.FromArgb(137, 215, 210);
        public static GraphicsPath Round(Rectangle bounds, int radius) {
            int d = radius * 2; var p = new GraphicsPath();
            p.AddArc(bounds.Left,bounds.Top,d,d,180,90); p.AddArc(bounds.Right-d,bounds.Top,d,d,270,90);
            p.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90); p.AddArc(bounds.Left,bounds.Bottom-d,d,d,90,90); p.CloseFigure(); return p;
        }
    }
    sealed class NightButton : Button {
        bool hover;
        public bool Secondary;
        public NightButton() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand; Font = new Font("Segoe UI", 10, FontStyle.Bold);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = Theme.Round(new Rectangle(1,1,Width-3,Height-3),8))
            using (var fill = new SolidBrush(!Enabled ? Color.FromArgb(65,75,85) : Secondary ? Color.FromArgb(38,58,73) : hover ? Color.FromArgb(255,224,146) : Theme.Gold))
            using (var border = new Pen(Focused ? Theme.Cream : Secondary ? Color.FromArgb(77,107,121) : Theme.Gold, Focused ? 2 : 1)) {
                e.Graphics.FillPath(fill,shape); e.Graphics.DrawPath(border,shape);
            }
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Secondary ? Theme.Cream : Theme.Sidebar,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
    sealed class ProgressTrack : Control {
        int value;
        public int Value { get { return value; } set { this.value = Math.Max(0,Math.Min(100,value)); Invalidate(); } }
        public ProgressTrack() { DoubleBuffered = true; AccessibleName = "Progreso de descarga"; }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var track = Theme.Round(new Rectangle(0,0,Width-1,Height-1),5))
            using (var fill = new SolidBrush(Color.FromArgb(43,63,78))) e.Graphics.FillPath(fill,track);
            int length = (Width-1) * value / 100;
            if (length >= 10) using (var active = Theme.Round(new Rectangle(0,0,length,Height-1),5))
                using (var fill = new SolidBrush(Theme.Teal)) e.Graphics.FillPath(fill,active);
        }
    }
}
