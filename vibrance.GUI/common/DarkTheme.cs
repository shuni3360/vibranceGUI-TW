using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace vibrance.GUI.common
{
    internal static class DarkTheme
    {
        private static readonly Color Window = Color.FromArgb(30, 32, 36);
        private static readonly Color Surface = Color.FromArgb(40, 43, 49);
        private static readonly Color Border = Color.FromArgb(96, 103, 115);
        private static readonly Color Text = Color.FromArgb(240, 243, 248);
        private static readonly Color Selected = Color.FromArgb(39, 77, 125);
        private static readonly Font UiFont = new Font("Microsoft JhengHei UI", 10.5F);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public static void Apply(Form form)
        {
            form.SuspendLayout();
            form.Font = UiFont;
            PaintControls(form);
            form.HandleCreated += delegate { SetTitleBar(form); };
            if (form.IsHandleCreated)
                SetTitleBar(form);
            form.ResumeLayout(true);
        }

        private static void SetTitleBar(Form form)
        {
            if (Environment.OSVersion.Version.Major < 6)
                return;

            int enabled = 1;
            if (DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
        }

        private static void PaintControls(Control control)
        {
            control.ForeColor = Text;
            control.BackColor = Window;

            Button button = control as Button;
            if (button != null)
            {
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Surface;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = Selected;
            }

            CheckBox checkBox = control as CheckBox;
            if (checkBox != null)
            {
                checkBox.UseVisualStyleBackColor = false;
                checkBox.FlatStyle = FlatStyle.Flat;
                checkBox.FlatAppearance.BorderColor = Border;
                checkBox.FlatAppearance.CheckedBackColor = Selected;
            }

            ListView listView = control as ListView;
            if (listView != null)
                listView.BackColor = Surface;

            ComboBox combo = control as ComboBox;
            if (combo != null)
            {
                combo.BackColor = Surface;
                combo.FlatStyle = FlatStyle.Flat;
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.ItemHeight = UiFont.Height + 2;
                combo.DrawItem += DrawComboItem;
            }

            foreach (Control child in control.Controls)
                PaintControls(child);

            if (control.ContextMenuStrip != null)
                PaintMenu(control.ContextMenuStrip);
        }

        public static void PaintMenu(ContextMenuStrip menu)
        {
            menu.Font = UiFont;
            menu.ShowImageMargin = false;
            menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            menu.BackColor = Surface;
            menu.ForeColor = Text;
            foreach (ToolStripItem item in menu.Items)
                item.ForeColor = Text;
        }

        private static void DrawComboItem(object sender, DrawItemEventArgs e)
        {
            var combo = (ComboBox)sender;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var brush = new SolidBrush(selected ? Selected : Surface))
                e.Graphics.FillRectangle(brush, e.Bounds);

            string label = e.Index < 0 ? combo.Text : combo.GetItemText(combo.Items[e.Index]);
            TextRenderer.DrawText(e.Graphics, label, combo.Font, e.Bounds,
                combo.Enabled ? Text : Color.FromArgb(163, 172, 184),
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        }

        private sealed class DarkMenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Surface; } }
            public override Color ImageMarginGradientBegin { get { return Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Surface; } }
            public override Color ImageMarginGradientEnd { get { return Surface; } }
            public override Color MenuItemSelected { get { return Selected; } }
            public override Color MenuItemBorder { get { return Border; } }
            public override Color MenuBorder { get { return Border; } }
        }
    }
}
