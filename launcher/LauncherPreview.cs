using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace LetMeSleep.Updater {
    static class LauncherPreview {
        [STAThread] static int Main(string[] args) {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new LauncherForm()) {
                // DrawToBitmap creates offscreen handles only. Never Show, run a message loop, download or launch the game.
                form.ShowInstall();
                if (form.Icon == null || form.Icon.Width < 16) throw new Exception("Window icon missing");
                foreach (Control control in form.Controls) {
                    if (control.Right > form.ClientSize.Width || control.Bottom > form.ClientSize.Height)
                        throw new Exception("Control exceeds window: " + control.Text);
                }
                // A hidden top-level form suppresses child painting. Reparent its real controls into a standalone panel.
                using (var surface = new Panel { Size = form.ClientSize, BackColor = form.BackColor, ForeColor = form.ForeColor, Font = form.Font }) {
                    var children = new Control[form.Controls.Count]; form.Controls.CopyTo(children,0);
                    surface.Controls.AddRange(children);
                    using (var preview = new Bitmap(surface.Width,surface.Height)) {
                        surface.DrawToBitmap(preview,new Rectangle(0,0,preview.Width,preview.Height));
                        preview.Save(args[0],ImageFormat.Png);
                    }
                }
                using (var ico = new BinaryReader(File.OpenRead(args[1]))) {
                    if (ico.ReadUInt16() != 0 || ico.ReadUInt16() != 1 || ico.ReadUInt16() != 7) throw new Exception("Expected seven Windows icon sizes");
                }
                Console.WriteLine("PREVIEW_PASS offscreen=true game_launched=false icon_sizes=7");
            }
            return 0;
        }
    }
}
