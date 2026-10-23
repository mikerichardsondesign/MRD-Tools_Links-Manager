using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MRDTools.Checks
{
    /// <summary>
    /// Counts the checks as they pass or fail, and helps drive WPF windows that are shown off screen.
    /// </summary>
    static class Checker
    {
        public static int Passes { get; private set; }
        public static int Failures { get; private set; }

        public static void Check(bool ok, string what)
        {
            if (ok)
            {
                Passes++;
            }
            else
            {
                Failures++;
                Console.WriteLine("FAIL: " + what);
            }
        }

        public static void Eq(object expected, object actual, string what) =>
            Check(Equals(expected, actual), $"{what}: expected [{expected}] got [{actual}]");

        // The Segoe MDL2 Assets glyphs the window uses as icons
        public static readonly string TickGlyph = char.ConvertFromUtf32(0xE73E);
        public static readonly string WarningGlyph = char.ConvertFromUtf32(0xE7BA);
        public static readonly string DashGlyph = char.ConvertFromUtf32(0xE738);
        public static readonly string LockGlyph = char.ConvertFromUtf32(0xE72E);

        /// <summary>Shows a window far off screen, without taking the focus, so it lays out and renders as it would for real.</summary>
        public static void ShowOffscreen(Window window)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000;
            window.Top = -20000;
            window.ShowInTaskbar = false;
            window.ShowActivated = false;
            window.Show();
            Pump(window);
        }

        /// <summary>Lets the window lay itself out and run everything waiting on its thread.</summary>
        public static void Pump(Window window)
        {
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// Lets this thread's dispatcher run until done or the time's up, for background work that reports back to it.
        /// It waits at the lowest priority the window uses, so the Saved In reads (ApplicationIdle) run too.
        /// </summary>
        public static bool WaitFor(Func<bool> done, int seconds = 30)
        {
            DateTime until = DateTime.Now.AddSeconds(seconds);
            while (!done() && DateTime.Now < until)
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                System.Threading.Thread.Sleep(10);
            }
            return done();
        }

        /// <summary>Saves a window's content as a PNG.</summary>
        public static void Render(Window window, string file)
        {
            Pump(window);
            RenderElement((FrameworkElement)window.Content, file);
        }

        public static void RenderElement(FrameworkElement element, string file)
        {
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight),
                96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(file))
                encoder.Save(stream);
        }

        /// <summary>The first element below parent, in the visual tree, of type T that matches.</summary>
        public static T FindChild<T>(DependencyObject parent, Func<T, bool> match) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found && match(found)) return found;
                T deeper = FindChild(child, match);
                if (deeper != null) return deeper;
            }
            return null;
        }

        /// <summary>Calls a private method, such as a button's click handler.</summary>
        public static object CallPrivate(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        /// <summary>Presses a button by calling its click handler, as the window has no names for its toolbar buttons.</summary>
        public static void Click(object window, string handler) => CallPrivate(window, handler, null, new RoutedEventArgs());

        public static T PrivateField<T>(object target, string field) =>
            (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        /// <summary>Shows a link's file as found or not, as a real project's would be, for the help page's screenshots.</summary>
        public static void Pretend(LinkRow row, bool found)
        {
            string file = row.BeginFileCheck();
            if (file != null) row.EndFileCheck(file, found);
        }
    }
}
