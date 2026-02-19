using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace EmulatorLauncher.ControlCenter
{
    public class ScrollingLabel : Control
    {
        private const int WAIT_MS = 3000;
        private const float SPEED = 0.5f;
        private const float SPEEDUP = 8f;

        public ScrollingLabel()
        {
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.ResizeRedraw, true);            

            TabStop = false;
            DoubleBuffered = true;

            _timer = new Timer { Interval = 16 };
            _timer.Tick += OnTimerTick;
            _timer.Start();
            _stateTime = DateTime.Now;
        }
        
        private Timer _timer;
        private float _scrollY = 0;
        private float _textHeight;
        private int _state = 0;
        private DateTime _stateTime;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalcText();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            RecalcText();
        }

        private void RecalcText()
        {
            using (var g = CreateGraphics())
                _textHeight = MeasureJustifiedTextHeight(g);

            _scrollY = 0;
            _state = 0;
            _stateTime = DateTime.Now;
            Invalidate();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (_textHeight <= Height) 
                return;

            switch (_state)
            {
                case 0: // wait top
                    if ((DateTime.Now - _stateTime).TotalMilliseconds > WAIT_MS)
                        _state = 1;

                    break;

                case 1: // scroll down
                    _scrollY += SPEED;
                    if (_scrollY >= _textHeight - Height)
                    {
                        _scrollY = _textHeight - Height;
                        _state = 2;
                        _stateTime = DateTime.Now;
                    }
                    break;

                case 2: // wait bottom
                    if ((DateTime.Now - _stateTime).TotalMilliseconds > WAIT_MS)
                        _state = 3;

                    break;

                case 3: // scroll up
                    _scrollY -= SPEEDUP;
                    if (_scrollY <= 0)
                    {
                        _scrollY = 0;
                        _state = 0;
                        _stateTime = DateTime.Now;
                    }
                    break;
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.TranslateTransform(0, -_scrollY);
            DrawJustifiedText(e.Graphics);
            e.Graphics.ResetTransform();
        }

        private float MeasureJustifiedTextHeight(Graphics g)
        {
            float y = 0;
            foreach (var line in WrapWords(g))
                y += Font.GetHeight(g);
            return y;
        }

        private IEnumerable<string[]> WrapWords(Graphics g)
        {
            var words = Text.Split(' ');
            var line = new List<string>();
            float width = 0;

            foreach (var word in words)
            {
                var size = g.MeasureString(word + " ", Font);
                if (width + size.Width > ClientSize.Width && line.Count > 0)
                {
                    yield return line.ToArray();
                    line.Clear();
                    width = 0;
                }

                line.Add(word);
                width += size.Width;
            }

            if (line.Count > 0)
                yield return line.ToArray();
        }

        private void DrawJustifiedText(Graphics g)
        {
            float y = 0;

            var lines = new List<string[]>(WrapWords(g));
            int lastIndex = lines.Count - 1;

            for (int i = 0; i < lines.Count; i++)
            {
                var words = lines[i];

                float lineWidth = 0;
                foreach (var w in words)
                    lineWidth += g.MeasureString(w, Font).Width;

                float x = 0;

                if (i != lastIndex && words.Length > 1)
                {
                    float space = (ClientSize.Width - lineWidth) / (words.Length - 1);

                    foreach (var w in words)
                    {
                        g.DrawString(w, Font, new SolidBrush(ForeColor), x, y);
                        x += g.MeasureString(w, Font).Width + space;
                    }
                }
                else
                {
                    foreach (var w in words)
                    {
                        g.DrawString(w, Font, new SolidBrush(ForeColor), x, y);
                        x += g.MeasureString(w + " ", Font).Width;
                    }
                }

                y += Font.GetHeight(g);
            }
        }
    }
}
