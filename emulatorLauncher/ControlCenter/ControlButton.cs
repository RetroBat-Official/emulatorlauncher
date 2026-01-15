using System.Drawing;
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

        private System.Drawing.Drawing2D.GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
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

            Rectangle rect = ClientRectangle;
            rect.Width--;
            rect.Height--;

            int radius = 5;

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (var path = GetRoundedRectPath(rect, radius))
            {
                using (Brush bg = new SolidBrush(_isPressed ? Color.FromArgb(24, 24, 24) : this.Focused ? Color.FromArgb(0, 16, 16) : Color.FromArgb(36, 36, 40)))
                    e.Graphics.FillPath(bg, path);

                using (Pen pen = new Pen(this.Focused ? Color.DarkCyan : Color.FromArgb(44, 44, 46)))
                    e.Graphics.DrawPath(pen, path);
            }

            TextFormatFlags tf = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, tf);
        }
    }
}
