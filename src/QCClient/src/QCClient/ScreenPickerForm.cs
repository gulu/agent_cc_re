using System;
using System.Drawing;
using System.Windows.Forms;

namespace QCClient
{
    /// <summary>
    /// 全屏屏幕框选蒙层。鼠标拖动选择区域，松开返回选中矩形。
    /// </summary>
    public class ScreenPickerForm : Form
    {
        private Point _startPoint;
        private Point _endPoint;
        private bool _isSelecting;

        public Rectangle? SelectedRect { get; private set; }

        public ScreenPickerForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            BackColor = Color.Black;
            Opacity = 0.3;
            ShowInTaskbar = false;
            KeyPreview = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isSelecting = true;
                _startPoint = e.Location;
                _endPoint = e.Location;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_isSelecting)
            {
                _endPoint = e.Location;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_isSelecting && e.Button == MouseButtons.Left)
            {
                _isSelecting = false;
                int x = Math.Min(_startPoint.X, _endPoint.X);
                int y = Math.Min(_startPoint.Y, _endPoint.Y);
                int w = Math.Abs(_endPoint.X - _startPoint.X);
                int h = Math.Abs(_endPoint.Y - _startPoint.Y);

                if (w > 10 && h > 10)
                    SelectedRect = new Rectangle(x, y, w, h);

                DialogResult = DialogResult.OK;
                Close();
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_isSelecting)
            {
                using (var pen = new Pen(Color.Red, 2))
                {
                    int x = Math.Min(_startPoint.X, _endPoint.X);
                    int y = Math.Min(_startPoint.Y, _endPoint.Y);
                    int w = Math.Abs(_endPoint.X - _startPoint.X);
                    int h = Math.Abs(_endPoint.Y - _startPoint.Y);
                    e.Graphics.DrawRectangle(pen, x, y, w, h);

                    var fill = new SolidBrush(Color.FromArgb(60, 255, 0, 0));
                    e.Graphics.FillRectangle(fill, x, y, w, h);
                }
            }
            base.OnPaint(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
            base.OnKeyDown(e);
        }
    }
}
