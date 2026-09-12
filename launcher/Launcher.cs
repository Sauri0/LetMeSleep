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
        readonly ProgressBar bar = new ProgressBar();
        readonly Button retry = new Button(), previous = new Button();
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        readonly string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LetMeSleep");
        bool running;
        public LauncherForm() {
            Text = "Let me sleep"; ClientSize = new Size(520, 255); FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(24, 28, 49); ForeColor = Color.White; Font = new Font("Segoe UI", 10);
            var title = new Label { Text = "Let me sleep", Font = new Font("Segoe UI", 25, FontStyle.Bold), AutoSize = true, Location = new Point(24, 20) };
            status.SetBounds(26, 84, 465, 66); status.Text = "Preparando tu próxima noche…";
            bar.SetBounds(26, 155, 466, 12);
            retry.Text = "Reintentar"; retry.SetBounds(26, 191, 130, 35); retry.Visible = false;
            previous.Text = "Abrir versión instalada"; previous.SetBounds(169, 191, 220, 35); previous.Visible = false;
            foreach (var b in new[] { retry, previous }) { b.ForeColor = Color.Black; b.BackColor = Color.FromArgb(246, 206, 89); b.FlatStyle = FlatStyle.Flat; }
            Controls.AddRange(new Control[] { title, status, bar, retry, previous });
            retry.Click += async (s,e) => await Prepare();
            previous.Click += async (s,e) => {
                if (running) return;
                running = true; previous.Enabled = false;
                var install = await Task.Run(() => new Updater(root, Report, cancellation.Token).Current());
                if (install != null) await Launch(install); else { previous.Visible = false; status.Text = "La instalación necesita repararse. Elegí Reintentar."; }
                running = false; previous.Enabled = true;
            };
            Shown += async (s,e) => await Prepare();
            FormClosing += (s,e) => cancellation.Cancel();
        }
        void Report(string text, int percent) {
            if (IsDisposed || cancellation.IsCancellationRequested) return;
            try { BeginInvoke((Action)(() => { status.Text = text; bar.Value = Math.Max(0, Math.Min(100, percent)); })); }
            catch (InvalidOperationException) {}
        }
        async Task Prepare() {
            if (running) return; running = true; retry.Visible = previous.Visible = false;
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
