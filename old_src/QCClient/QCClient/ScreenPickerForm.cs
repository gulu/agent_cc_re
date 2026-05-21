using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QCClient
{
    /// <summary>
    /// 全屏半透明蒙层 — 鼠标拖拽框选 OCR 区域
    /// 支持多屏幕，Show() 自动覆盖所有屏幕
    /// </summary>
    public class ScreenPickerForm : Form
    {
        private Point _drawStart;
        private Point _drawCurrent;
        private Rectangle _selectedRect;
        private bool _hasSelection;

        // 物理屏幕坐标（Cursor.Position），用于返回精确的截取坐标
        private Point _physStart;
        private Point _physEnd;
        private bool _isDrawing;

        public Rectangle? SelectedRect { get; private set; }

        public ScreenPickerForm()
        {
            InitForm();
        }

        private void InitForm()
        {
            // 覆盖所有屏幕
            var bounds = GetAllScreensBounds();
            this.StartPosition = FormStartPosition.Manual;
            this.Location = bounds.Location;
            this.Size = bounds.Size;

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Normal;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.Cursor = Cursors.Cross;
            this.BackColor = Color.Black;
            this.Opacity = 0.35;
            this.KeyPreview = true;
            this.DoubleBuffered = true;

            this.MouseDown += OnMouseDown;
            this.MouseMove += OnMouseMove;
            this.MouseUp += OnMouseUp;
            this.KeyDown += OnKeyDown;
            this.Paint += OnPaint;
        }

        /// <summary>计算所有屏幕的边界矩形</summary>
        public static Rectangle GetAllScreensBounds()
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var screen in Screen.AllScreens)
            {
                var b = screen.Bounds;
                if (b.Left < minX) minX = b.Left;
                if (b.Top < minY) minY = b.Top;
                if (b.Right > maxX) maxX = b.Right;
                if (b.Bottom > maxY) maxY = b.Bottom;
            }

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _drawStart = e.Location;      // 虚拟坐标 → OnPaint 显示用
                _drawCurrent = e.Location;
                _physStart = Cursor.Position;  // ★ 物理屏幕坐标 → 返回值用
                _physEnd = _physStart;
                _isDrawing = true;
                _hasSelection = false;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing) return;
            _drawCurrent = e.Location;         // 虚拟坐标 → OnPaint 显示用
            _physEnd = Cursor.Position;        // ★ 物理屏幕坐标 → 返回值用
            this.Invalidate();
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (!_isDrawing) return;
            _isDrawing = false;

            // 显示用矩形（虚拟坐标）→ OnPaint
            var drawRect = NormalizeRect(_drawStart, _drawCurrent);
            _selectedRect = drawRect;
            _hasSelection = true;

            // ★ 物理坐标矩形（Cursor.Position）→ 直接用于 CopyFromScreen
            //    Cursor.Position 是系统级物理像素坐标，无需任何 DPI 转换
            _physEnd = Cursor.Position;
            var physRect = NormalizeRect(_physStart, _physEnd);

            if (physRect.Width > 5 && physRect.Height > 5)
            {
                SelectedRect = physRect;
                Logger.Info($"[SCREEN-PICK] physical=({physRect.X},{physRect.Y},{physRect.Width}x{physRect.Height}) | draw=({drawRect.X},{drawRect.Y})");

                // 短暂高亮显示结果，然后关闭
                var timer = new Timer { Interval = 600 };
                timer.Tick += (s, ev) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                };
                timer.Start();
                this.Invalidate();
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;

            if (_isDrawing || _hasSelection)
            {
                var rect = _hasSelection ? _selectedRect : NormalizeRect(_drawStart, _drawCurrent);

                // 清除选中区域的半透明蒙层（挖洞效果）
                using (var clearBrush = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
                {
                    var clearRegion = new Region(this.ClientRectangle);
                    clearRegion.Exclude(rect);
                    g.FillRegion(clearBrush, clearRegion);
                }

                // 选中区域边框和背景
                using (var borderPen = new Pen(Color.FromArgb(255, 30, 96, 190), 2))
                using (var fillBrush = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                {
                    g.FillRectangle(fillBrush, rect);
                    g.DrawRectangle(borderPen, rect);
                }

                // 尺寸标签
                var label = $"{rect.Width} × {rect.Height}  (X:{rect.X}, Y:{rect.Y})";
                var labelFont = new Font("Microsoft YaHei", 11, FontStyle.Regular);
                var labelSize = g.MeasureString(label, labelFont);
                var labelX = rect.X + rect.Width - labelSize.Width - 4;
                var labelY = rect.Y < 30 ? rect.Bottom + 4 : rect.Y - labelSize.Height - 4;

                using (var labelBg = new SolidBrush(Color.FromArgb(220, 30, 96, 190)))
                using (var labelBrush = new SolidBrush(Color.White))
                {
                    var labelRect = new RectangleF(labelX - 4, labelY - 2, labelSize.Width + 8, labelSize.Height + 4);
                    g.FillRectangle(labelBg, labelRect);
                    g.DrawString(label, labelFont, labelBrush, labelX, labelY);
                }
                labelFont.Dispose();
            }

            // 提示文字（无选择时居中显示）
            if (!_isDrawing && !_hasSelection)
            {
                var hint = "拖拽鼠标框选 OCR 识别区域\n按 ESC 取消";
                var hintFont = new Font("Microsoft YaHei", 16, FontStyle.Regular);
                var hintSize = g.MeasureString(hint, hintFont);
                using (var hintBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                {
                    g.DrawString(hint, hintFont, hintBrush,
                        (this.Width - hintSize.Width) / 2,
                        (this.Height - hintSize.Height) / 2);
                }
                hintFont.Dispose();
            }
        }

        private static Rectangle NormalizeRect(Point p1, Point p2)
        {
            int x = Math.Min(p1.X, p2.X);
            int y = Math.Min(p1.Y, p2.Y);
            int w = Math.Abs(p2.X - p1.X);
            int h = Math.Abs(p2.Y - p1.Y);
            return new Rectangle(x, y, w, h);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 清理资源
            }
            base.Dispose(disposing);
        }
    }
}
