using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LetMeSleep.Updater {
    static class Program {
        [STAThread] static void Main() {
            bool owner;
            using (var single = new Mutex(true, @"Local\LetMeSleepUpdater", out owner)) {
                if (!owner) { MessageBox.Show("Let me sleep ya se está preparando o está abierto.", "Let me sleep"); return; }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new LauncherForm());
            }
        }
    }
    sealed class LauncherForm : Form {
        readonly Label status = new Label();
        readonly ProgressTrack bar = new ProgressTrack();
        readonly NightButton retry = new NightButton(), previous = new NightButton { Secondary = true };
        readonly NightButton browse = new NightButton { Secondary = true }, installButton = new NightButton(), changeFolder = new NightButton { Secondary = true };
        readonly Label heading = new Label(), folderCaption = new Label(), hint = new Label();
        readonly TextBox folder = new TextBox();
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        readonly InstallLocation location = new InstallLocation(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LetMeSleepLauncher"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LetMeSleep"));
        string root;
        bool running;
        public LauncherForm() {
            root = location.Load();
            Text = "Let me sleep · Inicio"; ClientSize = new Size(800, 444); FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96,96);
            BackColor = Theme.Background; ForeColor = Theme.Cream; Font = new Font("Segoe UI", 10);
            var side = new Panel { BackColor = Theme.Sidebar, Bounds = new Rectangle(0,0,244,444) };
            var art = new PictureBox { Bounds = new Rectangle(26,24,192,192), SizeMode = PictureBoxSizeMode.Zoom };
            using (var stream = typeof(LauncherForm).Assembly.GetManifestResourceStream("LauncherLogo"))
            using (var image = Image.FromStream(stream)) art.Image = new Bitmap(image);
            using (var stream = typeof(LauncherForm).Assembly.GetManifestResourceStream("LauncherIcon"))
            using (var icon = new Icon(stream)) Icon = (Icon)icon.Clone();
            var title = new Label { Text = "Let me\nsleep", Font = new Font("Segoe UI", 29, FontStyle.Bold), Bounds = new Rectangle(30,221,210,106), ForeColor = Theme.Cream };
            var tagline = new Label { Text = "BUENAS NOCHES.\nMALA COMPAÑÍA.", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Theme.Teal, Bounds = new Rectangle(34,353,190,46) };
            side.Controls.AddRange(new Control[] { art, title, tagline });
            heading.Text = "Preparando tu noche"; heading.Font = new Font("Segoe UI", 22, FontStyle.Bold); heading.SetBounds(274,32,496,50);
            status.SetBounds(277,94,487,66); status.Text = "Buscando tu instalación…"; status.ForeColor = Theme.Muted;
            bar.SetBounds(278,198,488,12);
            retry.Text = "Reintentar"; retry.SetBounds(276,250,142,42); retry.Visible = false;
            previous.Text = "Abrir versión instalada"; previous.SetBounds(430,250,336,42); previous.Visible = false;
            folderCaption.Text = "CARPETA DEL JUEGO"; folderCaption.Font = new Font("Segoe UI",9,FontStyle.Bold); folderCaption.ForeColor = Theme.Teal;
            folderCaption.SetBounds(278,171,440,22); folderCaption.Visible = false;
            folder.SetBounds(278,199,488,28); folder.ReadOnly = true; folder.Visible = false;
            folder.BackColor = Color.FromArgb(35,52,68); folder.ForeColor = Theme.Cream; folder.BorderStyle = BorderStyle.FixedSingle; folder.AccessibleName = "Carpeta del juego";
            browse.Text = "Elegir carpeta…"; browse.SetBounds(276,239,165,38); browse.Visible = false;
            installButton.Text = "Instalar y jugar"; installButton.SetBounds(276,344,260,48); installButton.Visible = false;
            changeFolder.Text = "Cambiar carpeta"; changeFolder.SetBounds(276,306,180,38); changeFolder.Visible = false;
            hint.Text = "Actualizaciones automáticas.\nTus ajustes y personalización se conservan."; hint.ForeColor = Theme.Muted;
            hint.Font = new Font("Segoe UI",9); hint.SetBounds(279,290,485,42);
            var footer = new Label { Text = "WINDOWS 64 BITS   ·   INICIO 1.1.0", ForeColor = Theme.Muted, Font = new Font("Segoe UI",8), Bounds = new Rectangle(279,414,460,20) };
            Controls.AddRange(new Control[] { side, heading, status, bar, retry, previous, folderCaption, folder, browse, installButton, changeFolder, hint, footer });
            FormClosed += (s,e) => { art.Image.Dispose(); Icon.Dispose(); };
            browse.Click += (s,e) => {
                using (var dialog = new FolderBrowserDialog { Description = "Elegí dónde guardar el juego. Crearemos la carpeta LetMeSleep dentro.", ShowNewFolderButton = true }) {
                    if (Directory.Exists(folder.Text)) dialog.SelectedPath = folder.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) {
                        string selected = dialog.SelectedPath;
                        folder.Text = String.Equals(Path.GetFileName(selected), "LetMeSleep", StringComparison.OrdinalIgnoreCase)
                            ? selected : Path.Combine(selected, "LetMeSleep");
                    }
                }
            };
            installButton.Click += async (s,e) => {
                if (running) return;
                try { location.Save(folder.Text); root = location.Load(); }
                catch { status.Text = "No podemos escribir en esa carpeta. Elegí otra ubicación con permiso de escritura."; browse.Focus(); return; }
                await Prepare();
            };
            changeFolder.Click += (s,e) => { if (!running) ShowInstall(); };
            retry.Click += async (s,e) => await Prepare();
            previous.Click += async (s,e) => {
                if (running) return;
                running = true; previous.Enabled = false;
                var install = await Task.Run(() => new Updater(root, Report, cancellation.Token).Current());
                if (install != null) await Launch(install); else { previous.Visible = false; status.Text = "La instalación necesita repararse. Elegí Reintentar."; }
                running = false; previous.Enabled = true;
            };
            Shown += async (s,e) => {
                var existing = await Task.Run(() => new Updater(root, Report, cancellation.Token).Current());
                if (cancellation.IsCancellationRequested) return;
                if (existing == null) ShowInstall(); else await Prepare();
            };
            FormClosing += (s,e) => cancellation.Cancel();
        }
        internal void ShowInstall() {
            retry.Visible = previous.Visible = changeFolder.Visible = bar.Visible = false;
            heading.Text = "Instalá el juego";
            hint.Visible = folderCaption.Visible = true;
            status.Text = "¿Dónde querés instalar el juego? Descargaremos la última versión completa y la mantendremos actualizada.";
            folder.Text = root; folder.Visible = browse.Visible = installButton.Visible = true;
            AcceptButton = installButton; installButton.Focus();
        }
        void Report(string text, int percent) {
            if (IsDisposed || cancellation.IsCancellationRequested) return;
            try { BeginInvoke((Action)(() => { status.Text = text; bar.Value = Math.Max(0, Math.Min(100, percent)); })); }
            catch (InvalidOperationException) {}
        }
        async Task Prepare() {
            if (running) return; running = true; retry.Visible = previous.Visible = false;
            folder.Visible = browse.Visible = installButton.Visible = changeFolder.Visible = false;
            folderCaption.Visible = hint.Visible = false; heading.Text = "Preparando tu noche";
            bar.Visible = true; AcceptButton = retry;
            var updater = new Updater(root, Report, cancellation.Token);
            Exception failure = null;
            try {
                var install = await Task.Run(() => updater.EnsureLatest());
                if (!cancellation.IsCancellationRequested) await Launch(install);
            } catch (Exception error) {
                failure = error;
            }
            if (failure != null) {
                if (cancellation.IsCancellationRequested) return;
                try { Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "updater.log"), DateTime.UtcNow + "\n" + failure); } catch {}
                var fallback = await Task.Run(() => updater.Current());
                if (cancellation.IsCancellationRequested) return;
                status.Text = fallback == null ? "No pudimos preparar el juego. Revisá tu conexión y reintentá."
                    : "No pudimos actualizar. Podés reintentar o abrir " + fallback.Version + ". Para jugar online, todos necesitan la misma versión.";
                bar.Value = 0; retry.Visible = true; previous.Visible = fallback != null; retry.Focus();
                changeFolder.Visible = fallback == null;
            }
            running = false;
        }
        async Task Launch(Installation install) {
            try {
                var process = Process.Start(new ProcessStartInfo(install.Executable) { WorkingDirectory = install.DirectoryPath, UseShellExecute = false });
                if (process == null) throw new IOException("No se pudo iniciar el juego.");
                Hide(); await Task.Run(() => { process.WaitForExit(); process.Dispose(); }); Close();
            } catch {
                Show(); status.Text = "No se pudo abrir el juego. Cerrá cualquier partida abierta y reintentá.";
                retry.Visible = true;
            }
        }
    }
}
