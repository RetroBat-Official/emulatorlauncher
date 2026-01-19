using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EmulatorLauncher.ControlCenter
{
    class ControlButton : System.Windows.Forms.Button
    {
        public ControlButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        public static System.Drawing.Drawing2D.GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);

            path.CloseFigure();
            return path;
        }

        private bool _isPressed = false;
        private bool _isHovered = false;

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e); 
            
            if (!_isHovered)
            {
                _isHovered = true;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_isHovered)
            {
                _isHovered = false;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!_isPressed)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_isPressed)
            {
                _isPressed = false;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {

            if (Parent != null)
                e.Graphics.Clear(Parent.BackColor);
            else
                ButtonRenderer.DrawParentBackground(e.Graphics, ClientRectangle, this);

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int radius = 6;

            Rectangle rect = ClientRectangle;
            rect.Width--;
            rect.Height--;

            using (var path = GetRoundedRectPath(rect, radius))
            {
                Color backColor = _isPressed ? Color.FromArgb(24, 24, 24) : this.Focused ? Color.FromArgb(0, 16, 16) : Color.FromArgb(36, 36, 40);
                if (_isHovered && !_isPressed)
                    backColor = Focused ? Color.FromArgb(8, 24, 24) : Color.FromArgb(44, 44, 50);

                using (Brush bg = new SolidBrush(backColor))
                    e.Graphics.FillPath(bg, path);

                Rectangle highlightRect = rect;
                highlightRect.Height = rect.Height / 2 + 1;

                using (var highlightPath = GetRoundedRectPath(highlightRect, radius))
                {
                    highlightRect.Height++;
                    using (var highlightBrush = new LinearGradientBrush(
                        highlightRect,
                        Color.FromArgb(20, Color.White),
                        Color.FromArgb(0, Color.White),
                        LinearGradientMode.Vertical))
                    {
                        e.Graphics.FillPath(highlightBrush, highlightPath);
                    }
                }

                Rectangle innerRect = rect;
                innerRect.Inflate(-1, -1);

                using (var innerPath = GetRoundedRectPath(innerRect, radius - 1))
                using (var innerPen = new Pen(Color.FromArgb(12, Color.White)))
                    e.Graphics.DrawPath(innerPen, innerPath);

                using (Pen pen = new Pen(this.Focused ? Color.DarkCyan : Color.FromArgb(0, 8, 8))) // Color.FromArgb(44, 44, 46)
                    e.Graphics.DrawPath(pen, path);
            }

            TextFormatFlags tf = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, tf);
        }
    }
}
